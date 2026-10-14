using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Sample after BlockUI.LateUpdate, rather than in a coroutine before LateUpdate.
[DefaultExecutionOrder(10000)]
public sealed class PreviewRotationFrameProbe : MonoBehaviour
{
    public Action Sample;
    private void LateUpdate() => Sample?.Invoke();
}

public partial class FullFlowTests
{
    [UnityTest]
    public IEnumerator PreviewCameraRelativeDropKeepsTargetTransformAndRewards()
    {
        yield return EnterPuzzle("Structure", "Bigben");
        var manager = GameManager.Instance;
        var timer = Object.FindAnyObjectByType<Timer>();
        timer.enabled = false;
        var piece = manager.PieceContent.GetComponentsInChildren<BlockUI>().First(p =>
            manager.CanPlacePiece(PieceKey(p), manager.Puzzle[PieceKey(p)].Placement.WorldPosition));
        var target = manager.Puzzle[PieceKey(piece)];
        var sourcePose = target.Object.transform.localToWorldMatrix;
        var controller = Camera.main.GetComponent<PuzzleCameraController>();
        controller.RotateRight();
        yield return WaitFor(() => !controller.IsRotating, "Start holding from another view");
        var pointer = new PointerEventData(EventSystem.current)
            { position = Camera.main.WorldToScreenPoint(target.Placement.WorldPosition) };
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        var preview = Preview(piece);
        Assert.IsNotNull(preview); Assert.IsTrue(preview.IsVisible);
        Assert.That(Quaternion.Angle(Quaternion.identity, preview.Root.transform.rotation), Is.GreaterThan(80f));
        Assert.IsFalse(manager.CanPlacePiece(PieceKey(piece), preview.AnchorWorldPosition));
        controller.RotateLeft();
        yield return WaitFor(() => !controller.IsRotating, "Return to valid placement direction");
        yield return null; yield return null;
        Assert.That(Quaternion.Angle(Quaternion.identity, preview.Root.transform.rotation), Is.LessThan(.1f), "Preview aligns with the target at its authored image view");
        Assert.IsTrue(manager.CanPlacePiece(PieceKey(piece), preview.AnchorWorldPosition));
        Assert.IsTrue(preview.Root.GetComponentsInChildren<Renderer>().All(r => r.HasPropertyBlock()), "Placement hint survives visual rotation");
        timer.enabled = true; // Restore the real completion-event subscription before Drop.
        timer.Clock.Start(); timer.Clock.Tick(10f);
        float remaining = timer.Clock.RemainingTime;
        int score = manager.Score, completed = manager.CompletedPieceCount;
        piece.OnEndDrag(pointer);
        Assert.IsTrue(target.IsComplete); Assert.IsNull(Preview(piece)); Assert.IsFalse(preview.IsVisible);
        Assert.AreEqual(sourcePose, target.Object.transform.localToWorldMatrix, "Do not copy the preview pose into the correct piece");
        Assert.AreEqual(completed + 1, manager.CompletedPieceCount);
        Assert.AreEqual(score + manager.CurrentStageData.PointsPerPiece, manager.Score);
        timer.SendMessage("Update");
        Assert.That(timer.Clock.RemainingTime, Is.EqualTo(remaining + manager.CurrentStageData.TimeBonusSeconds).Within(.001f));
        Assert.IsTrue(target.Object.GetComponentsInChildren<Renderer>(true).All(r => !r.HasPropertyBlock()));
    }

