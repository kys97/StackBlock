#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Run only in the isolated validation copy. The original scene is merged by fileID.
public static class CameraSettingsSceneSetup
{
    public static void Run()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            Transform panel = GameObject.Find("Canvas").transform.Find("환경설정창");
            if (panel.Find("CameraSpeedSetting") != null) throw new Exception("Setting already exists");
            Text template = panel.Find("text").GetComponent<Text>();
            var row = new GameObject("CameraSpeedSetting", typeof(RectTransform));
            row.transform.SetParent(panel, false);
            SetRect((RectTransform)row.transform, new Vector2(0, -245), new Vector2(580, 210));
            AddText(template, row.transform, "TitleText", "카메라 회전 속도", new Vector2(0, 80), new Vector2(570, 60), template.fontSize);
            AddText(template, row.transform, "SlowText", "느림", new Vector2(-247, 0), new Vector2(100, 60), 32);
            AddText(template, row.transform, "FastText", "빠름", new Vector2(247, 0), new Vector2(100, 60), 32);
            Text value = AddText(template, row.transform, "ValueText", "3", new Vector2(0, -70), new Vector2(140, 60), template.fontSize);
            var slider = UnityEngine.Object.Instantiate(panel.Find("Slider").GetComponent<Slider>(), row.transform);
            slider.name = "CameraSpeedSlider";
            slider.onValueChanged = new Slider.SliderEvent(); // Do not clone audio listeners.
            slider.minValue = GameSettings.MinCameraRotationSpeedLevel;
            slider.maxValue = GameSettings.MaxCameraRotationSpeedLevel;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(GameSettings.DefaultCameraRotationSpeedLevel);
            SetRect((RectTransform)slider.transform, Vector2.zero, new Vector2(380, 60));
            var view = row.AddComponent<CameraSpeedSettingUI>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("cameraSpeedSlider").objectReferenceValue = slider;
            serialized.FindProperty("valueText").objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Camera settings UI saved; existing font, panel, slider sprites and colors reused.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static Text AddText(Text template, Transform parent, string name, string text, Vector2 position, Vector2 size, int fontSize)
    {
        var label = UnityEngine.Object.Instantiate(template, parent);
        label.name = name;
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        SetRect(label.rectTransform, position, size);
        return label;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
#endif
