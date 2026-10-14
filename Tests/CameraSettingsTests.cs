using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class GameSettingsTests
{
    private CameraPreferenceScope preferences;
    [SetUp] public void Setup() => preferences = new CameraPreferenceScope();
    [TearDown] public void Cleanup() => preferences.Dispose();

    [Test]
    public void FirstLaunchUsesThreeWithoutCreatingAValue()
    {
        Assert.AreEqual(3, GameSettings.CameraRotationSpeedLevel);
        Assert.IsFalse(PlayerPrefs.HasKey(GameSettings.CameraRotationSpeedKey));
    }

    [TestCase(0, 1), TestCase(15, 10), TestCase(7, 7), TestCase(-100, 1)]
    public void StoredValuesAreClamped(int saved, int expected)
    {
        PlayerPrefs.SetInt(GameSettings.CameraRotationSpeedKey, saved);
        Assert.AreEqual(expected, GameSettings.CameraRotationSpeedLevel);
    }

    [TestCase(0, 1), TestCase(15, 10), TestCase(7, 7)]
    public void WritesPersistAnIntegerInRange(int requested, int expected)
    {
        GameSettings.SetCameraRotationSpeedLevel(requested);
        Assert.AreEqual(expected, PlayerPrefs.GetInt(GameSettings.CameraRotationSpeedKey));
    }
}

[PrebuildSetup(typeof(CameraSettingsGameViewSetup))]
public partial class FullFlowTests
{
    private Transform SettingsPanel => GameObject.Find("Canvas").transform.Find("환경설정창");
    private Slider CameraSettingSlider => SettingsPanel.Find("CameraSpeedSetting/CameraSpeedSlider").GetComponent<Slider>();
    private Text CameraSettingValue => SettingsPanel.Find("CameraSpeedSetting/ValueText").GetComponent<Text>();

    private IEnumerator OpenSettings()
    {
        // Wait for the newly loaded Canvas to receive its first rendered layout.
        yield return null;
        var button = GameObject.Find("Canvas/환경설정").GetComponent<Button>();
        Debug.Log("Settings viewport=" + Screen.width + "x" + Screen.height + "; button=" + button.transform.position + "; depth=" + button.image.depth);
        PointerClick(button);
        yield return null;
        Assert.IsTrue(SettingsPanel.gameObject.activeSelf);
        AssertNoMissingComponents();
    }

    // Exercise the Slider's pointer path and verify the topmost raycast receiver.
    private void SetCameraSpeedWithPointer(int level)
    {
        var slider = CameraSettingSlider;
        var area = (RectTransform)slider.handleRect.parent;
        var local = new Vector3(Mathf.Lerp(area.rect.xMin, area.rect.xMax, (level - 1) / 9f), area.rect.center.y);
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, area.TransformPoint(local)),
            button = PointerEventData.InputButton.Left
        };
        Canvas.ForceUpdateCanvases();
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.IsNotEmpty(hits);
        Assert.AreEqual(slider.gameObject, ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject));
        ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        Assert.AreEqual(level, slider.value);
        Assert.AreEqual(level.ToString(), CameraSettingValue.text);
        Assert.AreEqual(level, PlayerPrefs.GetInt(GameSettings.CameraRotationSpeedKey));
    }

    [UnityTest]
    public IEnumerator SettingsPopupDefaultsAndReopensWithoutChangingAudio()
    {
        yield return SceneManager.LoadSceneAsync("Main");
        yield return null;
        yield return OpenSettings();
        var slider = CameraSettingSlider;
        Assert.AreEqual(1, slider.minValue); Assert.AreEqual(10, slider.maxValue); Assert.IsTrue(slider.wholeNumbers);
        Assert.AreEqual(3, slider.value); Assert.AreEqual("3", CameraSettingValue.text);
        var audio = SettingsPanel.Find("Slider").GetComponent<Slider>();
        float originalVolume = audio.value;
        int originalAudioListeners = audio.onValueChanged.GetPersistentEventCount();
        Assert.AreEqual(0, slider.onValueChanged.GetPersistentEventCount());
        SetCameraSpeedWithPointer(7);
        for (int i = 0; i < 3; i++)
        {
            PointerClick(SettingsPanel.Find("close").GetComponent<Button>());
            Assert.IsFalse(SettingsPanel.gameObject.activeSelf);
            yield return OpenSettings();
            Assert.AreEqual(7, CameraSettingSlider.value); Assert.AreEqual("7", CameraSettingValue.text);
        }
        Assert.AreEqual(originalVolume, audio.value);
        Assert.AreEqual(originalAudioListeners, audio.onValueChanged.GetPersistentEventCount());
        // Reopening reads storage again, without invoking a save callback.
        SettingsPanel.gameObject.SetActive(false);
        PlayerPrefs.SetInt(GameSettings.CameraRotationSpeedKey, 15);
        yield return OpenSettings();
        Assert.AreEqual(10, CameraSettingSlider.value); Assert.AreEqual("10", CameraSettingValue.text);
        Assert.AreEqual(15, PlayerPrefs.GetInt(GameSettings.CameraRotationSpeedKey));
    }

    [UnityTest]
    public IEnumerator MainSettingsSpeedAppliesWhenEnteringPuzzle([Values(1, 10)] int level)
    {
        yield return SceneManager.LoadSceneAsync("Main");
        yield return null;
        yield return OpenSettings();
        SetCameraSpeedWithPointer(level);
        PointerClick(SettingsPanel.Find("close").GetComponent<Button>());
        // Existing navigation helper follows Main -> Topic -> Stage -> Puzzle.
        yield return EnterPuzzle("Weather", "Spring");
        Assert.AreEqual(level, GameManager.Instance.CameraRotationSpeed);
        var camera = Camera.main.GetComponent<PuzzleCameraController>();
        var initial = camera.transform.rotation;
        float elapsed = 0;
        camera.RotateRight();
        while (camera.IsRotating && elapsed < 3) { yield return null; elapsed += Time.deltaTime; }
        Assert.IsFalse(camera.IsRotating);
        Assert.That(elapsed, Is.EqualTo(1f / level).Within(Mathf.Max(.08f, 2 * Time.deltaTime)));
        Assert.That(Quaternion.Angle(initial, camera.transform.rotation), Is.EqualTo(90f).Within(.001f));
    }

    [UnityTest]
    public IEnumerator SettingsSevenSurvivesSceneRoundTrip()
    {
        yield return SceneManager.LoadSceneAsync("Main");
        yield return null;
        yield return OpenSettings();
        SetCameraSpeedWithPointer(7);
        PointerClick(SettingsPanel.Find("close").GetComponent<Button>());
        Click("GameStart");
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "Topic", "Topic after settings");
        yield return null;
        Object.FindAnyObjectByType<UI>().ToMain();
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "Main", "Return to Main");
        yield return null;
        yield return OpenSettings();
        Assert.AreEqual(7, CameraSettingSlider.value); Assert.AreEqual("7", CameraSettingValue.text);
        string capture = Path.GetFullPath("../../Logs/camera-settings-main.png");
        ScreenCapture.CaptureScreenshot(capture);
        yield return WaitFor(() => File.Exists(capture), "Settings screenshot written", 10f);
    }
}

