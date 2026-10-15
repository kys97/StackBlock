using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public partial class FullFlowTests
{
    private static Vector2 JustOutsidePieceList(BlockUI piece)
    {
        var area = (RectTransform)typeof(BlockUI).GetField("pieceListArea", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(piece);
        var corners = new Vector3[4]; area.GetWorldCorners(corners);
        float left = corners.Min(p => p.x), right = corners.Max(p => p.x);
        // The saved scene has its list on the right. Exit toward the puzzle,
        // rather than beyond the screen; support a list moved to either side.
        float x = (left + right) * .5f > Camera.main.pixelRect.center.x ? left - 1f : right + 1f;
        return new Vector2(x, (corners[0].y + corners[1].y) * .5f);
    }

    [UnityTest]
    public IEnumerator ListExitShows3DBeforeBoardAndNeverPlacesFloatingPreview(
        [Values("Spring", "Summer", "Desert", "Fall", "Winter", "Bigben", "Egypt", "OperaHouse", "TowerBridge")] string stage)
    {
        yield return EnterPuzzle((int)System.Enum.Parse(typeof(GameManager.Stage), stage) < 5 ? "Weather" : "Structure", stage);
        var owner = GameManager.Instance;
        Object.FindAnyObjectByType<Timer>().enabled = false;
        var camera = Camera.main;
        var piece = owner.PieceContent.GetComponentsInChildren<BlockUI>().First(p =>
            owner.CanPlacePiece(PieceKey(p), owner.Puzzle[PieceKey(p)].Placement.WorldPosition));
        var target = owner.Puzzle[PieceKey(piece)];
        var original = target.Object.transform.localToWorldMatrix;
        var pointer = new PointerEventData(EventSystem.current) { position = ListPosition(owner) };
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        Assert.IsNull(Preview(piece)); Assert.IsTrue(DragImage(piece).enabled);

        pointer.position = JustOutsidePieceList(piece);
        using (var raycaster = new PuzzleDragRaycaster(new[] { owner.Ground }.Concat(owner.Puzzle.Values.Select(p => p.Surface)).ToArray(), owner.Ground.GetComponent<MeshFilter>()))
            Assert.IsFalse(raycaster.Raycast(camera.ScreenPointToRay(pointer.position), camera.farClipPlane, out _, target.Placement.WorldPosition.y), "Boundary position must exercise a miss, not an on-board preview");
        piece.OnDrag(pointer);
        var preview = Preview(piece);
        Assert.IsNotNull(preview); Assert.IsTrue(preview.IsVisible); Assert.IsFalse(DragImage(piece).enabled);
        Assert.That(Vector2.Distance(camera.WorldToScreenPoint(preview.VisualAnchorWorldPosition), pointer.position), Is.LessThan(.1f));
        if (stage == "Egypt")
        {
            yield return null; yield return null;
            string capture = System.IO.Path.GetFullPath("../../Logs/preview-list-exit-egypt.png");
            if (System.IO.File.Exists(capture)) System.IO.File.Delete(capture);
            ScreenCapture.CaptureScreenshot(capture);
            yield return WaitFor(() => System.IO.File.Exists(capture), "Preview just outside the list");
        }
        Vector3 firstAnchor = preview.AnchorWorldPosition;
        pointer.position += Vector2.up * 15; piece.OnDrag(pointer);
        Assert.IsTrue(preview.IsVisible);
        Assert.That(Vector3.Distance(firstAnchor, preview.AnchorWorldPosition), Is.GreaterThan(.001f));
        Assert.That(Vector2.Distance(camera.WorldToScreenPoint(preview.VisualAnchorWorldPosition), pointer.position), Is.LessThan(.1f));
        Assert.AreEqual(original, target.Object.transform.localToWorldMatrix);

        // Even an extremely permissive existing snap rule must not authorize a
        // visual-only floating position. No asset/configuration file is edited.
        var snap = typeof(PuzzlePlacementAnchor).GetField("snapDistanceOverride", BindingFlags.Instance | BindingFlags.NonPublic);
        float previousSnap = (float)snap.GetValue(target.Placement);
        snap.SetValue(target.Placement, 100f);
        Assert.IsTrue(owner.CanPlacePiece(PieceKey(piece), preview.AnchorWorldPosition));
        yield return null;
        Assert.IsTrue(preview.Root.GetComponentsInChildren<Renderer>().All(r => !r.HasPropertyBlock()));
        piece.OnEndDrag(pointer);
        snap.SetValue(target.Placement, previousSnap);
        Assert.IsFalse(target.IsComplete); Assert.AreEqual(0, owner.Score);

        pointer.position = ListPosition(owner); piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        pointer.position = JustOutsidePieceList(piece); piece.OnDrag(pointer);
        preview = Preview(piece);
        Quaternion orientation = preview.Root.transform.rotation;
        pointer.position = ListPosition(owner); piece.OnDrag(pointer);
        Assert.IsFalse(preview.IsVisible); Assert.IsTrue(DragImage(piece).enabled);
        pointer.position = camera.WorldToScreenPoint(target.Placement.WorldPosition); piece.OnDrag(pointer);
        Assert.AreSame(preview, Preview(piece)); Assert.IsTrue(preview.IsVisible);
        Assert.That(Quaternion.Angle(orientation, preview.Root.transform.rotation), Is.LessThan(.01f));
        Assert.IsTrue(owner.CanPlacePiece(PieceKey(piece), preview.AnchorWorldPosition));
        piece.OnEndDrag(pointer);
        Assert.IsTrue(target.IsComplete); Assert.AreEqual(owner.CurrentStageData.PointsPerPiece, owner.Score);
        Assert.AreEqual(original, target.Object.transform.localToWorldMatrix);
    }

    [UnityTest]
    public IEnumerator ListExitDuringCameraTurnCreatesPreviewAndPauseCancels([Values(1, 10)] int speed)
    {
        yield return EnterPuzzle("Structure", "Egypt");
        var owner = GameManager.Instance;
        Object.FindAnyObjectByType<Timer>().enabled = false;
        GameSettings.SetCameraRotationSpeedLevel(speed);
        var camera = Camera.main.GetComponent<PuzzleCameraController>();
        var piece = owner.PieceContent.GetComponentsInChildren<BlockUI>().Single(p => PieceKey(p) == "Small_pyramid");
        var pointer = new PointerEventData(EventSystem.current) { position = ListPosition(owner) };
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        camera.RotateRight(); Assert.IsTrue(camera.IsRotating);
        pointer.position = JustOutsidePieceList(piece); piece.OnDrag(pointer);
        var preview = Preview(piece);
        Assert.IsNotNull(preview); Assert.IsTrue(preview.IsVisible);
        Quaternion relative = Quaternion.Inverse(Camera.main.transform.rotation) * preview.Root.transform.rotation;
        yield return WaitFor(() => !camera.IsRotating, "Camera turn after list exit");
        yield return null;
        Assert.IsTrue(preview.IsVisible);
        Assert.That(Quaternion.Angle(relative, Quaternion.Inverse(Camera.main.transform.rotation) * preview.Root.transform.rotation), Is.LessThan(.1f));
        owner.PauseGame(); Assert.IsNull(Preview(piece)); Assert.IsFalse(preview.IsVisible);
        piece.OnBeginDrag(pointer); Assert.IsNull(Preview(piece));
        owner.ResumeGame(); piece.OnEndDrag(pointer);
        Assert.AreEqual(0, owner.CompletedPieceCount);
        yield return null;
        Assert.IsNull(GameObject.Find("Puzzle Preview Occlusion"));
    }
}
