using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class PuzzleTimerTests
{
    private StageData data;
    private GameManager manager;
    private GameObject timerObject;
    private GameObject failurePanel;

    [SetUp]
    public void Setup()
    {
        data = ScriptableObject.CreateInstance<StageData>();
        SetSetting("timeLimitSeconds", 30.5f);
        SetSetting("timeBonusSeconds", 2.5f);
    }

    private void SetSetting(string name, float value) => typeof(StageData)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(data, value);

    [Test]
    public void ClockNeedsNoSceneOrSliderAndHonorsStartStopReset()
    {
        var clock = new PuzzleTimer(data);
        Assert.AreEqual(30.5f, clock.TimeLimit);
        clock.Tick(10f);
        Assert.AreEqual(30.5f, clock.RemainingTime);
        clock.Start();
        clock.Tick(3.5f);
        Assert.AreEqual(27f, clock.RemainingTime);
        clock.Stop();
        clock.Tick(10f);
        Assert.AreEqual(27f, clock.RemainingTime);
        SetSetting("timeLimitSeconds", 17f);
        clock.Reset(data);
        Assert.AreEqual(17f, clock.RemainingTime);
        Assert.AreEqual(17f, clock.TimeLimit);
        Assert.IsFalse(clock.IsRunning);
        Assert.IsFalse(clock.IsExpired);
    }

    [Test]
    public void BonusCapsAtLimitAndExpiryOccursOnceWithoutUI()
    {
        var clock = new PuzzleTimer(data);
        int expirations = 0;
        clock.Expired += () => expirations++;
        clock.Start();
        clock.Tick(10f);
        clock.AddCompletionBonus();
        Assert.AreEqual(23f, clock.RemainingTime);
        for (int i = 0; i < 10; i++) clock.AddCompletionBonus();
        Assert.AreEqual(30.5f, clock.RemainingTime);
        clock.Tick(100f);
        Assert.AreEqual(0f, clock.RemainingTime);
        Assert.IsTrue(clock.IsExpired);
        Assert.IsFalse(clock.IsRunning);
        clock.Start();
        clock.AddCompletionBonus();
        clock.Tick(1f);
        Assert.AreEqual(1, expirations);
        Assert.AreEqual(0f, clock.RemainingTime);
    }

    [Test]
    public void ZeroLimitWaitsUntilStartedThenExpires()
    {
        SetSetting("timeLimitSeconds", 0f);
        var clock = new PuzzleTimer(data);
        clock.Tick(1f);
        Assert.IsFalse(clock.IsExpired);
        clock.Start();
        clock.Tick(0f);
        Assert.IsTrue(clock.IsExpired);
    }

    private Timer CreateBridge(bool withSlider)
    {
        manager = new GameObject("Timer test owner").AddComponent<GameManager>();
        manager.SetScreen(GameManager.Status.Puzzle);
        timerObject = withSlider ? new GameObject("Timer UI", typeof(RectTransform), typeof(Slider)) : new GameObject("Timer without UI");
        var bridge = timerObject.AddComponent<Timer>();
        failurePanel = new GameObject("Failure panel");
        failurePanel.SetActive(false);
        typeof(Timer).GetField("failurePanel", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge, failurePanel);
        bridge.Initialize(manager, data);
        return bridge;
    }

    [UnityTest]
    public IEnumerator BridgeWaitsForGameplayAndSliderCannotChangeTimeOrFailure()
    {
        Timer bridge = CreateBridge(true);
        Slider slider = timerObject.GetComponent<Slider>();
        Assert.AreEqual(30.5f, slider.maxValue);
        yield return new WaitForSeconds(0.1f);
        Assert.AreEqual(30.5f, bridge.Clock.RemainingTime);
        slider.value = 0f;
        Assert.AreEqual(30.5f, bridge.Clock.RemainingTime);
        Assert.AreEqual(30.5f, slider.value);
        Assert.IsFalse(failurePanel.activeSelf);
        TestState.Set(manager, "start", true);
        yield return new WaitForSeconds(0.1f);
        Assert.Less(bridge.Clock.RemainingTime, 30.5f);
        Assert.AreEqual(bridge.Clock.RemainingTime, slider.value);
        manager.SetScreen(GameManager.Status.Stage);
        float paused = bridge.Clock.RemainingTime;
        yield return new WaitForSeconds(0.1f);
        Assert.AreEqual(paused, bridge.Clock.RemainingTime);
        manager.SetScreen(GameManager.Status.Puzzle);
        TestState.Set(manager, "success", true);
        yield return new WaitForSeconds(0.1f);
        Assert.AreEqual(paused, bridge.Clock.RemainingTime);
        Assert.IsFalse(bridge.Clock.IsRunning);
    }

    [UnityTest]
    public IEnumerator CompletionNotificationsCoalesceAndDoNotDuplicateAfterReenable()
    {
        Timer bridge = CreateBridge(true);
        TestState.Set(manager, "start", true);
        bridge.Clock.Start();
        bridge.Clock.Tick(10.5f);
        TestState.Set(manager, "completedPieceCount", 1);
        TestState.NotifyCompletion(manager);
        TestState.NotifyCompletion(manager);
        TestState.Set(manager, "completedPieceCount", 3);
        TestState.NotifyCompletion(manager);
        timerObject.SendMessage("Update");
        Assert.AreEqual(22.5f, bridge.Clock.RemainingTime, "One bonus for the observed count change, not three");
        TestState.NotifyCompletion(manager);
        timerObject.SendMessage("Update");
        Assert.LessOrEqual(bridge.Clock.RemainingTime, 22.5f);
        bridge.enabled = false;
        bridge.enabled = true;
        timerObject.SendMessage("Update");
        Assert.LessOrEqual(bridge.Clock.RemainingTime, 22.5f, "Re-enable cannot reward the same count again");
        bridge.Initialize(manager, data);
        TestState.Set(manager, "completedPieceCount", manager.CompletedPieceCount + 1);
        TestState.NotifyCompletion(manager);
        timerObject.SendMessage("Update");
        Assert.AreEqual(30.5f, bridge.Clock.RemainingTime, "Capped at the configured limit");
        yield return null;
    }

    [UnityTest]
    public IEnumerator BridgeExpiresWithoutSliderThenReinitializesCleanly()
    {
        Timer bridge = CreateBridge(false);
        TestState.Set(manager, "start", true);
        bridge.Clock.Start();
        bridge.Clock.Tick(100f);
        Assert.IsFalse(manager.HasStarted);
        Assert.IsFalse(manager.IsSuccessful);
        Assert.IsTrue(failurePanel.activeSelf);
        Assert.IsTrue(bridge.Clock.IsExpired);
        SetSetting("timeLimitSeconds", 12f);
        bridge.Initialize(manager, data);
        Assert.AreEqual(12f, bridge.Clock.RemainingTime);
        Assert.IsFalse(bridge.Clock.IsExpired);
        Assert.IsFalse(failurePanel.activeSelf);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (timerObject != null) Object.Destroy(timerObject);
        if (manager != null) Object.Destroy(manager.gameObject);
        if (failurePanel != null) Object.Destroy(failurePanel);
        foreach (var session in Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None)) Object.Destroy(session.gameObject);
        Object.Destroy(data);
        yield return null;
    }
}

