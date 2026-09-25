using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public partial class FullFlowTests
{
    private static void Escape(PuzzlePauseUI view)
    {
        var input = typeof(PuzzlePauseUI).GetField("getKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
        input.SetValue(view, new Func<KeyCode, bool>(key => key == KeyCode.Escape));
        view.SendMessage("Update");
        input.SetValue(view, new Func<KeyCode, bool>(_ => false));
    }

    private static void PointerClick(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.IsNotEmpty(hits);
        var receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Assert.AreEqual(button.gameObject, receiver, "Pause UI must receive pointer clicks above the gameplay UI; hits="
            + string.Join(", ", hits.Select(h => h.gameObject.name + ":" + h.depth))
            + "; pointer=" + pointer.position + "; rect=" + rect.rect + "; active=" + button.gameObject.activeInHierarchy);
        ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
    }

    [UnityTest]
    public IEnumerator PauseFreezesTimeCameraAndDragThenResumesWithoutStaleDrop()
    {
        yield return EnterPuzzle("Weather", "Spring");
        var manager = GameManager.Instance;
        var view = Object.FindAnyObjectByType<PuzzlePauseUI>();
        var camera = Camera.main.GetComponent<PuzzleCameraController>();
        var timer = Object.FindAnyObjectByType<Timer>();
        var panel = GameObject.Find("Canvas").transform.Find("PausePanel").gameObject;
        Assert.IsFalse(panel.activeSelf);
        BlockUI piece = manager.PieceContent.GetComponentsInChildren<BlockUI>().First(ui =>
        {
            var block = manager.Puzzle[PieceKey(ui)];
            return block.Direction == manager.CurrentDirection && block.Surface.activeInHierarchy;
        });
        string key = PieceKey(piece);
        var pointer = new PointerEventData(EventSystem.current) { position = manager.Puzzle[key].ScreenPosition };
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        Assert.IsNotEmpty(manager.DragCanvas.GetComponentsInChildren<Image>());
        camera.RotateRight();
        yield return null;
        Assert.IsTrue(camera.IsRotating);
        Escape(view);
        Assert.IsTrue(manager.IsPaused); Assert.AreEqual(0f, Time.timeScale); Assert.IsTrue(panel.activeSelf);
        Assert.IsEmpty(manager.DragCanvas.GetComponentsInChildren<Image>(), "In-flight drag is hidden immediately");
        float remaining = timer.Clock.RemainingTime, elapsed = manager.PlayingTime;
        int score = manager.Score, count = manager.CompletedPieceCount, direction = camera.CurrentDirection;
        Vector3 position = Camera.main.transform.position;
        Quaternion rotation = Camera.main.transform.rotation;
        foreach (KeyCode keyCode in new[] { KeyCode.A, KeyCode.D, KeyCode.LeftArrow, KeyCode.RightArrow })
        {
            typeof(PuzzleCameraController).GetField("getKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(camera, new Func<KeyCode, bool>(candidate => candidate == keyCode));
            camera.SendMessage("Update");
        }
        typeof(PuzzleCameraController).GetField("getKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(camera, new Func<KeyCode, bool>(_ => false));
        Click("RotateLeft"); Click("RotateRight");
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer); piece.OnEndDrag(pointer);
        Assert.IsFalse(manager.TryCompletePiece(key, pointer.position));
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.AreEqual(remaining, timer.Clock.RemainingTime);
        Assert.IsFalse(timer.Clock.IsRunning);
        Assert.AreEqual(elapsed, manager.PlayingTime);
        Assert.AreEqual(score, manager.Score); Assert.AreEqual(count, manager.CompletedPieceCount);
        Assert.AreEqual(direction, camera.CurrentDirection);
        Assert.AreEqual(position, Camera.main.transform.position); Assert.AreEqual(rotation, Camera.main.transform.rotation);
        Escape(view);
        Assert.IsFalse(manager.IsPaused); Assert.AreEqual(1f, Time.timeScale); Assert.IsFalse(panel.activeSelf);
        piece.OnEndDrag(pointer);
        Assert.AreEqual(count, manager.CompletedPieceCount, "Old pointer release must not place a piece after resuming");
        yield return WaitFor(() => !camera.IsRotating, "Paused rotation completes after resume");
        Assert.Less(timer.Clock.RemainingTime, remaining);
        PointerClick(GameObject.Find("Canvas/PauseButton").GetComponent<Button>());
        Assert.IsTrue(manager.IsPaused);
        PointerClick(panel.transform.Find("ResumeButton").GetComponent<Button>());
        Assert.IsFalse(manager.IsPaused); Assert.AreEqual(1f, Time.timeScale);
    }

    [UnityTest]
    public IEnumerator PauseDoesNotPayOrLosePendingCompletionBonus()
    {
        yield return EnterPuzzle("Weather", "Spring");
        var manager = GameManager.Instance;
        var timer = Object.FindAnyObjectByType<Timer>();
        timer.Clock.Start(); timer.Clock.Tick(10f);
        var piece = manager.Puzzle.First(p => !p.Value.IsComplete && p.Value.Surface.activeInHierarchy && p.Value.Direction == manager.CurrentDirection);
        float before = timer.Clock.RemainingTime;
        Assert.IsTrue(manager.TryCompletePiece(piece.Key, piece.Value.ScreenPosition));
        int score = manager.Score;
        manager.PauseGame();
        yield return new WaitForSecondsRealtime(.2f);
        Assert.AreEqual(before, timer.Clock.RemainingTime); Assert.AreEqual(score, manager.Score);
        manager.ResumeGame(); timer.SendMessage("Update");
        float rewarded = timer.Clock.RemainingTime;
        Assert.That(rewarded, Is.EqualTo(before + manager.CurrentStageData.TimeBonusSeconds).Within(.001f));
        manager.PauseGame(); yield return null; manager.ResumeGame(); timer.SendMessage("Update");
        Assert.LessOrEqual(timer.Clock.RemainingTime, rewarded, "Resume must not grant another completion bonus");
        Assert.AreEqual(score, manager.Score);
    }

    [UnityTest]
    public IEnumerator PausedSceneTransitionsRestoreTimeScale([Values("Restart", "Stage", "Main", "Direct")] string target)
    {
        yield return EnterPuzzle("Weather", "Spring");
        var manager = GameManager.Instance;
        manager.PauseGame();
        var ui = Object.FindAnyObjectByType<UI>();
        if (target == "Restart") ui.TryAgain();
        else if (target == "Stage") ui.ToStage();
        else if (target == "Main") ui.ToMain();
        else yield return SceneManager.LoadSceneAsync("Main");
        yield return null;
        Assert.AreEqual(1f, Time.timeScale); Assert.IsFalse(manager.IsPaused);
        if(target == "Restart")
        {
            var view = Object.FindAnyObjectByType<PuzzlePauseUI>();
            Escape(view); Assert.IsFalse(manager.IsPaused, "Countdown cannot be paused");
            yield return WaitFor(()=>manager.HasStarted, "Restart countdown completes");
            Assert.AreEqual(0, manager.Score); Assert.AreEqual(0, manager.CompletedPieceCount);
            Assert.That(Object.FindAnyObjectByType<Timer>().Clock.RemainingTime, Is.InRange(19f,20f));
        }
        else { manager.PauseGame(); Assert.IsFalse(manager.IsPaused); }
    }

    [UnityTest]
    public IEnumerator PauseBackButtonCleansRuntimeAndReturnsToStageThenReenters()
    {
        yield return EnterPuzzle("Weather", "Spring");
        var manager = GameManager.Instance;
        var selection = manager.Session;
        var topic = selection.Topic;
        var stage = selection.Stage;
        manager.PauseGame();
        var backGraphic = GameObject.Find("Canvas/PausePanel/BackButton").GetComponent<Image>();
        Canvas.ForceUpdateCanvases();
        Debug.Log("Pause Back canvas depth on activation: " + backGraphic.canvasRenderer.absoluteDepth);
        // A newly activated Canvas needs a rendered frame before a physical pointer can hit it.
        yield return null;
        Debug.Log("Pause Back canvas depth after first frame: " + backGraphic.canvasRenderer.absoluteDepth);
        bool resumedBeforeCleanup = false;
        Action<bool> observeResume = paused =>
        {
            if (paused) return;
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(manager.IsPaused);
            resumedBeforeCleanup = manager.Puzzle.Count > 0;
        };
        manager.PauseChanged += observeResume;
        Button back = GameObject.Find("Canvas/PausePanel/BackButton").GetComponent<Button>();
        Assert.AreEqual("ToStage", back.onClick.GetPersistentMethodName(0));
        Assert.IsInstanceOf<UI>(back.onClick.GetPersistentTarget(0));
        PointerClick(back);
        manager.PauseChanged -= observeResume;
        Assert.IsTrue(resumedBeforeCleanup, "Time and pause state are restored before cleanup");
        Assert.IsEmpty(manager.Puzzle);
        Assert.IsFalse(manager.HasStarted);
        Assert.AreEqual(0, manager.Score); Assert.AreEqual(0, manager.CompletedPieceCount);
        Assert.AreEqual(0, manager.TotalPieceCount); Assert.AreEqual(0f, manager.PlayingTime);
        Assert.IsNull(manager.Ground); Assert.IsNull(manager.BlockParent);
        Assert.IsNull(manager.PieceContent); Assert.IsNull(manager.DragCanvas);
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "Stage", "Pause Back opens Stage");
        yield return null;
        Assert.AreEqual(1f, Time.timeScale); Assert.IsFalse(manager.IsPaused);
        Assert.AreEqual(GameManager.Status.Stage, manager.CurrentScreen);
        Assert.AreSame(selection, manager.Session);
        Assert.AreEqual(topic, selection.Topic); Assert.AreEqual(stage, selection.Stage);
        Button stageButton = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "Spring");
        stageButton.onClick.Invoke();
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "Puzzle" && manager.HasStarted, "Reenter after Pause Back");
        Assert.AreEqual(1f, Time.timeScale); Assert.IsFalse(manager.IsPaused);
        Assert.IsNotNull(manager.Ground); Assert.IsNotEmpty(manager.Puzzle);
        Assert.AreEqual(0, manager.Score); Assert.AreEqual(0, manager.CompletedPieceCount);
        Assert.That(Object.FindAnyObjectByType<Timer>().Clock.RemainingTime, Is.InRange(19f, 20f));
        Assert.IsFalse(GameObject.Find("Canvas").transform.Find("PausePanel").gameObject.activeSelf);
    }

    [UnityTest]
    public IEnumerator SuccessAndFailureRejectPause()
    {
        yield return EnterPuzzle("Weather", "Spring");
        var manager = GameManager.Instance;
        var timer = Object.FindAnyObjectByType<Timer>();
        timer.Clock.Start(); timer.Clock.Tick(timer.Clock.TimeLimit + 1);
        Escape(Object.FindAnyObjectByType<PuzzlePauseUI>()); Click("PauseGame");
        Assert.IsFalse(manager.IsPaused); Assert.AreEqual(1f,Time.timeScale); Assert.IsTrue(timer.FailurePanel.activeSelf);
        Click("TryAgain"); yield return null;
        yield return WaitFor(()=>manager.HasStarted,"Retry before success");
        yield return PlaceAvailableBlocks();
        Escape(Object.FindAnyObjectByType<PuzzlePauseUI>()); Click("PauseGame");
        Assert.IsFalse(manager.IsPaused); Assert.AreEqual(1f,Time.timeScale); Assert.IsTrue(manager.IsSuccessful);
    }

    [UnityTest]
    public IEnumerator ManagerInspectorSpeedControlsActualFrameRotation()
    {
        yield return EnterPuzzle("Weather", "Spring");
        var manager = GameManager.Instance;
        var camera = Camera.main.GetComponent<PuzzleCameraController>();
        Assert.AreEqual(3,manager.CameraRotationSpeed);
        foreach(int speed in new[]{1,3,5,10})
        {
            TestState.Set(manager,"cameraRotationSpeed",speed);
            Quaternion before = Camera.main.transform.rotation;
            float elapsed=0;
            camera.RotateRight();
            while(camera.IsRotating && elapsed<3f) { yield return null; elapsed+=Time.deltaTime; }
            Assert.IsFalse(camera.IsRotating);
            Assert.That(Quaternion.Angle(before,Camera.main.transform.rotation),Is.EqualTo(90f).Within(.001f));
            Assert.That(elapsed,Is.EqualTo(1f/speed).Within(Mathf.Max(.08f,2*Time.deltaTime)));
        }
    }
}
