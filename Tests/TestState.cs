using System.Reflection;
using System;
using UnityEngine;

// Fixture setup only; gameplay callers cannot write the manager's counters and flags.
public static class TestState
{
    public static void Set(GameManager owner, string field, object value) => typeof(GameManager)
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);

    public static void NotifyCompletion(GameManager owner) => typeof(GameManager)
        .GetMethod("NotifyPieceCompleted", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
}

// Preserve the caller's preferences; never clear unrelated PlayerPrefs.
public sealed class CameraPreferenceScope : IDisposable
{
    private readonly bool hadValue = PlayerPrefs.HasKey(GameSettings.CameraRotationSpeedKey);
    private readonly int previousValue = PlayerPrefs.GetInt(GameSettings.CameraRotationSpeedKey);

    public CameraPreferenceScope() => PlayerPrefs.DeleteKey(GameSettings.CameraRotationSpeedKey);

    public void Dispose()
    {
        if (hadValue) PlayerPrefs.SetInt(GameSettings.CameraRotationSpeedKey, previousValue);
        else PlayerPrefs.DeleteKey(GameSettings.CameraRotationSpeedKey);
        PlayerPrefs.Save();
    }
}
