using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public partial class FullFlowTests
{
    private StageData testData;

    [UnityTest]
    public IEnumerator PieceAndCameraReadNonDefaultStageSettings()
    {
        yield return EnterPuzzle("Weather", "Spring");
        GameManager manager = GameManager.Instance;
        testData = Object.Instantiate(manager.CurrentStageData);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(StageData).GetField("pointsPerPiece", flags).SetValue(testData, 73);
        typeof(StageData).GetField("snapDistancePixels", flags).SetValue(testData, 40f);
        typeof(StageData).GetField("rotationSpeedDegreesPerSecond", flags).SetValue(testData, 120f);
        typeof(GameSession).GetField("currentStageData", flags).SetValue(manager.Session, testData);
        var cameraController = Camera.main.GetComponent<PuzzleCameraController>();
        cameraController.Initialize(testData, manager.Ground.transform, manager);

        BlockUI piece = manager.PieceContent.GetComponentsInChildren<BlockUI>().First(ui =>
        {
            string key = (string)typeof(BlockUI).GetField("key", flags).GetValue(ui);
            var block = manager.Puzzle[key];
            return block.Direction == manager.CurrentDirection && block.Surface.activeInHierarchy;
        });
        string pieceKey = (string)typeof(BlockUI).GetField("key", flags).GetValue(piece);
        var pointer = new PointerEventData(EventSystem.current) { position = manager.Puzzle[pieceKey].ScreenPosition + Vector2.right * 60f };
        int before = manager.CompletedPieceCount;
        int scoreBefore = manager.Score;
        piece.OnBeginDrag(pointer);
        piece.OnDrag(pointer);
        piece.OnEndDrag(pointer);
        Assert.AreEqual(before, manager.CompletedPieceCount, "60 pixels must exceed the configured 40-pixel snap range");
        yield return null;
        pointer.position = manager.Puzzle[pieceKey].ScreenPosition + Vector2.right * 20f;
        piece.OnBeginDrag(pointer);
        piece.OnDrag(pointer);
        piece.OnEndDrag(pointer);
        Assert.AreEqual(before + 1, manager.CompletedPieceCount);
        Assert.AreEqual(scoreBefore + 73, manager.Score);
        yield return null;

        Click("RotateRight");
        Quaternion beforeRotation = Camera.main.transform.rotation;
        typeof(PuzzleCameraController).GetMethod("AdvanceRotation", flags).Invoke(cameraController, new object[] { 1f / 60f });
        Assert.That(Quaternion.Angle(beforeRotation, Camera.main.transform.rotation), Is.EqualTo(2f).Within(0.01f));
        yield return WaitFor(() => !Object.FindFirstObjectByType<PuzzleCameraController>().IsRotating, "Configured rotation reaches target", 8f);
    }

    [UnityTest]
    public IEnumerator StageBackReceivesPointerAndReturnsToTopic([Values("Weather", "Structure")] string topic)
    {
        yield return SceneManager.LoadSceneAsync("Main");
        yield return null;
        Click("GameStart");
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "Topic", "Topic loaded");
        yield return null;
        Click("Topic", topic);
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "Stage", "Stage loaded");
        yield return null;
        Canvas.ForceUpdateCanvases();
        StageLoad stageLoader = Object.FindFirstObjectByType<StageLoad>();
        var canvasRect = (RectTransform)GameObject.Find("Canvas").transform;
        var canvasCorners = new Vector3[4];
        canvasRect.GetWorldCorners(canvasCorners);
        foreach (GameObject cloud in new[] { stageLoader.LeftCloud, stageLoader.RightCloud })
        {
            foreach (Image image in cloud.GetComponentsInChildren<Image>())
            {
                Assert.IsTrue(image.raycastTarget, "Cloud raycast settings must remain enabled");
                var corners = new Vector3[4];
                image.rectTransform.GetWorldCorners(corners);
                if (cloud == stageLoader.LeftCloud)
                    Assert.Less(corners.Max(c => c.x), canvasCorners[0].x, "Left cloud fully outside canvas");
                else
                    Assert.Greater(corners.Min(c => c.x), canvasCorners[2].x, "Right cloud fully outside canvas");
            }
        }
        Button back = GameObject.Find("Canvas/Back").GetComponent<Button>();
        var rect = (RectTransform)back.transform;
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
            button = PointerEventData.InputButton.Left
        };
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.IsNotEmpty(hits, "Back button has a pointer hit");
        GameObject receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Assert.AreEqual(back.gameObject, receiver, "Top raycast hit: " + hits[0].gameObject.name);
        ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "Topic", "Back returns to Topic");
        Assert.AreEqual(GameManager.Status.Topic, GameManager.Instance.CurrentScreen);
        yield return null;
        Click("Topic", topic);
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "Stage", "Stage reopens");
        yield return null;
        Canvas.ForceUpdateCanvases();
        string stageName = topic == "Weather" ? "Spring" : "Bigben";
        Button stageButton = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.name == stageName);
        var stageRect = (RectTransform)stageButton.transform;
        pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, stageRect.TransformPoint(stageRect.rect.center)),
            button = PointerEventData.InputButton.Left
        };
        hits.Clear();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.IsNotEmpty(hits);
        receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Assert.AreEqual(stageButton.gameObject, receiver, "Stage button top hit: " + hits[0].gameObject.name);
        EventSystem.current.SetSelectedGameObject(receiver);
        ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "Puzzle", "Selected puzzle opens");
        yield return null;
        yield return WaitFor(() => GameManager.Instance.HasStarted, "Countdown completes after returning");
    }

    private IEnumerator WaitFor(Func<bool> condition, string description, float timeout = 15f)
    {
        float gameTimeAtStart = Time.time;
        float wallTimeAtStart = Time.realtimeSinceStartup;
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.IsTrue(condition(), description + "; scene=" + SceneManager.GetActiveScene().name
            + "; game seconds=" + (Time.time - gameTimeAtStart)
            + "; wall seconds=" + (Time.realtimeSinceStartup - wallTimeAtStart));
    }

    private void Click(string method, string objectName = null)
    {
        var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b =>
            (objectName == null || b.name == objectName) &&
            Enumerable.Range(0, b.onClick.GetPersistentEventCount()).Any(i => b.onClick.GetPersistentMethodName(i) == method));
        Assert.IsNotNull(button, "Button for " + method + "/" + objectName);
        EventSystem.current.SetSelectedGameObject(button.gameObject);
        button.onClick.Invoke();
    }

    private void AssertNoMissingComponents()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                Assert.IsFalse(child.GetComponents<Component>().Any(c => c == null), "Missing script: " + child.name);
    }

    private void AssertReferenceImage(string stage)
    {
        var preview = GameObject.Find("Canvas/Puzzle Reference");
        Assert.IsNotNull(preview, "Reference image is wired in the actual Puzzle scene");
        Assert.IsNotNull(preview.GetComponent<PuzzleReferenceImage>());
        var image = preview.GetComponent<Image>();
        Assert.IsTrue(image.enabled);
        Assert.IsNotNull(image.sprite);
        Assert.AreEqual(stage, image.sprite.name.Substring(1));
        Assert.IsTrue(image.preserveAspect);
        Assert.IsFalse(image.raycastTarget);
        var rect = image.rectTransform;
        Assert.AreEqual(Vector2.zero, rect.anchorMin);
        Assert.AreEqual(Vector2.zero, rect.anchorMax);
        Assert.AreEqual(Vector2.zero, rect.pivot);
        Canvas.ForceUpdateCanvases();
        Vector3 bottomLeft = ((RectTransform)rect.parent).InverseTransformPoint(rect.TransformPoint(rect.rect.min));
        Vector2 canvasBottomLeft = ((RectTransform)rect.parent).rect.min;
        Assert.That(bottomLeft.x - canvasBottomLeft.x, Is.EqualTo(24f).Within(0.1f));
        Assert.That(bottomLeft.y - canvasBottomLeft.y, Is.EqualTo(24f).Within(0.1f));
    }

    private IEnumerator EnterPuzzle(string topic, string stage)
    {
        yield return SceneManager.LoadSceneAsync("Main");
        yield return null;
        AssertNoMissingComponents();
        Click("GameStart");
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "Topic", "Topic loaded");
        yield return null;
        AssertNoMissingComponents();
        Click("Topic", topic);
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "Stage", "Stage loaded");
        yield return null;
        AssertNoMissingComponents();
        Button stageButton = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == stage);
        Assert.IsNotNull(stageButton, "Generated stage button: " + stage);
        EventSystem.current.SetSelectedGameObject(stageButton.gameObject);
        stageButton.onClick.Invoke();
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "Puzzle", "Puzzle loaded");
        yield return null;
        AssertNoMissingComponents();
        Timer initialTimer = Object.FindFirstObjectByType<Timer>();
        Assert.IsNotNull(initialTimer.Clock);
        Assert.AreEqual(initialTimer.Clock.TimeLimit, initialTimer.Clock.RemainingTime, "Full time before countdown ends");
        yield return WaitFor(() => GameManager.Instance.HasStarted, "Countdown starts gameplay");
        Assert.IsFalse(GameObject.Find("Canvas").transform.Find("Ready").gameObject.activeSelf);
        Assert.Greater(GameManager.Instance.TotalPieceCount, 0);
        Assert.AreEqual(GameManager.Instance.TotalPieceCount, GameManager.Instance.PieceContent.GetComponentsInChildren<BlockUI>().Length);
        AssertReferenceImage(stage);
        StageData data = GameManager.Instance.CurrentStageData;
        Assert.AreEqual(data.InitialCameraPosition, Camera.main.transform.position);
        Assert.Less(Quaternion.Angle(Quaternion.Euler(data.InitialCameraEulerAngles), Camera.main.transform.rotation), 0.01f);
        Assert.AreEqual(data.TimeLimitSeconds, Object.FindFirstObjectByType<Timer>().GetComponent<Slider>().maxValue);
        Assert.AreEqual(data.GroundPrefab.name + "(Clone)", GameManager.Instance.Ground.name);
    }

    private IEnumerator PlaceAvailableBlocks()
    {
        GameManager manager = GameManager.Instance;
        for (int turn = 0; turn < 32 && !manager.IsSuccessful; turn++)
        {
            foreach (BlockUI blockUI in manager.PieceContent.GetComponentsInChildren<BlockUI>())
            {
                string key = (string)typeof(BlockUI).GetField("key", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(blockUI);
                GameManager.Block block = manager.Puzzle[key];
                if (block.Direction != manager.CurrentDirection || !block.Surface.activeInHierarchy)
                    continue;
                var pointer = new PointerEventData(EventSystem.current) { position = block.ScreenPosition };
                blockUI.OnBeginDrag(pointer);
                blockUI.OnDrag(pointer);
                int before = manager.CompletedPieceCount;
                int scoreBefore = manager.Score;
                blockUI.OnEndDrag(pointer);
                Assert.AreEqual(before + 1, manager.CompletedPieceCount, "Placed " + key);
                int expectedScore = scoreBefore + manager.CurrentStageData.PointsPerPiece;
                if (manager.IsSuccessful) expectedScore -= (int)manager.PlayingTime;
                Assert.AreEqual(expectedScore, manager.Score, "Score for " + key);
            }
            yield return null;
            if (manager.IsSuccessful) break;
            Click("RotateRight");
            yield return WaitFor(() => !Object.FindFirstObjectByType<PuzzleCameraController>().IsRotating, "Camera finishes 90-degree rotation", 8f);
        }
        Assert.IsTrue(manager.IsSuccessful, "All reachable blocks can be placed");
        yield return null;
        Assert.IsTrue(Object.FindFirstObjectByType<StageLoad>().SuccessPanel.activeSelf, "Success panel visible");
        Timer completedTimer = Object.FindFirstObjectByType<Timer>();
        float stoppedTime = completedTimer.Clock.RemainingTime;
        yield return new WaitForSeconds(0.1f);
        Assert.AreEqual(stoppedTime, completedTimer.Clock.RemainingTime, "Success freezes time");
        Assert.IsFalse(completedTimer.Clock.IsRunning);
    }

    [UnityTest]
    public IEnumerator WeatherFlowIncludesCompletionNextStageAndRetry()
    {
        yield return EnterPuzzle("Weather", "Spring");
        yield return PlaceAvailableBlocks();
        Click("NextStage");
        yield return null;
        yield return WaitFor(() => GameManager.Instance.Session.Stage == GameManager.Stage.Summer && GameManager.Instance.HasStarted, "Summer ready");
        AssertReferenceImage("Summer");
        yield return PlaceAvailableBlocks();
        Object.FindFirstObjectByType<UI>().TryAgain();
        yield return null;
        yield return WaitFor(() => GameManager.Instance.HasStarted && !GameManager.Instance.IsSuccessful, "Retry ready");
        Assert.AreEqual(0, GameManager.Instance.CompletedPieceCount);
        Assert.IsFalse(GameObject.Find("Canvas").transform.Find("Ready").gameObject.activeSelf);
        AssertReferenceImage("Summer");
    }

    [UnityTest]
    public IEnumerator ReferenceImageObservesStageChangesWithoutChangingPuzzle()
    {
        yield return EnterPuzzle("Weather", "Spring");
        GameManager manager = GameManager.Instance;
        int originalScore = manager.Score;
        int originalCount = manager.CompletedPieceCount;
        var originalPuzzle = manager.Puzzle;
        var originalBlock = manager.Puzzle.Values.First();
        manager.Session.Topic = GameManager.Topic.Structure;
        manager.Session.Stage = GameManager.Stage.Bigben;
        yield return null;
        yield return null;
        AssertReferenceImage("Bigben");
        Assert.AreEqual(originalScore, manager.Score);
        Assert.AreEqual(originalCount, manager.CompletedPieceCount);
        Assert.AreSame(originalPuzzle, manager.Puzzle);
        Assert.AreSame(originalBlock, manager.Puzzle.Values.First());
        manager.Session.Topic = GameManager.Topic.Weather;
        manager.Session.Stage = GameManager.Stage.Spring;
        yield return null;
        yield return null;
        AssertReferenceImage("Spring");
    }

    [UnityTest]
    public IEnumerator StructureFlowCompletesBigben()
    {
        yield return EnterPuzzle("Structure", "Bigben");
        yield return PlaceAvailableBlocks();
    }

    private static readonly string[] OtherStages = { "Desert", "Fall", "Winter", "Egypt", "OperaHouse", "TowerBridge" };

    [UnityTest]
    public IEnumerator RemainingStagesCanBeCompleted([ValueSource(nameof(OtherStages))] string stage)
    {
        string topic = (int)Enum.Parse(typeof(GameManager.Stage), stage) < (int)GameManager.Stage.Bigben ? "Weather" : "Structure";
        yield return EnterPuzzle(topic, stage);
        yield return PlaceAvailableBlocks();
    }

    [UnityTest]
    public IEnumerator TimeoutShowsFailureAndRetryResetsPuzzle()
    {
        yield return EnterPuzzle("Weather", "Spring");
        yield return WaitFor(() => !GameManager.Instance.HasStarted, "Timer expires", GameManager.Instance.CurrentStageData.TimeLimitSeconds + 3f);
        Assert.IsFalse(GameManager.Instance.IsSuccessful);
        Assert.IsTrue(Object.FindFirstObjectByType<Timer>().FailurePanel.activeSelf);
        Click("TryAgain");
        yield return null;
        yield return WaitFor(() => GameManager.Instance.HasStarted, "Retry after failure");
        Assert.IsFalse(Object.FindFirstObjectByType<Timer>().FailurePanel.activeSelf);
        Timer retryTimer = Object.FindFirstObjectByType<Timer>();
        Assert.IsFalse(retryTimer.Clock.IsExpired);
        Assert.That(retryTimer.Clock.RemainingTime, Is.InRange(retryTimer.Clock.TimeLimit - 1f, retryTimer.Clock.TimeLimit));
        Assert.AreEqual(0, GameManager.Instance.CompletedPieceCount);
        Object.FindFirstObjectByType<UI>().ToStage();
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "Stage", "Return to selection");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Scene gameplayScene = SceneManager.GetActiveScene();
        Scene cleanupScene = SceneManager.CreateScene("Test cleanup");
        SceneManager.SetActiveScene(cleanupScene);
        yield return SceneManager.UnloadSceneAsync(gameplayScene);
        foreach (GameManager manager in Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None)) Object.Destroy(manager.gameObject);
        foreach (Sound sound in Object.FindObjectsByType<Sound>(FindObjectsSortMode.None)) Object.Destroy(sound.gameObject);
        foreach (GameSession session in Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None)) Object.Destroy(session.gameObject);
        if (testData != null) Object.Destroy(testData);
        yield return null;
    }
}
