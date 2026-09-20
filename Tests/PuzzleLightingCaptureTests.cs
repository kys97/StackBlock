using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public partial class FullFlowTests
{
    [UnityTest]
    public IEnumerator SavedPuzzleLightingRenders([Values("Spring", "Bigben", "Winter")] string stage)
    {
        yield return EnterPuzzle(stage == "Bigben" ? "Structure" : "Weather", stage);
        var manager = GameManager.Instance;
        Object.FindFirstObjectByType<Timer>().enabled = false;
        foreach (var block in manager.Puzzle.Values) block.Object.SetActive(true);
        foreach (var block in manager.Puzzle.Values) block.Surface.SetActive(false);
        yield return null;
        Assert.AreEqual(UnityEngine.Rendering.AmbientMode.Trilight, RenderSettings.ambientMode);
        Assert.AreEqual(1.1f, GameObject.Find("Directional Light").GetComponent<Light>().intensity);
        CaptureLightingFrame(stage + "-0-session-after");
        Click("RotateRight");
        yield return WaitFor(() => !Object.FindFirstObjectByType<PuzzleCameraController>().IsRotating, "Saved lighting side view", 8f);
        CaptureLightingFrame(stage + "-1-session-after");
        LogAssert.NoUnexpectedReceived();
    }

    // Optional visual diagnostic: operates on the isolated PlayMode scene only.
    [UnityTest, Category("LightingCapture"), Explicit("Run by name only when comparing lighting candidates")]
    public IEnumerator CapturePuzzleLighting([Values("Spring", "Bigben")] string stage)
    {
        yield return EnterPuzzle(stage == "Spring" ? "Weather" : "Structure", stage);
        var manager = GameManager.Instance;
        Object.FindFirstObjectByType<Timer>().enabled = false;
        foreach (var block in manager.Puzzle.Values) block.Object.SetActive(true);
        foreach (var block in manager.Puzzle.Values) block.Surface.SetActive(false);
        yield return null;
        Light light = GameObject.Find("Directional Light").GetComponent<Light>();
        for (int angle = 0; angle < 2; angle++)
        {
            light.intensity = 0.7f;
            light.shadowStrength = 1f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            CaptureLightingFrame(stage + "-" + angle + "-before");
            light.intensity = 1.05f;
            CaptureLightingFrame(stage + "-" + angle + "-key");
            light.shadowStrength = 0.8f;
            RenderSettings.ambientIntensity = 1.2f;
            CaptureLightingFrame(stage + "-" + angle + "-fill");
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.386f, 0.44f);
            RenderSettings.ambientEquatorColor = new Color(0.228f, 0.25f, 0.266f);
            RenderSettings.ambientGroundColor = new Color(0.141f, 0.129f, 0.105f);
            CaptureLightingFrame(stage + "-" + angle + "-gradient");
            if (angle == 0)
            {
                Click("RotateRight");
                yield return WaitFor(() => !Object.FindFirstObjectByType<PuzzleCameraController>().IsRotating, "Side view ready", 8f);
            }
        }
    }

    private static void CaptureLightingFrame(string name)
    {
        Camera camera = Camera.main;
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var target = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            pixels.Apply();
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Logs/PuzzleLighting"));
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(pixels);
        }
    }
}
