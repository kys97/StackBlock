using System;
using UnityEngine;

// Time state only: no scene objects, UI, singleton or game-failure dependency.
public sealed class PuzzleTimer
{
    public float TimeLimit { get; private set; }
    public float RemainingTime { get; private set; }
    public float TimeBonus { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsExpired { get; private set; }
    public event Action Changed;
    public event Action Expired;

    public PuzzleTimer(StageData data) => Reset(data);

    public void Reset(StageData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        TimeLimit = Mathf.Max(0f, data.TimeLimitSeconds);
        TimeBonus = Mathf.Max(0f, data.TimeBonusSeconds);
        RemainingTime = TimeLimit;
        IsRunning = false;
        IsExpired = false;
        Changed?.Invoke();
    }

    public void Start()
    {
        if (!IsExpired) IsRunning = true;
    }

    public void Stop() => IsRunning = false;

    public void AddCompletionBonus()
    {
        if (!IsRunning || IsExpired) return;
        SetRemainingTime(Mathf.Min(TimeLimit, RemainingTime + TimeBonus));
    }

    // The compatibility component supplies Time.deltaTime once per playing frame.
    public void Tick(float deltaTime)
    {
        if (!IsRunning || IsExpired) return;
        SetRemainingTime(Mathf.Max(0f, RemainingTime - Mathf.Max(0f, deltaTime)));
        if (RemainingTime > 0f) return;
        IsRunning = false;
        IsExpired = true;
        Expired?.Invoke();
    }

    private void SetRemainingTime(float value)
    {
        if (RemainingTime == value) return;
        RemainingTime = value;
        Changed?.Invoke();
    }
}
