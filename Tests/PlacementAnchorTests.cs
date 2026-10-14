using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public partial class FullFlowTests
{
    [UnityTest]
    public IEnumerator AnchorClockAndTreeVisualVerification([Values("Spring", "Bigben")] string stage)
    {
        yield return EnterPuzzle(stage == "Spring" ? "Weather" : "Structure", stage);
        var manager = GameManager.Instance;
        Object.FindAnyObjectByType<Timer>().enabled = false;
        string key = stage == "Spring" ? "Tree02" : "ClockTower";
        var target = manager.Puzzle[key];
        var camera = Camera.main.GetComponent<PuzzleCameraController>();
        while (camera.CurrentDirection != target.Direction)
        {
            camera.RotateRight();
            yield return WaitFor(() => !camera.IsRotating, "Visual test correct direction");
        }
        var piece = manager.PieceContent.GetComponentsInChildren<BlockUI>().Single(p => PieceKey(p) == key);
        var pointer = new PointerEventData(EventSystem.current) { position = Camera.main.WorldToScreenPoint(target.Placement.WorldPosition) };
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        var preview = Preview(piece);
        Vector3 anchor = preview.AnchorWorldPosition;
        GameSettings.SetCameraRotationSpeedLevel(1);
        Quaternion cameraStart = Camera.main.transform.rotation;
        for (int shot = 0; shot < 3; shot++)
        {
            yield return null; yield return null;
            string suffix = shot == 0 ? "-before.png" : shot == 1 ? "-during.png" : "-after.png";
            string path = System.IO.Path.GetFullPath("../../Logs/camera-relative-visual-" + stage + suffix);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            yield return WaitFor(() => System.IO.File.Exists(path), "Anchor camera screenshot");
            if (shot == 0)
            {
                camera.RotateRight();
                yield return WaitFor(() => Quaternion.Angle(cameraStart, Camera.main.transform.rotation) >= 30f, "Visual test intermediate turn");
            }
            else if (shot == 1) yield return WaitFor(() => !camera.IsRotating, "Visual test 90-degree turn");
            Assert.That(Vector3.Distance(anchor, preview.AnchorWorldPosition), Is.LessThan(.00001f));
        }
        pointer.position = Vector2.one * -100f; piece.OnEndDrag(pointer);
    }

    [UnityTest]
    public IEnumerator AnchorPreviewKeepsPlacementPointAcrossPointerMovementDuringRotation([Values("Spring", "Bigben")] string stage)
    {
        yield return EnterPuzzle(stage == "Spring" ? "Weather" : "Structure", stage);
        var manager = GameManager.Instance;
        Object.FindAnyObjectByType<Timer>().enabled = false;
        string key = stage == "Spring" ? "Tree02" : "ClockTower"; // Reproduction cases, never runtime exceptions.
        var target = manager.Puzzle[key];
        var piece = manager.PieceContent.GetComponentsInChildren<BlockUI>().Single(p => PieceKey(p) == key);
        var pointer = new PointerEventData(EventSystem.current) { position = Camera.main.WorldToScreenPoint(target.Placement.WorldPosition) };
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        var preview = Preview(piece);
        Assert.IsNotNull(preview); Assert.IsTrue(preview.IsVisible);
        Assert.IsNull(preview.Root.transform.parent);
        var camera = Camera.main.GetComponent<PuzzleCameraController>();
        var renderers = preview.Root.GetComponentsInChildren<Renderer>();
        var relative = renderers.Select(r => Quaternion.Inverse(Camera.main.transform.rotation) * r.transform.rotation).ToArray();
        Vector3 firstView = Camera.main.WorldToScreenPoint(renderers[0].bounds.center);
        for (int input = 0; input < 4; input++)
        {
            Vector3 fixedAnchor = preview.AnchorWorldPosition;
            if (input < 2)
            {
                KeyCode keyCode = input == 0 ? KeyCode.A : KeyCode.D;
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(PuzzleCameraController).GetField("getKeyDown", flags).SetValue(camera, new Func<KeyCode, bool>(k => k == keyCode));
                typeof(PuzzleCameraController).GetMethod("ReadKeyboardInput", flags).Invoke(camera, null);
                typeof(PuzzleCameraController).GetField("getKeyDown", flags).SetValue(camera, new Func<KeyCode, bool>(Input.GetKeyDown));
            }
            else Click(input == 2 ? "RotateLeft" : "RotateRight");
            Assert.IsTrue(camera.IsRotating);
            pointer.position += new Vector2(4f, 3f); // Even movement DURING the turn must not move the piece.
            piece.OnDrag(pointer);
            do
            {
                yield return null;
                Assert.That(Vector3.Distance(fixedAnchor, preview.AnchorWorldPosition), Is.LessThan(.00001f));
            } while (camera.IsRotating);
            piece.OnDrag(pointer); // Same coordinates after rotation are not a new movement.
            yield return null; yield return null;
            Assert.That(Vector3.Distance(fixedAnchor, preview.AnchorWorldPosition), Is.LessThan(.00001f), "No jump after completing the turn");
            for (int r = 0; r < renderers.Length; r++)
                Assert.That(Quaternion.Angle(relative[r], Quaternion.Inverse(Camera.main.transform.rotation) * renderers[r].transform.rotation), Is.LessThan(.1f));
            if (input == 0) Assert.That(Vector3.Distance(firstView, Camera.main.WorldToScreenPoint(renderers[0].bounds.center)), Is.GreaterThan(1f));
            // Choose a fresh point inside the board after each view change;
            // accumulating screen offsets can legitimately leave the bounded board.
            Vector3 nextAnchor = target.Placement.WorldPosition + Vector3.right * (.02f * (input + 1));
            pointer.position = Camera.main.WorldToScreenPoint(nextAnchor); piece.OnDrag(pointer);
            Assert.IsTrue(preview.IsVisible);
            Assert.That(Vector3.Distance(fixedAnchor, preview.AnchorWorldPosition), Is.GreaterThan(.001f), "New movement resumes ray projection");
        }
        pointer.position = Vector2.one * -100f; piece.OnEndDrag(pointer);
        Assert.IsFalse(target.IsComplete);
    }

    [Serializable] private class AnchorResultRow
    {
        public string key; public Vector3 pivot, boundsCenter, boundsSize, targetAnchor, previewAnchor;
        public Vector2 oldPivotScreen, anchorScreen; public float xzError; public bool canPlace;
    }
    [Serializable] private class AnchorResultReport { public string stage; public List<AnchorResultRow> pieces = new List<AnchorResultRow>(); }

    [UnityTest]
    public IEnumerator AnchorEveryRealPieceMatchesPreviewHintAndDrop(
        [Values("Spring", "Summer", "Desert", "Fall", "Winter", "Bigben", "Egypt", "OperaHouse", "TowerBridge")] string stage)
    {
        yield return EnterPuzzle((int)Enum.Parse(typeof(GameManager.Stage), stage) < 5 ? "Weather" : "Structure", stage);
        var manager = GameManager.Instance;
        var timer = Object.FindAnyObjectByType<Timer>();
        timer.enabled = false;
        var camera = Camera.main.GetComponent<PuzzleCameraController>();
        var report = new AnchorResultReport { stage = stage };
        for (int turn = 0; turn < 32 && !manager.IsSuccessful; turn++)
        {
            foreach (var piece in manager.PieceContent.GetComponentsInChildren<BlockUI>())
            {
                string key = PieceKey(piece); var target = manager.Puzzle[key];
                Vector3 anchor = target.Placement.WorldPosition;
                if (!manager.CanPlacePiece(key, anchor)) continue;
                float radius = target.Placement.GetSnapDistance(manager.CurrentStageData.SnapDistanceWorld);
                Assert.IsTrue(manager.CanPlacePiece(key, anchor + Vector3.up * 100f), "Y must not alter XZ distance");
                Assert.IsTrue(manager.CanPlacePiece(key, anchor + Vector3.right * (radius * .99f)));
                Assert.IsFalse(manager.CanPlacePiece(key, anchor + Vector3.right * (radius + .01f)));
                var pointer = new PointerEventData(EventSystem.current) { position = Camera.main.WorldToScreenPoint(anchor) };
                piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
                var preview = Preview(piece);
                Assert.IsNotNull(preview, stage + "/" + key + " ray did not hit at the target anchor");
                Assert.IsTrue(preview.IsVisible, stage + "/" + key + " preview hidden");
                Bounds bounds = target.Object.GetComponentsInChildren<Renderer>(true)[0].bounds;
                foreach (Renderer renderer in target.Object.GetComponentsInChildren<Renderer>(true).Skip(1)) bounds.Encapsulate(renderer.bounds);
                var row = new AnchorResultRow { key = key, pivot = target.Object.transform.position, boundsCenter = bounds.center,
                    boundsSize = bounds.size, targetAnchor = anchor, previewAnchor = preview.AnchorWorldPosition,
                    oldPivotScreen = Camera.main.WorldToScreenPoint(target.Object.transform.position), anchorScreen = pointer.position,
                    xzError = Vector2.Distance(new Vector2(anchor.x, anchor.z), new Vector2(preview.AnchorWorldPosition.x, preview.AnchorWorldPosition.z)),
                    canPlace = manager.CanPlacePiece(key, preview.AnchorWorldPosition) };
                report.pieces.Add(row);
                // Preserve a diagnostic row even if a real authored surface exposes a regression.
                System.IO.File.WriteAllText(System.IO.Path.GetFullPath("../../Logs/anchor-" + stage + ".json"), JsonUtility.ToJson(report, true));
                Assert.IsTrue(row.canPlace, stage + "/" + key + " anchor miss " + row.xzError);
                var properties = new MaterialPropertyBlock();
                foreach (Renderer renderer in preview.Root.GetComponentsInChildren<Renderer>())
                    for (int slot = 0; slot < renderer.sharedMaterials.Length; slot++)
                    {
                        renderer.GetPropertyBlock(properties, slot);
                        Assert.IsFalse(properties.isEmpty, "Valid placement must drive the MPB hint: " + key);
                    }
                int before = manager.CompletedPieceCount;
                piece.OnEndDrag(pointer);
                Assert.IsTrue(target.IsComplete, "Highlighted piece must drop successfully: " + key);
                Assert.AreEqual(before + 1, manager.CompletedPieceCount);
                Assert.IsNull(Preview(piece));
                foreach (Renderer renderer in target.Object.GetComponentsInChildren<Renderer>(true)) Assert.IsFalse(renderer.HasPropertyBlock());
            }
            yield return null;
            if (manager.IsSuccessful) break;
            camera.RotateRight();
            yield return WaitFor(() => !camera.IsRotating, "Anchor placement direction");
        }
        Assert.IsTrue(manager.IsSuccessful, stage + " all real pieces reached completion");
        Assert.AreEqual(manager.TotalPieceCount, report.pieces.Count);
    }

    [UnityTest]
    public IEnumerator AnchorCustomMarkerAndSnapOverridePreservePrerequisites()
    {
        yield return EnterPuzzle("Weather", "Spring");
        var manager = GameManager.Instance;
        var entry = manager.Puzzle.First(p => manager.CanPlacePiece(p.Key, p.Value.Placement.WorldPosition));
        var target = entry.Value;
        var marker = new GameObject("Custom Placement Anchor").transform;
        marker.SetParent(target.Object.transform, false);
        marker.position = target.Placement.WorldPosition + Vector3.right * .1f;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(PuzzlePlacementAnchor).GetField("placementAnchor", flags).SetValue(target.Placement, marker);
        typeof(PuzzlePlacementAnchor).GetField("useCustomPlacementAnchor", flags).SetValue(target.Placement, true);
        typeof(PuzzlePlacementAnchor).GetField("snapDistanceOverride", flags).SetValue(target.Placement, .035f);
        typeof(GameManager).GetMethod("HideDependentSurfaces", flags).Invoke(manager, null);
        Assert.AreEqual(marker.position, target.Placement.WorldPosition);
        Assert.IsTrue(manager.CanPlacePiece(entry.Key, marker.position + Vector3.right * .03f));
        Assert.IsFalse(manager.CanPlacePiece(entry.Key, marker.position + Vector3.right * .04f));
        using (var preview = new PuzzleDragPreview(target.Object))
        {
            Vector3 destination = marker.position + new Vector3(.4f, 3f, -.2f);
            preview.Show(new RaycastHit { point = destination, normal = Vector3.up });
            Assert.That(Vector3.Distance(destination, preview.AnchorWorldPosition), Is.LessThan(.00001f));
        }
        typeof(PuzzlePlacementAnchor).GetField("snapDistanceOverride", flags).SetValue(target.Placement, 0f);
        Assert.IsTrue(manager.CanPlacePiece(entry.Key, marker.position));
        Assert.IsFalse(manager.CanPlacePiece(entry.Key, marker.position + Vector3.right * .001f));
        typeof(PuzzlePlacementAnchor).GetField("snapDistanceOverride", flags).SetValue(target.Placement, -1f);
        Assert.IsTrue(manager.CanPlacePiece(entry.Key, marker.position + Vector3.right * .1f));
        Assert.IsTrue(manager.TryCompletePiece(entry.Key, marker.position));
        foreach (Transform child in target.Object.transform)
            if (manager.Puzzle.TryGetValue(child.name, out var dependent)) Assert.IsTrue(dependent.Surface.activeSelf);
        yield return null;
        LogAssert.NoUnexpectedReceived();
    }
}

