using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public partial class FullFlowTests
{
    [UnityTest]
    public IEnumerator EveryStageRejectsInvalidDropsRewardsOnceAndRestarts(
        [Values("Spring", "Summer", "Desert", "Fall", "Winter", "Bigben", "Egypt", "OperaHouse", "TowerBridge")] string stage)
    {
        string topic = (int)Enum.Parse(typeof(GameManager.Stage), stage) < 5 ? "Weather" : "Structure";
        yield return EnterPuzzle(topic, stage);
        GameManager manager = GameManager.Instance;
        Timer timer = Object.FindFirstObjectByType<Timer>();
        PuzzleCameraController camera = Object.FindFirstObjectByType<PuzzleCameraController>();
        BlockUI piece = manager.PieceContent.GetComponentsInChildren<BlockUI>().First(ui =>
        {
            var block = manager.Puzzle[PieceKey(ui)];
            return block.Direction == manager.CurrentDirection && block.Surface.activeInHierarchy;
        });
        string key = PieceKey(piece);
        var target = manager.Puzzle[key];
        var pointer = new PointerEventData(EventSystem.current) { position = target.ScreenPosition + Vector2.right * (manager.CurrentStageData.SnapDistancePixels + 10f) };
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer); piece.OnEndDrag(pointer);
        Assert.AreEqual(0, manager.CompletedPieceCount, stage + ": invalid distance");
        Assert.AreEqual(0, manager.Score);
        yield return null;

        // Same coordinates cannot place a piece from a different direction.
        camera.RotateRight();
        yield return WaitFor(() => !camera.IsRotating, "Wrong direction rotation");
        pointer.position = target.ScreenPosition;
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer); piece.OnEndDrag(pointer);
        Assert.AreEqual(0, manager.CompletedPieceCount, stage + ": wrong direction");
        camera.RotateLeft();
        yield return WaitFor(() => !camera.IsRotating, "Return to starting direction");
        Assert.That(Vector2.Distance(target.ScreenPosition, camera.ControlledCamera.WorldToScreenPoint(target.Object.transform.position)), Is.LessThan(0.01f));

        timer.Clock.Start(); timer.Clock.Tick(10f);
        float before = timer.Clock.RemainingTime;
        pointer.position = target.ScreenPosition;
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer); piece.OnEndDrag(pointer);
        piece.OnEndDrag(pointer);
        Assert.IsFalse(manager.TryCompletePiece(key, pointer.position), "Duplicate completion rejected");
        timer.SendMessage("Update");
        Assert.AreEqual(1, manager.CompletedPieceCount);
        Assert.AreEqual(manager.CurrentStageData.PointsPerPiece, manager.Score);
        Assert.That(timer.Clock.RemainingTime, Is.EqualTo(Mathf.Min(timer.Clock.TimeLimit, before + manager.CurrentStageData.TimeBonusSeconds)).Within(0.001f));
        foreach (Transform child in target.Object.transform)
            Assert.IsTrue(manager.Puzzle[child.name].Surface.activeSelf, "Dependent surface: " + child.name);
        yield return null;

        // Expire while a drag is in flight; release cannot grant points or reopen rotation.
        BlockUI remaining = manager.PieceContent.GetComponentsInChildren<BlockUI>().First();
        remaining.OnBeginDrag(pointer);
        timer.Clock.Tick(timer.Clock.TimeLimit + 1f);
        remaining.OnDrag(pointer); remaining.OnEndDrag(pointer);
        int direction = camera.CurrentDirection;
        camera.RotateLeft(); camera.RotateRight();
        Assert.AreEqual(direction, camera.CurrentDirection);
        Assert.AreEqual(1, manager.CompletedPieceCount);
        Assert.IsTrue(timer.FailurePanel.activeSelf);
        Assert.IsFalse(manager.CanAcceptInput);
        Assert.AreEqual(0f, timer.Clock.RemainingTime);

        Click("TryAgain");
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "Puzzle" && GameManager.Instance.CompletedPieceCount == 0 && GameManager.Instance.HasStarted, "Retry " + stage);
        manager = GameManager.Instance;
        Assert.AreEqual(stage, manager.Session.Stage.ToString());
        yield return PlaceAvailableBlocks();
        camera = Object.FindFirstObjectByType<PuzzleCameraController>();
        direction = camera.CurrentDirection;
        int score = manager.Score;
        camera.RotateLeft(); camera.RotateRight();
        Assert.AreEqual(direction, camera.CurrentDirection);
        Assert.IsFalse(manager.TryCompletePiece(key, pointer.position));
        Assert.AreEqual(score, manager.Score);

        int index = (int)manager.Session.Stage;
        int expected = index == 4 || index == 8 ? index : index + 1;
        Click("NextStage");
        yield return WaitFor(() => GameManager.Instance.HasStarted && !GameManager.Instance.IsSuccessful, "Next stage from " + stage);
        Assert.AreEqual(expected, (int)manager.Session.Stage);
        Assert.AreEqual(0, manager.CompletedPieceCount);
    }

    private static string PieceKey(BlockUI piece) => (string)typeof(BlockUI)
        .GetField("key", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(piece);
}
