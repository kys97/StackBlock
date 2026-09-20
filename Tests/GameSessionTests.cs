using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class GameSessionTests
{
    [UnityTest]
    public IEnumerator LegacySerializedSelectionSeedsSessionAndSurvivesManagerReplacement()
    {
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("LegacySelectionValidation");
        yield return null;
        var manager = GameManager.Instance;
        GameSession session = manager.Session;
        Assert.AreEqual(GameManager.Topic.Structure, session.Topic);
        Assert.AreEqual(GameManager.Stage.OperaHouse, session.Stage);
        StageData data = session.CurrentStageData;
        TestState.Set(manager, "score", 123);
        Object.Destroy(manager.gameObject);
        yield return null;
        Assert.IsNotNull(session);
        manager = new GameObject("Replacement manager").AddComponent<GameManager>();
        Assert.AreSame(session, manager.Session);
        Assert.AreSame(data, manager.CurrentStageData);
        Assert.AreEqual(GameManager.Stage.OperaHouse, manager.Session.Stage);
        Assert.AreEqual(0, manager.Score, "Puzzle score must not persist inside GameSession");
    }

    [UnityTest]
    public IEnumerator DuplicateSessionDoesNotReplaceSelection()
    {
        GameSession session = GameSession.GetOrCreate(GameManager.Topic.Structure, GameManager.Stage.Egypt);
        StageData data = session.CurrentStageData;
        var duplicate = new GameObject("Duplicate session").AddComponent<GameSession>();
        yield return null;
        Assert.IsTrue(duplicate == null);
        Assert.AreSame(session, GameSession.GetOrCreate(GameManager.Topic.Weather, GameManager.Stage.Spring));
        Assert.AreEqual(GameManager.Stage.Egypt, session.Stage);
        Assert.AreSame(data, session.CurrentStageData);
        Assert.AreEqual(1, Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None).Length);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var loadedScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var cleanupScene = UnityEngine.SceneManagement.SceneManager.CreateScene("Session test cleanup");
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(cleanupScene);
        yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(loadedScene);
        foreach (var manager in Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None)) Object.Destroy(manager.gameObject);
        foreach (var session in Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None)) Object.Destroy(session.gameObject);
        foreach (var sound in Object.FindObjectsByType<Sound>(FindObjectsSortMode.None)) Object.Destroy(sound.gameObject);
        yield return null;
    }
}

public partial class FullFlowTests
{
    [UnityTest]
    public IEnumerator SessionSurvivesReturnToMainWithoutDuplicateOrSelectionReset()
    {
        yield return EnterPuzzle("Structure", "Bigben");
        GameSession session = GameManager.Instance.Session;
        StageData data = session.CurrentStageData;
        Object.FindFirstObjectByType<UI>().ToMain();
        yield return WaitFor(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Main", "Main reloaded");
        yield return null;
        Assert.AreSame(session, GameManager.Instance.Session);
        Assert.AreSame(data, session.CurrentStageData);
        Assert.AreEqual(GameManager.Topic.Structure, session.Topic);
        Assert.AreEqual(GameManager.Stage.Bigben, session.Stage);
        Assert.AreEqual(1, Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None).Length);
        Assert.AreEqual(1, Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length);
        Click("GameStart");
        yield return WaitFor(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Topic", "Topic reentered");
        yield return null;
        Click("Topic", "Weather");
        yield return WaitFor(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Stage", "Weather selection");
        yield return null;
        var button = GameObject.Find("Spring").GetComponent<UnityEngine.UI.Button>();
        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(button.gameObject);
        button.onClick.Invoke();
        yield return WaitFor(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Puzzle", "New selection opens");
        yield return null;
        yield return WaitFor(() => GameManager.Instance.HasStarted, "New puzzle countdown");
        Assert.AreSame(session, GameManager.Instance.Session);
        Assert.AreEqual(GameManager.Topic.Weather, session.Topic);
        Assert.AreEqual(GameManager.Stage.Spring, session.Stage);
        Assert.AreEqual(GameManager.Stage.Spring, session.CurrentStageData.Stage);
        AssertReferenceImage("Spring");
    }
}