public partial class FullFlowTests
{
    [UnityTest]
    public IEnumerator RealPieceRequestsOneTimerBonus()
    {
        yield return EnterPuzzle("Weather", "Spring");
        GameManager manager = GameManager.Instance;
        Timer bridge = Object.FindFirstObjectByType<Timer>();
        bridge.Clock.Start();
        bridge.Clock.Tick(10f);
        float before = bridge.Clock.RemainingTime;
        BlockUI piece = null;
        GameManager.Block target = null;
        foreach (var candidate in manager.PieceContent.GetComponentsInChildren<BlockUI>())
        {
            string key = (string)typeof(BlockUI).GetField("key", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(candidate);
            var block = manager.Puzzle[key];
            if (block.Direction != manager.CurrentDirection || !block.Surface.activeInHierarchy) continue;
            piece = candidate;
            target = block;
            break;
        }
        Assert.IsNotNull(piece);
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = target.ScreenPosition };
        piece.OnBeginDrag(pointer);
        piece.OnDrag(pointer);
        piece.OnEndDrag(pointer);
        bridge.SendMessage("Update");
        Assert.AreEqual(before + manager.CurrentStageData.TimeBonusSeconds, bridge.Clock.RemainingTime);
        float rewarded = bridge.Clock.RemainingTime;
        TestState.NotifyCompletion(manager);
        bridge.SendMessage("Update");
        Assert.LessOrEqual(bridge.Clock.RemainingTime, rewarded);
        yield return null;
    }
}