    [UnityTest]
    public IEnumerator PreviewMaintainsCameraRelativePoseEveryFrame(
        [Values("Spring", "Bigben")] string stage, [Values(1, 10)] int speed)
    {
        yield return EnterPuzzle(stage == "Spring" ? "Weather" : "Structure", stage);
        var manager = GameManager.Instance;
        Object.FindAnyObjectByType<Timer>().enabled = false;
        GameSettings.SetCameraRotationSpeedLevel(speed);
        string key = stage == "Spring" ? "Tree02" : "ClockTower";
        var target = manager.Puzzle[key];
        var sourcePose = target.Object.transform.localToWorldMatrix;
        var piece = manager.PieceContent.GetComponentsInChildren<BlockUI>().Single(p => PieceKey(p) == key);
        var camera = Camera.main;
        var controller = camera.GetComponent<PuzzleCameraController>();
        var pointer = new PointerEventData(EventSystem.current)
            { position = camera.WorldToScreenPoint(target.Placement.WorldPosition) };
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        var preview = Preview(piece);
        Assert.IsNotNull(preview); Assert.IsTrue(preview.IsVisible);
        var renderers = preview.Root.GetComponentsInChildren<Renderer>(true);
        var relative = renderers.Select(r => Quaternion.Inverse(camera.transform.rotation) * r.transform.rotation).ToArray();
        var children = preview.Root.GetComponentsInChildren<Transform>(true).Where(t => t != preview.Root.transform).ToArray();
        var localRotations = children.Select(t => t.localRotation).ToArray();
        var localScales = children.Select(t => t.localScale).ToArray();
        Vector3 anchor = preview.AnchorWorldPosition;
        float maxAngle = 0, maxAnchorError = 0;
        int movingSamples = 0;
        var probe = new GameObject("Preview frame probe").AddComponent<PreviewRotationFrameProbe>();
        probe.Sample = () =>
        {
            if (controller.IsRotating) movingSamples++;
            maxAnchorError = Mathf.Max(maxAnchorError, Vector3.Distance(anchor, preview.AnchorWorldPosition));
            for (int i = 0; i < renderers.Length; i++)
                maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(relative[i],
                    Quaternion.Inverse(camera.transform.rotation) * renderers[i].transform.rotation));
        };
        try
        {
            // Real Update animation, all keyboard routes, buttons, repeated turns and a full orbit.
            for (int turn = 0; turn < 12; turn++)
            {
                if (turn == 6)
                {
                    pointer.position = ListPosition(manager); piece.OnDrag(pointer);
                    Assert.IsFalse(preview.IsVisible);
                }
                int before = movingSamples;
                if (turn < 4)
                {
                    KeyCode input = new[] { KeyCode.A, KeyCode.D, KeyCode.LeftArrow, KeyCode.RightArrow }[turn];
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    var field = typeof(PuzzleCameraController).GetField("getKeyDown", flags);
                    bool consumed = false;
                    field.SetValue(controller, new Func<KeyCode, bool>(k =>
                    {
                        if (consumed || k != input) return false;
                        consumed = true; return true;
                    }));
                    yield return WaitFor(() => consumed, "Keyboard request in real Update");
                    field.SetValue(controller, new Func<KeyCode, bool>(Input.GetKeyDown));
                }
                else Click(turn == 4 || turn == 5 ? "RotateLeft" : "RotateRight");
                yield return WaitFor(() => !controller.IsRotating, "Camera animation finishes");
                yield return null; yield return null;
                Assert.Greater(movingSamples, before, "Must sample intermediate animation frames at speed " + speed);
                Assert.That(maxAngle, Is.LessThan(.1f), "Camera-relative orientation drift at turn " + turn);
                Assert.That(maxAnchorError, Is.LessThan(.00001f), "Rotation must not move the placement anchor");
                Assert.AreEqual(sourcePose, target.Object.transform.localToWorldMatrix);
                for (int i = 0; i < children.Length; i++)
                {
                    Assert.AreEqual(localRotations[i], children[i].localRotation);
                    Assert.AreEqual(localScales[i], children[i].localScale);
                }
                if (turn == 7)
                {
                    pointer.position = camera.WorldToScreenPoint(target.Placement.WorldPosition); piece.OnDrag(pointer);
                    Assert.AreSame(preview, Preview(piece)); Assert.IsTrue(preview.IsVisible);
                    anchor = preview.AnchorWorldPosition;
                }
            }
            Debug.Log($"Camera-relative preview {stage} speed {speed}: {movingSamples} moving frames; max angle {maxAngle}; anchor error {maxAnchorError}");
            manager.PauseGame();
            probe.Sample = null;
            Assert.IsFalse(preview.IsVisible); Assert.IsNull(Preview(piece));
            manager.ResumeGame(); piece.OnEndDrag(pointer);
            Assert.IsFalse(target.IsComplete);
            // A new drag restores the image's authored view, independent of previous sessions.
            pointer.position = camera.WorldToScreenPoint(target.Placement.WorldPosition);
            piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
            var next = Preview(piece);
            Assert.IsNotNull(next); Assert.AreNotSame(preview, next);
            Quaternion expectedRoot = camera.transform.rotation * Quaternion.Inverse(controller.GetDirectionRotation(target.Direction));
            Assert.That(Quaternion.Angle(expectedRoot, next.Root.transform.rotation), Is.LessThan(.1f));
            pointer.position = Vector2.one * -100f; piece.OnEndDrag(pointer);
        }
        finally { probe.Sample = null; Object.Destroy(probe.gameObject); }
    }
}
