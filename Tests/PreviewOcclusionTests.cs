using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class PreviewOcclusionGeometryTests
{
    [UnityTest]
    public IEnumerator WidePreviewAvoidsThinEdgeOccluderAndRestoresWithoutDrift()
    {
        var camera = new GameObject("Occlusion camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(100, 100, -6);
        camera.nearClipPlane = .1f;
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.transform.position = new Vector3(100, 100, 1);
        source.transform.localScale = new Vector3(2, 1, 1);
        source.SetActive(false);
        var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.transform.position = new Vector3(100.6f, 100, -2);
        obstacle.transform.localScale = new Vector3(.08f, 2, .1f);
        Object.DestroyImmediate(obstacle.GetComponent<Collider>()); // Real puzzle pieces also lack colliders.
        var original = source.transform.localToWorldMatrix;
        using (var query = new PuzzlePreviewOcclusion(new[] { obstacle }))
        using (var preview = new PuzzleDragPreview(source))
        {
            Vector3 anchor = PuzzlePlacementAnchor.CalculateBottomCenter(source);
            preview.Show(new RaycastHit { point = anchor });
            preview.UpdateCameraRelativeRotation(camera.transform.rotation);
            Assert.IsTrue(preview.TryGetCameraBounds(camera, out var bounds));
            Assert.IsTrue(query.TryGetOffset(camera, bounds, anchor, out var offset));
            Assert.Less(offset.z, -3, "Thin edge obstacle must be found although center ray misses it");
            preview.ApplyVisibilityOffset(offset, true);
            var renderer = preview.Root.GetComponentInChildren<Renderer>();
            Assert.Less(renderer.bounds.max.z, obstacle.GetComponent<Renderer>().bounds.min.z);
            Assert.That(Vector2.Distance(camera.WorldToScreenPoint(anchor), camera.WorldToScreenPoint(preview.VisualAnchorWorldPosition)), Is.LessThan(.001f));
            Assert.AreEqual(anchor, preview.AnchorWorldPosition);
            Assert.AreEqual(original, source.transform.localToWorldMatrix);
            obstacle.SetActive(false);
            for (int i = 0; i < 10; i++)
            {
                preview.UpdateCameraRelativeRotation(camera.transform.rotation);
                preview.TryGetCameraBounds(camera, out bounds);
                Assert.IsTrue(query.TryGetOffset(camera, bounds, anchor, out offset));
                Assert.AreEqual(Vector3.zero, offset);
                preview.ApplyVisibilityOffset(offset, true);
                Assert.That(Vector3.Distance(anchor, preview.VisualAnchorWorldPosition), Is.LessThan(.00001f));
            }
        }
        Object.Destroy(camera.gameObject); Object.Destroy(source); Object.Destroy(obstacle);
        yield return null;
        Assert.IsNull(GameObject.Find("Puzzle Preview Occlusion"));
    }

    [UnityTest]
    public IEnumerator NearCameraOverlapUsesFallbackAndCanRecoverWithoutPointerMovement()
    {
        var camera = new GameObject("Occlusion camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(100, 100, -6);
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.transform.position = new Vector3(100, 100, 1); source.SetActive(false);
        var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.transform.position = camera.transform.position;
        using (var query = new PuzzlePreviewOcclusion(new[] { obstacle }))
        using (var preview = new PuzzleDragPreview(source))
        {
            Vector3 anchor = PuzzlePlacementAnchor.CalculateBottomCenter(source);
            preview.Show(new RaycastHit { point = anchor });
            preview.UpdateCameraRelativeRotation(camera.transform.rotation);
            preview.TryGetCameraBounds(camera, out var bounds);
            Assert.IsFalse(query.TryGetOffset(camera, bounds, anchor, out var offset));
            preview.ApplyVisibilityOffset(offset, false);
            Assert.IsFalse(preview.IsVisible); Assert.IsTrue(preview.HasPlacementPosition);
            Assert.AreEqual(anchor, preview.AnchorWorldPosition);
            obstacle.SetActive(false);
            preview.UpdateCameraRelativeRotation(camera.transform.rotation);
            preview.TryGetCameraBounds(camera, out bounds);
            Assert.IsTrue(query.TryGetOffset(camera, bounds, anchor, out offset));
            preview.ApplyVisibilityOffset(offset, true);
            Assert.IsTrue(preview.IsVisible);
        }
        Object.Destroy(camera.gameObject); Object.Destroy(source); Object.Destroy(obstacle);
        yield return null;
    }
}

public partial class FullFlowTests
{
    [UnityTest]
    public IEnumerator OcclusionChecksWholePreviewDuringCameraAnimation([Values(1, 10)] int speed)
    {
        yield return EnterPuzzle("Structure", "Egypt");
        var owner = GameManager.Instance;
        Object.FindAnyObjectByType<Timer>().enabled = false;
        GameSettings.SetCameraRotationSpeedLevel(speed);
        var camera = Camera.main;
        var controller = camera.GetComponent<PuzzleCameraController>();
        var big = owner.Puzzle["Big_pyramid"];
        while (controller.CurrentDirection != big.Direction)
        {
            controller.RotateRight(); yield return WaitFor(() => !controller.IsRotating, "Pyramid camera side");
        }
        Assert.IsTrue(owner.TryCompletePiece("Big_pyramid", big.Placement.WorldPosition));
        var target = owner.Puzzle["Small_pyramid"];
        var piece = owner.PieceContent.GetComponentsInChildren<BlockUI>().Single(p => PieceKey(p) == "Small_pyramid");
        var pointer = new PointerEventData(EventSystem.current) { position = camera.WorldToScreenPoint(target.Placement.WorldPosition) };
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        var preview = Preview(piece);
        var renderers = preview.Root.GetComponentsInChildren<Renderer>();
        Vector3 anchor = preview.AnchorWorldPosition;
        // Independent visibility oracle: ordinary rays to the final visual box
        // corners must not meet the actual placed pyramid mesh first.
        var mesh = big.Object.GetComponentInChildren<MeshFilter>();
        var oracleObject = new GameObject("Placed mesh ray oracle");
        oracleObject.layer = 2;
        oracleObject.transform.SetPositionAndRotation(mesh.transform.position, mesh.transform.rotation);
        oracleObject.transform.localScale = mesh.transform.lossyScale;
        var oracle = oracleObject.AddComponent<MeshCollider>(); oracle.sharedMesh = mesh.sharedMesh;
        Physics.SyncTransforms();
        int samples = 0, hidden = 0, blocked = 0;
        float anchorError = 0;
        var probe = new GameObject("Occlusion frame probe").AddComponent<PreviewRotationFrameProbe>();
        probe.Sample = () =>
        {
            if (!controller.IsRotating) return;
            samples++;
            if (!preview.IsVisible) { hidden++; return; }
            anchorError = Mathf.Max(anchorError, Vector3.Distance(anchor, preview.AnchorWorldPosition));
            foreach (var renderer in renderers)
            {
                Bounds bounds = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                    Vector3 point = renderer.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, sign));
                    Vector3 ray = point - camera.transform.position;
                    if (oracle.Raycast(new Ray(camera.transform.position, ray), out _, ray.magnitude - .001f)) blocked++;
                }
            }
        };
        try
        {
            for (int turn = 0; turn < 4; turn++)
            {
                if (turn % 2 == 0) controller.RotateRight(); else controller.RotateLeft();
                yield return WaitFor(() => !controller.IsRotating, "Rotate with stationary dragged preview");
                yield return null;
            }
            Assert.Greater(samples, 0); Assert.AreEqual(0, hidden); Assert.AreEqual(0, blocked);
            Assert.That(anchorError, Is.LessThan(.00001f));
            Debug.Log($"Preview occlusion speed {speed}: {samples} animated frames, {blocked} blocked rays, {hidden} hidden frames");
        }
        finally
        {
            probe.Sample = null;
            Object.Destroy(probe.gameObject); Object.Destroy(oracleObject);
            pointer.position = Vector2.one * -100; piece.OnEndDrag(pointer);
        }
    }

    [UnityTest]
    public IEnumerator OcclusionEgyptPyramidStaysVisibleWithoutChangingPlacementOrRewards()
    {
        yield return EnterPuzzle("Structure", "Egypt");
        var owner = GameManager.Instance;
        var timer = Object.FindAnyObjectByType<Timer>(); timer.enabled = false;
        var camera = Camera.main;
        var controller = camera.GetComponent<PuzzleCameraController>();
        var big = owner.Puzzle["Big_pyramid"];
        while (controller.CurrentDirection != big.Direction)
        {
            controller.RotateRight(); yield return WaitFor(() => !controller.IsRotating, "Pyramid camera side");
        }
        Assert.IsTrue(owner.TryCompletePiece("Big_pyramid", big.Placement.WorldPosition));
        var target = owner.Puzzle["Small_pyramid"];
        var sourcePose = target.Object.transform.localToWorldMatrix;
        var piece = owner.PieceContent.GetComponentsInChildren<BlockUI>().Single(p => PieceKey(p) == "Small_pyramid");
        var pointer = new PointerEventData(EventSystem.current) { position = camera.WorldToScreenPoint(target.Placement.WorldPosition) };
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        yield return null; yield return null;
        var preview = Preview(piece);
        Assert.IsNotNull(preview); Assert.IsTrue(preview.IsVisible);
        Assert.That(Vector3.Distance(preview.AnchorWorldPosition, target.Placement.WorldPosition), Is.LessThan(.001f));
        Assert.That(Vector3.Distance(preview.VisualAnchorWorldPosition, preview.AnchorWorldPosition), Is.GreaterThan(.05f));
        Assert.Less(camera.WorldToViewportPoint(preview.VisualAnchorWorldPosition).z, camera.WorldToViewportPoint(preview.AnchorWorldPosition).z);
        Assert.IsTrue(owner.CanPlacePiece("Small_pyramid", preview.AnchorWorldPosition));
        Vector2 logicalScreen = camera.WorldToScreenPoint(preview.AnchorWorldPosition);
        Vector2 visualScreen = camera.WorldToScreenPoint(preview.VisualAnchorWorldPosition);
        Assert.That(Vector2.Distance(logicalScreen, visualScreen), Is.LessThan(.01f));
        string path = System.IO.Path.GetFullPath("../../Logs/preview-occlusion-egypt.png");
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        yield return WaitFor(() => System.IO.File.Exists(path), "Visible pyramid capture");
        // Hidden list visits retain the logical anchor and the image-view rotation.
        pointer.position = ListPosition(owner); piece.OnDrag(pointer);
        Assert.IsFalse(preview.IsVisible);
        pointer.position = camera.WorldToScreenPoint(target.Placement.WorldPosition); piece.OnDrag(pointer);
        Assert.IsTrue(preview.IsVisible);
        Assert.IsTrue(preview.Root.GetComponentsInChildren<Renderer>().All(r => r.HasPropertyBlock()));
        owner.PauseGame();
        Assert.IsNull(Preview(piece)); Assert.IsNull(DragImage(piece));
        owner.ResumeGame(); piece.OnEndDrag(pointer); Assert.IsFalse(target.IsComplete);
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        preview = Preview(piece);
        Assert.IsTrue(preview.IsVisible);
        timer.enabled = true; timer.Clock.Start(); timer.Clock.Tick(10);
        float remaining = timer.Clock.RemainingTime;
        int score = owner.Score;
        piece.OnEndDrag(pointer);
        Assert.IsTrue(target.IsComplete); Assert.IsFalse(preview.IsVisible);
        Assert.AreEqual(sourcePose, target.Object.transform.localToWorldMatrix);
        Assert.AreEqual(score + owner.CurrentStageData.PointsPerPiece, owner.Score);
        timer.SendMessage("Update");
        Assert.That(timer.Clock.RemainingTime, Is.EqualTo(remaining + owner.CurrentStageData.TimeBonusSeconds).Within(.001f));
        yield return null;
        Assert.IsNull(GameObject.Find("Puzzle Preview Occlusion"));
    }
}
