using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public partial class FullFlowTests
{
    [UnityTest]
    public IEnumerator WinterSurfacesRenderBothSidesAndKeepProgression()
    {
        yield return EnterPuzzle("Weather", "Winter");
        var owner = GameManager.Instance;
        Object.FindAnyObjectByType<Timer>().enabled = false;
        Assert.AreEqual(8, owner.Puzzle.Count);
        Assert.IsFalse(owner.Puzzle["door"].Surface.activeSelf);
        Assert.IsFalse(owner.Puzzle["sheet"].Surface.activeSelf);
        Assert.AreEqual(6, owner.Puzzle.Values.Count(p => p.Surface.activeInHierarchy));
        var actualMaterials = owner.Puzzle.Values.SelectMany(p => p.Object.GetComponentsInChildren<Renderer>(true))
            .SelectMany(r => r.sharedMaterials).ToArray();
        foreach (var entry in owner.Puzzle)
        {
            var source = entry.Value.Surface.GetComponent<MeshFilter>();
            var material = source.GetComponent<Renderer>().sharedMaterial;
            Assert.AreEqual("StackBlock/Puzzle Surface", material.shader.name, entry.Key);
            Assert.IsTrue(material.shader.isSupported);
            Assert.AreEqual(0f, material.GetFloat("_Cull"), entry.Key);
            Assert.AreEqual(Color.black, material.color);
            Assert.IsFalse(actualMaterials.Contains(material), "Surface material must not replace an actual piece slot");
            AssertSurfaceVisibleFromBothSides(source, material, entry.Key);
        }
        var camera = Camera.main.GetComponent<PuzzleCameraController>();
        for (int side = 0; side < 4; side++)
        {
            CaptureLightingFrame("WinterSurface-fixed-" + side);
            camera.RotateRight();
            yield return WaitFor(() => !camera.IsRotating, "Winter Surface camera side");
        }
        // Uses real UI drag handlers, direction/prerequisite checks, score,
        // completion and success UI. No force-activation of dependent markers.
        yield return PlaceAvailableBlocks();
        Assert.AreEqual(8, owner.CompletedPieceCount);
        Assert.IsTrue(owner.Puzzle["door"].IsComplete);
        Assert.IsTrue(owner.Puzzle["sheet"].IsComplete);
    }

    private static void AssertSurfaceVisibleFromBothSides(MeshFilter source, Material material, string key)
    {
        var copy = new GameObject("Surface render verification"); copy.layer = 2;
        copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        copy.transform.localScale = source.transform.lossyScale;
        copy.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
        var renderer = copy.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        var cameraObject = new GameObject("Surface verification camera");
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.cullingMask = 1 << 2; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
        camera.orthographic = true; camera.nearClipPlane = .01f; camera.farClipPlane = 20;
        var target = RenderTexture.GetTemporary(256, 256, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);
        var previousActive = RenderTexture.active;
        try
        {
            Bounds local = source.sharedMesh.bounds;
            int thinAxis = new[] { 0, 1, 2 }.OrderBy(i => local.size[i]).First();
            Vector3 axis = Vector3.zero; axis[thinAxis] = 1;
            Vector3 normal = copy.transform.TransformDirection(axis).normalized;
            Vector3 center = copy.transform.TransformPoint(local.center);
            camera.orthographicSize = Mathf.Max(.02f, renderer.bounds.extents.magnitude * 1.1f);
            camera.targetTexture = target;
            foreach (int side in new[] { -1, 1 })
            {
                camera.transform.position = center + normal * side * 3;
                camera.transform.LookAt(center, Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > .9f ? Vector3.forward : Vector3.up);
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); pixels.Apply();
                int blackPixels = pixels.GetPixels32().Count(p => p.r < 30 && p.g < 30 && p.b < 30);
                Assert.Greater(blackPixels, 8, key + " must render from side " + side);
                Debug.Log("WINTER SURFACE " + key + " side=" + side + " visiblePixels=" + blackPixels);
            }
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            copy.SetActive(false); cameraObject.SetActive(false);
            Object.Destroy(pixels); Object.Destroy(copy); Object.Destroy(cameraObject);
        }
    }
}
