using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class StartupRegressionTests
{
    private GameManager manager;
    private GameObject panel;
    private GameObject surfaceObject;


    [SetUp]
    public void SetUp()
    {
        manager = new GameObject("Test manager").AddComponent<GameManager>();
        panel = new GameObject("Ready", typeof(RectTransform));
    }

    private Image ConfigureCountdown()
    {
        var image = new GameObject("Number", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(panel.transform);
        manager.ConfigureCountdown(panel, image);
        return image;
    }

    [UnityTest]
    public IEnumerator CountdownDisplaysThreeTwoOneThenHidesPanel()
    {
        Image image = ConfigureCountdown();
        manager.ReadyCount();
        manager.ReadyCount(); // Duplicate requests must not create a second countdown.
        Assert.AreEqual("3", image.sprite.name);
        Assert.IsFalse(manager.HasStarted);
        yield return new WaitForSeconds(1.1f);
        Assert.AreEqual("2", image.sprite.name);
        Assert.IsTrue(panel.activeSelf);
        Assert.IsFalse(manager.HasStarted);
        yield return new WaitForSeconds(1.1f);
        Assert.AreEqual("1", image.sprite.name);
        Assert.IsFalse(manager.HasStarted);
        yield return new WaitForSeconds(1.1f);
        Assert.IsFalse(panel.activeSelf);
        Assert.IsTrue(manager.HasStarted);
    }

    [UnityTest]
    public IEnumerator DisablingManagerCancelsCountdown()
    {
        ConfigureCountdown();
        manager.ReadyCount();
        manager.enabled = false;
        yield return new WaitForSeconds(3.2f);
        Assert.IsFalse(panel.activeSelf);
        Assert.IsFalse(manager.HasStarted);
    }

    [UnityTest]
    public IEnumerator SurfaceCanDisableAfterManagerIsDestroyed()
    {
        surfaceObject = new GameObject("surface");
        Surface surface = surfaceObject.AddComponent<Surface>();
        surface.SetKey("test");
        manager.Puzzle.Add("test", new GameManager.Block(surfaceObject, surfaceObject, null, Vector2.zero, 0, false));
        TestState.Set(manager, "start", true);
        Object.Destroy(manager.gameObject);
        yield return null;
        surfaceObject.SetActive(false);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void SurfaceDisableHidesTargetButDoesNotTouchReplacementBlock()
    {
        surfaceObject = new GameObject("surface");
        Surface surface = surfaceObject.AddComponent<Surface>();
        surface.SetKey("test");
        var block = new GameManager.Block(surfaceObject, surfaceObject, null, Vector2.zero, 0, false);
        manager.Puzzle.Add("test", block);
        TestState.Set(manager, "start", true);
        surfaceObject.SetActive(false);
        Assert.AreEqual(new Vector2(-3000, -3000), block.ScreenPosition);
        surfaceObject.SetActive(true);
        var replacement = new GameManager.Block(panel, panel, null, Vector2.one, 0, false);
        manager.Puzzle["test"] = replacement;
        surfaceObject.SetActive(false);
        Assert.AreEqual(Vector2.one, replacement.ScreenPosition);
    }

    [Test]
    public void UninitializedSurfaceCanDisable()
    {
        surfaceObject = new GameObject("surface");
        surfaceObject.AddComponent<Surface>();
        surfaceObject.SetActive(false);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (surfaceObject != null) Object.Destroy(surfaceObject);
        if (manager != null) Object.Destroy(manager.gameObject);
        foreach (var session in Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None)) Object.Destroy(session.gameObject);
        if (panel != null) Object.Destroy(panel);

        yield return null;
    }

}
