using UnityEngine;
using UnityEngine.UI;

// Lives inside the existing Main settings popup; no runtime manager is required.
public sealed class CameraSpeedSettingUI : MonoBehaviour
{
    [SerializeField] private Slider cameraSpeedSlider;
    [SerializeField] private Text valueText;

    private void OnEnable()
    {
        cameraSpeedSlider.minValue = GameSettings.MinCameraRotationSpeedLevel;
        cameraSpeedSlider.maxValue = GameSettings.MaxCameraRotationSpeedLevel;
        cameraSpeedSlider.wholeNumbers = true;
        int level = GameSettings.CameraRotationSpeedLevel;
        cameraSpeedSlider.SetValueWithoutNotify(level);
        valueText.text = level.ToString();
        cameraSpeedSlider.onValueChanged.AddListener(OnSpeedChanged);
    }

    private void OnDisable()
    {
        cameraSpeedSlider.onValueChanged.RemoveListener(OnSpeedChanged);
    }

    private void OnSpeedChanged(float value)
    {
        GameSettings.SetCameraRotationSpeedLevel(Mathf.RoundToInt(value));
        valueText.text = GameSettings.CameraRotationSpeedLevel.ToString();
    }
}