public class PlacementAnchorGeometryTests
{
    [UnityTest]
    public IEnumerator HollowAndOccludingSurfacesUseAnchorPlaneAndStillRequireAPuzzleHit()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.transform.position = new Vector3(0f, -.5f, 0f);
        ground.transform.localScale = new Vector3(30f, .1f, 30f);
        var ray = new Ray(new Vector3(0f, 4f, -4f), new Vector3(0f, -2f, 4f));
        using (var projection = new PuzzleDragRaycaster(new[] { ground }))
        {
            Assert.IsTrue(projection.Raycast(ray, 100f, out var original));
            Assert.Less(original.point.y, 0f);
            Assert.Greater(Mathf.Abs(original.point.z), 4f);
            Assert.IsTrue(projection.Raycast(ray, 100f, out var supported, 2f));
            Assert.That(Vector3.Distance(supported.point, new Vector3(0f, 2f, 0f)), Is.LessThan(.0001f));
            Assert.IsTrue(projection.Raycast(ray, 100f, out var nearer, -2f));
            Assert.That(nearer.point.y, Is.EqualTo(-2f).Within(.0001f), "An occluding surface must not redirect the piece's XZ anchor");
            ground.SetActive(false);
            Assert.IsFalse(projection.Raycast(ray, 100f, out _, 2f), "Plane alone cannot create a hit outside the puzzle");
        }
        Object.Destroy(ground);
        yield return null;
    }

    [UnityTest]
    public IEnumerator OffsetPivotAndRotatedScaledRenderersUseCombinedBottomCenter()
    {
        var source = new GameObject("Offset pivot");
        source.transform.SetPositionAndRotation(new Vector3(2f, 7f, -3f), Quaternion.Euler(11f, 37f, -5f));
        source.transform.localScale = new Vector3(1.5f, .7f, 2f);
        var renderers = new List<Renderer>();
        for (int i = 0; i < 2; i++)
        {
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.transform.SetParent(source.transform, false);
            mesh.transform.localPosition = new Vector3(i * 2f, -4f, i);
            mesh.transform.localScale = new Vector3(.2f, 4f, .3f);
            renderers.Add(mesh.GetComponent<Renderer>());
        }
        Physics.SyncTransforms();
        Bounds bounds = renderers[0].bounds; bounds.Encapsulate(renderers[1].bounds);
        Vector3 expected = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        var placement = source.AddComponent<PuzzlePlacementAnchor>(); placement.Initialize();
        source.SetActive(false);
        Assert.That(Vector3.Distance(expected, placement.WorldPosition), Is.LessThan(.00001f));
        Vector3 pivot = source.transform.position;
        Assert.That(Vector3.Distance(pivot, placement.WorldPosition), Is.GreaterThan(1f));
        using (var preview = new PuzzleDragPreview(source))
        {
            var visual = preview.Root.GetComponentsInChildren<Renderer>(true);
            var rotations = visual.Select(r => r.transform.rotation).ToArray();
            foreach (Vector3 hit in new[] { new Vector3(4f, 0f, 5f), new Vector3(-6f, 2f, 3f) })
            {
                preview.Show(new RaycastHit { point = hit, normal = Vector3.forward });
                Assert.That(Vector3.Distance(hit, preview.AnchorWorldPosition), Is.LessThan(.00001f));
                for (int i = 0; i < visual.Length; i++) Assert.AreEqual(rotations[i], visual[i].transform.rotation);
                Assert.AreEqual(pivot, source.transform.position);
            }
        }
        Object.Destroy(source);
        yield return null;
    }
}