// Run these two tests by name in separate Unity processes, in order.
// A dedicated product name prevents touching the user's real preferences.
[PrebuildSetup(typeof(CameraSettingsGameViewSetup))]
public class GameSettingsRestartTests
{
    private IEnumerator OpenSettings()
    {
        Assert.AreEqual("StackBlock_CameraSettingsValidation", Application.productName);
        yield return SceneManager.LoadSceneAsync("Main");
        yield return null;
        GameObject.Find("Canvas/환경설정").GetComponent<Button>().onClick.Invoke();
        yield return null;
    }

    [UnityTest, Explicit("Persistence writer: run before reader in a separate process")]
    public IEnumerator SaveSevenBeforeExit()
    {
        yield return OpenSettings();
        GameObject.Find("Canvas/환경설정창/CameraSpeedSetting/CameraSpeedSlider").GetComponent<Slider>().value = 7;
        Assert.AreEqual(7, PlayerPrefs.GetInt(GameSettings.CameraRotationSpeedKey));
        Assert.AreEqual("7", GameObject.Find("Canvas/환경설정창/CameraSpeedSetting/ValueText").GetComponent<Text>().text);
    }

    [UnityTest, Explicit("Persistence reader: run after writer exits")]
    public IEnumerator ReadSevenAfterRestart()
    {
        // No SetInt occurs before this read in the new process.
        Assert.AreEqual(7, PlayerPrefs.GetInt(GameSettings.CameraRotationSpeedKey, -1));
        yield return OpenSettings();
        Assert.AreEqual(7, GameObject.Find("Canvas/환경설정창/CameraSpeedSetting/CameraSpeedSlider").GetComponent<Slider>().value);
        Assert.AreEqual("7", GameObject.Find("Canvas/환경설정창/CameraSpeedSetting/ValueText").GetComponent<Text>().text);
        PlayerPrefs.DeleteKey(GameSettings.CameraRotationSpeedKey);
        PlayerPrefs.Save();
    }
}

// The Main scene uses a fixed-pixel layout. Batch Play Mode otherwise defaults
// to 640x480, placing its existing menu buttons outside the test viewport.
public class CameraSettingsGameViewSetup : IPrebuildSetup
{
    public void Setup()
    {
#if UNITY_EDITOR
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var assembly = typeof(UnityEditor.Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = sizesType.BaseType.GetProperty("instance", System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy).GetValue(null);
        var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
        var group = sizesType.GetMethod("GetGroup", flags).Invoke(sizes, new[] { System.Enum.Parse(groupType, "Standalone") });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var kindType = assembly.GetType("UnityEditor.GameViewSizeType");
        var size = System.Activator.CreateInstance(sizeType, flags, null,
            new object[] { System.Enum.Parse(kindType, "FixedResolution"), 1920, 1080, "Camera settings validation" }, null);
        group.GetType().GetMethod("AddCustomSize", flags).Invoke(group, new[] { size });
        int builtinCount = (int)group.GetType().GetMethod("GetBuiltinCount", flags).Invoke(group, null);
        int customCount = (int)group.GetType().GetMethod("GetCustomCount", flags).Invoke(group, null);
        var viewType = assembly.GetType("UnityEditor.GameView");
        var view = UnityEditor.EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex", flags).SetValue(view, builtinCount + customCount - 1);
        view.Repaint();
#endif
    }
}
