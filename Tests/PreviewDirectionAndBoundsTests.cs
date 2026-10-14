using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public partial class FullFlowTests
{
    [UnityTest]
    public IEnumerator EgyptGroundMatchesCompleteModelAndPyramidBaseHeight()
    {
        yield return EnterPuzzle("Structure", "Egypt");
        var manager = GameManager.Instance;
        Object.FindAnyObjectByType<Timer>().enabled = false;
        var ground = manager.Ground.GetComponent<MeshFilter>();
        var authored = Resources.Load<GameObject>("Complete/Egypt").GetComponentsInChildren<MeshFilter>(true)
            .Single(f => f.sharedMesh == ground.sharedMesh);
        Assert.AreEqual(authored.transform.localPosition, ground.transform.localPosition,
            "Keep the Ground mesh at the same authored height as the complete model");
        using (var raycaster = new PuzzleDragRaycaster(new[] { manager.Ground }))
            foreach (string key in new[] { "Big_pyramid", "Small_pyramid" })
            {
                Vector3 anchor = manager.Puzzle[key].Placement.WorldPosition;
                Assert.IsTrue(raycaster.Raycast(new Ray(anchor + Vector3.up * 10, Vector3.down), 20, out var hit));
                Assert.That(hit.point.y, Is.EqualTo(anchor.y).Within(.001f), key + " should rest on the board, not inside it");
            }
    }

    [UnityTest]
    public IEnumerator ImageSuffixPreviewMatchesEachAuthoredViewFromEveryCameraSide()
    {
        yield return EnterPuzzle("Structure", "Egypt");
        var manager = GameManager.Instance;
        Object.FindAnyObjectByType<Timer>().enabled = false;
        var camera = Camera.main;
        var controller = camera.GetComponent<PuzzleCameraController>();
        // Oracle: observe the actual navigation poses, independently of preview math.
        var imageViews = new Quaternion[4];
        for (int direction = 0; direction < 4; direction++)
        {
            imageViews[controller.CurrentDirection] = camera.transform.rotation;
            controller.RotateRight();
            yield return WaitFor(() => !controller.IsRotating, "Record authored image view");
        }
        var pieces = manager.PieceContent.GetComponentsInChildren<BlockUI>()
            .GroupBy(p => manager.Puzzle[PieceKey(p)].Direction).Select(g => g.First()).ToArray();
        Assert.AreEqual(4, pieces.Length, "Exercise F, R, B and L image suffixes");
        for (int cameraSide = 0; cameraSide < 4; cameraSide++)
        {
            foreach (var piece in pieces)
            {
                var target = manager.Puzzle[PieceKey(piece)];
                var sourceMatrix = target.Object.transform.localToWorldMatrix;
                var pointer = new PointerEventData(EventSystem.current)
                    { position = camera.WorldToScreenPoint(target.Placement.WorldPosition) };
                piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
                var preview = Preview(piece);
                Assert.IsNotNull(preview, target.Image.name); Assert.IsTrue(preview.IsVisible);
                var sourceMeshes = target.Object.GetComponentsInChildren<MeshFilter>(true);
                var copies = preview.Root.GetComponentsInChildren<MeshFilter>(true);
                Assert.AreEqual(sourceMeshes.Length, copies.Length);
                for (int mesh = 0; mesh < sourceMeshes.Length; mesh++)
                    foreach (var basis in new[] { Vector3.right, Vector3.up, Vector3.forward })
                    {
                        Vector3 expected = Quaternion.Inverse(imageViews[target.Direction]) * sourceMeshes[mesh].transform.TransformVector(basis);
                        Vector3 actual = Quaternion.Inverse(camera.transform.rotation) * copies[mesh].transform.TransformVector(basis);
                        Assert.That(Vector3.Distance(expected, actual), Is.LessThan(.00001f), target.Image.name + " at camera side " + cameraSide);
                    }
                if (cameraSide == 0)
                {
                    yield return null; yield return null;
                    string path = System.IO.Path.GetFullPath("../../Logs/direction-preview-" + target.Image.name + ".png");
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    ScreenCapture.CaptureScreenshot(path);
                    yield return WaitFor(() => System.IO.File.Exists(path), "Image suffix preview capture");
                }
                Quaternion pose = preview.Root.transform.rotation;
                pointer.position = ListPosition(manager); piece.OnDrag(pointer);
                Assert.IsFalse(preview.IsVisible);
                pointer.position = camera.WorldToScreenPoint(target.Placement.WorldPosition); piece.OnDrag(pointer);
                Assert.AreSame(preview, Preview(piece)); Assert.IsTrue(preview.IsVisible);
                Assert.That(Quaternion.Angle(pose, preview.Root.transform.rotation), Is.LessThan(.1f));
                Assert.AreEqual(sourceMatrix, target.Object.transform.localToWorldMatrix);
                pointer.position = Vector2.one * -100f; piece.OnEndDrag(pointer);
            }
            controller.RotateRight();
            yield return WaitFor(() => !controller.IsRotating, "Next initial camera side");
        }
    }

    [UnityTest]
    public IEnumerator EgyptPyramidRejectsBackgroundProjectionAndReturnsToBoard()
    {
        yield return EnterPuzzle("Structure", "Egypt");
        var manager = GameManager.Instance;
        Object.FindAnyObjectByType<Timer>().enabled = false;
        var camera = Camera.main;
        var controller = camera.GetComponent<PuzzleCameraController>();
        foreach (string key in new[] { "Big_pyramid", "Small_pyramid" })
        {
            var target = manager.Puzzle[key];
            var original = target.Object.transform.localToWorldMatrix;
            var piece = manager.PieceContent.GetComponentsInChildren<BlockUI>().Single(p => PieceKey(p) == key);
            for (int side = 0; side < 4; side++)
            {
                // Original reproduction: this pointer used to create a pyramid at |XZ| = 1.55,
                // outside even the board's world AABB (extents about .84).
                var pointer = new PointerEventData(EventSystem.current)
                    { position = new Vector2(Screen.width * .55f, Screen.height * .8f) };
                piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
                Assert.IsTrue(Preview(piece) == null || !Preview(piece).IsVisible, key + " off-board side " + side);
                Assert.IsTrue(DragImage(piece).enabled);
                if (key == "Big_pyramid" && side == 0)
                {
                    yield return null; yield return null;
                    string path = System.IO.Path.GetFullPath("../../Logs/egypt-drag-behind-fixed.png");
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    ScreenCapture.CaptureScreenshot(path);
                    yield return WaitFor(() => System.IO.File.Exists(path), "Off-board fallback capture");
                }
                pointer.position = camera.WorldToScreenPoint(target.Placement.WorldPosition); piece.OnDrag(pointer);
                var preview = Preview(piece);
                Assert.IsNotNull(preview); Assert.IsTrue(preview.IsVisible);
                Assert.That(Vector3.Distance(preview.AnchorWorldPosition, target.Placement.WorldPosition), Is.LessThan(.001f));
                pointer.position = new Vector2(Screen.width * .55f, Screen.height * .8f); piece.OnDrag(pointer);
                Assert.IsFalse(preview.IsVisible); Assert.IsTrue(DragImage(piece).enabled);
                piece.OnEndDrag(pointer);
                Assert.IsFalse(target.IsComplete); Assert.AreEqual(original, target.Object.transform.localToWorldMatrix);
                Assert.AreEqual(0, manager.Score);
                controller.RotateRight();
                yield return WaitFor(() => !controller.IsRotating, "Egypt boundary direction");
            }
        }
    }
}

