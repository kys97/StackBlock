using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public partial class FullFlowTests
{
    private static PuzzleDragPreview Preview(BlockUI piece) => (PuzzleDragPreview)typeof(BlockUI)
        .GetField("dragPreview", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(piece);
    private static Image DragImage(BlockUI piece) => (Image)typeof(BlockUI)
        .GetField("dragImage", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(piece);
    private static Vector2 ListPosition(GameManager manager)
    {
        var area = (RectTransform)manager.PieceContent.transform.parent;
        return RectTransformUtility.WorldToScreenPoint(null, area.TransformPoint(area.rect.center));
    }

    [UnityTest]
    public IEnumerator DragPreviewTransitionsAndPreservesPlacementAcrossStages(
        [Values("Spring", "Summer", "Desert", "Fall", "Winter", "Bigben", "Egypt", "OperaHouse", "TowerBridge")] string stage)
    {
        yield return EnterPuzzle((int)System.Enum.Parse(typeof(GameManager.Stage), stage) < 5 ? "Weather" : "Structure", stage);
        var manager = GameManager.Instance;
        var piece = manager.PieceContent.GetComponentsInChildren<BlockUI>().First(ui =>
            manager.Puzzle[PieceKey(ui)].Direction == manager.CurrentDirection && manager.Puzzle[PieceKey(ui)].Surface.activeInHierarchy);
        var target = manager.Puzzle[PieceKey(piece)];
        Vector3 position = target.Object.transform.position;
        Quaternion rotation = target.Object.transform.rotation;
        Vector3 scale = target.Object.transform.lossyScale;
        Transform parent = target.Object.transform.parent;
        var pointer = new PointerEventData(EventSystem.current) { position = ListPosition(manager) };
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        Assert.IsTrue(DragImage(piece).enabled);
        Assert.AreEqual(piece.transform.localScale, DragImage(piece).transform.localScale);
        Assert.IsNull(Preview(piece), "No mesh/material copy needed while still over the list");

        pointer.position = target.ScreenPosition;
        piece.OnDrag(pointer);
        var preview = Preview(piece);
        Assert.IsNotNull(preview, stage + ": Ground ray must hit at a valid piece position");
        Assert.IsTrue(preview.IsVisible); Assert.IsFalse(DragImage(piece).enabled);
        Assert.IsEmpty(preview.Root.GetComponentsInChildren<Collider>());
        Assert.IsEmpty(preview.Root.GetComponentsInChildren<MonoBehaviour>());
        Assert.AreEqual(position, target.Object.transform.position);
        Assert.AreEqual(rotation, target.Object.transform.rotation);
        Assert.AreEqual(scale, target.Object.transform.lossyScale);
        Assert.AreSame(parent, target.Object.transform.parent);
        Assert.IsFalse(target.Object.activeSelf);

        var sourceFilter = target.Object.GetComponentInChildren<MeshFilter>(true);
        var previewFilter = preview.Root.GetComponentsInChildren<MeshFilter>().Single(f => f.sharedMesh == sourceFilter.sharedMesh);
        foreach (Vector3 basis in new[] { Vector3.right, Vector3.up, Vector3.forward })
            Assert.That(Vector3.Distance(sourceFilter.transform.TransformVector(basis), previewFilter.transform.TransformVector(basis)), Is.LessThan(.00001f));
        var originalMaterials = sourceFilter.GetComponent<Renderer>().sharedMaterials;
        var previewMaterials = previewFilter.GetComponent<Renderer>().sharedMaterials;
        Assert.AreEqual(originalMaterials.Length, previewMaterials.Length);
        for (int i = 0; i < originalMaterials.Length; i++)
        {
            Assert.AreNotSame(originalMaterials[i], previewMaterials[i]);
            Assert.AreEqual("StackBlock/Puzzle Drag Preview", previewMaterials[i].shader.name);
            Assert.That(previewMaterials[i].GetFloat("_Opacity"), Is.EqualTo(.55f));
            Assert.AreEqual(originalMaterials[i].color, previewMaterials[i].color);
        }

        pointer.position = ListPosition(manager);
        piece.OnDrag(pointer);
        Assert.IsFalse(preview.IsVisible); Assert.IsTrue(DragImage(piece).enabled);
        pointer.position = target.ScreenPosition; piece.OnDrag(pointer);
        Assert.AreSame(preview, Preview(piece), "Crossing list boundaries reuses the visual copy");
        var timer = Object.FindAnyObjectByType<Timer>();
        timer.Clock.Start(); timer.Clock.Tick(10f);
        float remaining = timer.Clock.RemainingTime;
        piece.OnEndDrag(pointer);
        Assert.IsFalse(preview.IsVisible); Assert.IsNull(Preview(piece));
        Assert.IsTrue(target.IsComplete); Assert.IsTrue(target.Object.activeSelf);
        Assert.AreEqual(1, manager.CompletedPieceCount); Assert.AreEqual(manager.CurrentStageData.PointsPerPiece, manager.Score);
        timer.SendMessage("Update");
        Assert.That(timer.Clock.RemainingTime, Is.EqualTo(remaining + manager.CurrentStageData.TimeBonusSeconds).Within(.001f));
        Assert.IsFalse(manager.TryCompletePiece(PieceKey(piece), target.Placement.WorldPosition));
        Assert.AreEqual(position, target.Object.transform.position);
        yield return null;
        Assert.IsTrue(preview.Root == null);
        Assert.IsTrue(previewMaterials.All(m => m == null), "Only transient materials are destroyed");
        Assert.IsTrue(originalMaterials.All(m => m != null));
    }

    [UnityTest]
    public IEnumerator DragPreviewWrongDropPauseExpiryAndSceneExitCleanUp()
    {
        yield return EnterPuzzle("Weather", "Spring");
        var manager = GameManager.Instance;
        var piece = manager.PieceContent.GetComponentsInChildren<BlockUI>().First(ui =>
            manager.Puzzle[PieceKey(ui)].Direction == manager.CurrentDirection && manager.Puzzle[PieceKey(ui)].Surface.activeInHierarchy);
        var target = manager.Puzzle[PieceKey(piece)];
        Vector3 originalUI = piece.transform.localPosition;
        var pointer = new PointerEventData(EventSystem.current) { position = target.ScreenPosition };
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        var wrongPreview = Preview(piece);
        pointer.position = Camera.main.WorldToScreenPoint(target.Placement.WorldPosition
            + Vector3.right * (manager.CurrentStageData.SnapDistanceWorld + .3f));
        piece.OnDrag(pointer); piece.OnEndDrag(pointer);
        Assert.IsFalse(wrongPreview.IsVisible); Assert.IsFalse(target.IsComplete); Assert.IsFalse(target.Object.activeSelf);
        Assert.AreEqual(originalUI, piece.transform.localPosition); Assert.AreEqual(0, manager.Score);

        pointer.position = target.ScreenPosition;
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        var pausedPreview = Preview(piece);
        manager.PauseGame();
        Assert.IsFalse(pausedPreview.IsVisible); Assert.IsNull(Preview(piece)); Assert.IsNull(DragImage(piece));
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer); Assert.IsNull(Preview(piece));
        manager.ResumeGame(); piece.OnEndDrag(pointer);
        Assert.IsFalse(target.IsComplete); Assert.AreEqual(originalUI, piece.transform.localPosition);
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        var expiredPreview = Preview(piece);
        var timer = Object.FindAnyObjectByType<Timer>();
        timer.Clock.Start(); timer.Clock.Tick(timer.Clock.TimeLimit);
        yield return null; // No more pointer events: LateUpdate must still cancel.
        Assert.IsFalse(expiredPreview.IsVisible); Assert.IsNull(Preview(piece));
        piece.OnEndDrag(pointer); Assert.IsFalse(target.IsComplete);

        Click("TryAgain"); yield return null;
        yield return WaitFor(() => manager.HasStarted, "Retry after preview expiry");
        piece = manager.PieceContent.GetComponentsInChildren<BlockUI>().First(ui =>
            manager.Puzzle[PieceKey(ui)].Direction == manager.CurrentDirection && manager.Puzzle[PieceKey(ui)].Surface.activeInHierarchy);
        pointer.position = manager.Puzzle[PieceKey(piece)].ScreenPosition;
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        var leavingPreview = Preview(piece);
        Object.FindAnyObjectByType<UI>().ToStage();
        yield return null;
        Assert.IsTrue(leavingPreview.Root == null);
    }

    [UnityTest]
    public IEnumerator DragPreviewUsesRealSizesAndPerspectiveWithoutMutatingMaterials()
    {
        yield return EnterPuzzle("Weather", "Spring");
        var manager = GameManager.Instance;
        var timer = Object.FindAnyObjectByType<Timer>();
        timer.enabled = false;
        var filters = manager.Puzzle.Values.Select(p => p.Object.GetComponentInChildren<MeshFilter>(true)).ToArray();
        var ordered = filters.OrderBy(f => f.transform.TransformVector(f.sharedMesh.bounds.size).magnitude).ToArray();
        float smallest = 0, largest = 0;
        foreach (var filter in new[] { ordered.First(), ordered.Last() })
        {
            var block = manager.Puzzle.First(p => p.Value.Object.GetComponentInChildren<MeshFilter>(true) == filter);
            var piece = manager.PieceContent.GetComponentsInChildren<BlockUI>().Single(p => PieceKey(p) == block.Key);
            Vector3 boardCenter = manager.Ground.GetComponent<Renderer>().bounds.center;
            boardCenter.y = block.Value.Placement.WorldPosition.y;
            Vector3 depthOffset = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized * .2f;
            var pointer = new PointerEventData(EventSystem.current)
                { position = Camera.main.WorldToScreenPoint(boardCenter - depthOffset) };
            var originals = filter.GetComponent<Renderer>().sharedMaterials;
            var colors = originals.Select(m => m.color).ToArray();
            var shaders = originals.Select(m => m.shader).ToArray();
            piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
            var renderer = Preview(piece).Root.GetComponentInChildren<Renderer>();
            float size = renderer.bounds.size.magnitude;
            if (filter == ordered.First()) smallest = size; else largest = size;
            if (filter == ordered.Last())
            {
                var capture = System.IO.Path.GetFullPath("../../Logs/drag-preview-world-surface.png");
                if (System.IO.File.Exists(capture)) System.IO.File.Delete(capture);
                yield return null;
                yield return null;
                Debug.Log("Preview source=" + block.Key + "; bounds=" + renderer.bounds + "; screen=" + Camera.main.WorldToScreenPoint(renderer.bounds.center));
                ScreenCapture.CaptureScreenshot(capture);
                yield return WaitFor(() => System.IO.File.Exists(capture), "Preview screenshot");
                Assert.IsFalse(Camera.main.orthographic);
                float firstWidth = ProjectedWidth(renderer.bounds, Camera.main);
                float firstDepth = Camera.main.WorldToViewportPoint(renderer.bounds.center).z;
                pointer.position = Camera.main.WorldToScreenPoint(boardCenter + depthOffset);
                piece.OnDrag(pointer);
                Assert.IsTrue(Preview(piece).IsVisible);
                float nextWidth = ProjectedWidth(renderer.bounds, Camera.main);
                float nextDepth = Camera.main.WorldToViewportPoint(renderer.bounds.center).z;
                Assert.That(Mathf.Abs(firstDepth - nextDepth), Is.GreaterThan(.05f));
                Assert.AreEqual(firstDepth < nextDepth, firstWidth > nextWidth, "Closer world geometry appears larger");
            }
            pointer.position = ListPosition(manager); piece.OnDrag(pointer); piece.OnEndDrag(pointer);
            CollectionAssert.AreEqual(colors, originals.Select(m => m.color));
            CollectionAssert.AreEqual(shaders, originals.Select(m => m.shader));
            Assert.IsFalse(block.Value.IsComplete);
            yield return null;
        }
        Assert.That(largest, Is.GreaterThan(smallest * 1.5f), "Source piece sizes are not normalized to the UI thumbnail");
    }

    private static float ProjectedWidth(Bounds bounds, Camera camera)
    {
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            float x = camera.WorldToScreenPoint(corner).x;
            min = Mathf.Min(min, x); max = Mathf.Max(max, x);
        }
        return max - min;
    }

    [UnityTest]
    public IEnumerator DragPreviewAnchorStaysFixedWithStationaryPointerAndHandlesMisses()
    {
        yield return EnterPuzzle("Weather", "Spring");
        var manager = GameManager.Instance;
        Object.FindAnyObjectByType<Timer>().enabled = false;
        var piece = manager.PieceContent.GetComponentsInChildren<BlockUI>().First();
        var target = manager.Puzzle[PieceKey(piece)];
        var original = target.Object.transform.localToWorldMatrix;
        var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width * .45f, Screen.height * .45f) };
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        var preview = Preview(piece);
        var camera = Camera.main.GetComponent<PuzzleCameraController>();
        for (int i = 0; i < 4; i++)
        {
            var position = preview.AnchorWorldPosition;
            camera.RotateRight();
            yield return WaitFor(() => !camera.IsRotating, "Preview during camera rotation");
            yield return null;
            Assert.IsTrue(preview.IsVisible);
            Assert.That(Vector3.Distance(position, preview.AnchorWorldPosition), Is.LessThan(.00001f));
            Assert.AreEqual(original, target.Object.transform.localToWorldMatrix);
        }
        pointer.position = new Vector2(-100, -100); piece.OnDrag(pointer);
        Assert.IsFalse(preview.IsVisible); Assert.IsTrue(DragImage(piece).enabled);
        piece.OnEndDrag(pointer);
        yield return null;
        Assert.IsNull(GameObject.Find("Puzzle Drag Raycast Surfaces"));
        Assert.IsTrue(preview.Root == null);
    }
}

