using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Retains the existing MonoScript GUID. UI and temporary GameManager bridge only.
public class Timer : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("fail_pan")] private GameObject failurePanel;
    private Slider slider;
    private GameManager owner;
    private int lastRewardedCompletionCount;
    private int pendingCompletionCount;
    private bool subscribed;

    public PuzzleTimer Clock { get; private set; }
    public GameObject FailurePanel => failurePanel;

    private void Awake() => slider = GetComponent<Slider>();

    public void Initialize(GameManager manager, StageData data)
    {
        Unsubscribe();
        Clock?.Stop();
        owner = manager;
        Clock = new PuzzleTimer(data);
        lastRewardedCompletionCount = pendingCompletionCount = manager.CompletedPieceCount;
        if (failurePanel != null) failurePanel.SetActive(false);
        if (isActiveAndEnabled) Subscribe();
        RefreshDisplay();
    }

    private void OnEnable() => Subscribe();

    private void Subscribe()
    {
        if (subscribed || Clock == null || owner == null) return;
        pendingCompletionCount = owner.CompletedPieceCount;
        owner.PieceCompleted += OnPieceCompleted;
        Clock.Changed += RefreshDisplay;
        Clock.Expired += OnExpired;
        if (slider != null) slider.onValueChanged.AddListener(OnSliderValueChanged);
        subscribed = true;
        RefreshDisplay();
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (owner != null) owner.PieceCompleted -= OnPieceCompleted;
        Clock.Changed -= RefreshDisplay;
        Clock.Expired -= OnExpired;
        if (slider != null) slider.onValueChanged.RemoveListener(OnSliderValueChanged);
        subscribed = false;
    }

    private void OnDisable()
    {
        Clock?.Stop();
        Unsubscribe();
    }

    private void OnPieceCompleted(int completedCount) => pendingCompletionCount = completedCount;
    private void OnSliderValueChanged(float ignoredValue) => RefreshDisplay();

    private void Update()
    {
        if (Clock == null) return;
        if (owner == null || owner.CurrentScreen != GameManager.Status.Puzzle || !owner.HasStarted || owner.IsSuccessful)
        {
            Clock.Stop();
            return;
        }

        Clock.Start();
        if (pendingCompletionCount != lastRewardedCompletionCount)
        {
            // Match the old observed-count rule: one bonus per update, not per count delta.
            lastRewardedCompletionCount = pendingCompletionCount;
            Clock.AddCompletionBonus();
        }
        else Clock.Tick(Time.deltaTime);
    }

    private void RefreshDisplay()
    {
        if (slider == null || Clock == null) return;
        slider.minValue = 0f;
        slider.maxValue = Clock.TimeLimit;
        slider.SetValueWithoutNotify(Clock.RemainingTime);
    }

    private void OnExpired()
    {
        if (owner == null) return;
        owner.BlockFail();
        if (failurePanel != null) failurePanel.SetActive(true);
    }
}