public class PreviewPlayAreaGeometryTests
{
    [UnityTest]
    public IEnumerator RotatedBoardBoundsExcludeBackgroundWithoutClampingAnchorHeight()
    {
        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.transform.localScale = new Vector3(2f, .2f, 2f);
        board.transform.rotation = Quaternion.Euler(0, 45, 0);
        var background = GameObject.CreatePrimitive(PrimitiveType.Cube);
        background.transform.SetParent(board.transform, false);
        background.transform.position = new Vector3(0, -3, 0);
        background.transform.localScale = new Vector3(30, 1, 30);
        using (var raycaster = new PuzzleDragRaycaster(new[] { board }, board.GetComponent<MeshFilter>()))
        {
            var center = new Ray(new Vector3(0, 20, 0), Vector3.down);
            Assert.IsTrue(raycaster.Raycast(center, 100f, out var hit, 2f));
            Assert.That(hit.point.y, Is.EqualTo(2f).Within(.00001f));
            var outside = new Ray(new Vector3(1.2f, 20, 1.2f), Vector3.down);
            Assert.IsTrue(raycaster.Raycast(outside, 100f, out _), "The background collider is still available");
            Assert.IsFalse(raycaster.Raycast(outside, 100f, out _, 2f), "Reject oriented-bounds corner outside the board, even inside its world AABB");
        }
        Object.Destroy(board);
        yield return null;
    }
}
