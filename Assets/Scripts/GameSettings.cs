using UnityEngine;

// Persistent user preferences shared by the Main UI and puzzle camera.
public static class GameSettings
{
    public const string CameraRotationSpeedKey = "CameraRotationSpeedLevel";
    public const int DefaultCameraRotationSpeedLevel = 3;
    public const int MinCameraRotationSpeedLevel = 1;
    public const int MaxCameraRotationSpeedLevel = 10;

    public static int CameraRotationSpeedLevel => ClampCameraSpeed(
        PlayerPrefs.GetInt(CameraRotationSpeedKey, DefaultCameraRotationSpeedLevel));

    public static void SetCameraRotationSpeedLevel(int level)
    {
        level = ClampCameraSpeed(level);
        if (PlayerPrefs.HasKey(CameraRotationSpeedKey) &&
            PlayerPrefs.GetInt(CameraRotationSpeedKey) == level) return;

        PlayerPrefs.SetInt(CameraRotationSpeedKey, level);
        PlayerPrefs.Save();
    }

    private static int ClampCameraSpeed(int level) => Mathf.Clamp(level,
        MinCameraRotationSpeedLevel, MaxCameraRotationSpeedLevel);
}