public class DragPreviewGeometryTests
{
    [UnityTest]
    public IEnumerator CopiesNestedNonuniformTransformsWithoutSourceBehaviourOrMaterialChanges()
    {
        var parent = new GameObject("Scaled parent");
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.transform.SetParent(parent.transform, false);
        parent.transform.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(17, 43, 9));
        parent.transform.localScale = new Vector3(2, .7f, 3);
        source.transform.localRotation = Quaternion.Euler(12, 60, 7);
        source.transform.localScale = new Vector3(.3f, 4, .2f);
        source.AddComponent<Surface>();
        source.SetActive(false);
        Material original = source.GetComponent<Renderer>().sharedMaterial;
        var preview = new PuzzleDragPreview(source);
        var copy = preview.Root.GetComponentInChildren<MeshFilter>(true);
        Assert.AreSame(source.GetComponent<MeshFilter>().sharedMesh, copy.sharedMesh);
        for (int i = 0; i < 16; i++)
            Assert.That(copy.transform.localToWorldMatrix[i], Is.EqualTo(source.transform.localToWorldMatrix[i]).Within(.00001f));
        Assert.IsEmpty(preview.Root.GetComponentsInChildren<Collider>(true));
        Assert.IsEmpty(preview.Root.GetComponentsInChildren<MonoBehaviour>(true));
        Assert.IsFalse(source.activeSelf); Assert.AreSame(parent.transform, source.transform.parent);
        var material = copy.GetComponent<Renderer>().sharedMaterial;
        Assert.AreNotSame(original, material);
        preview.Dispose(); Object.Destroy(parent);
        yield return null;
        Assert.IsTrue(material == null); Assert.IsTrue(original != null);
    }
}
