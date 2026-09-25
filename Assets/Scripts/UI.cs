using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class UI : MonoBehaviour
{
    [SerializeField] private Button next_btn;
    private GameSession session;
    private GameSession Selection => session != null ? session : (session = GameManager.Instance.Session);

    private void Start()
    {
        session = GameManager.Instance.Session;
    }

    public void ToMain()
    {
        LoadScene("Main");
    }

    public void ToStage()
    {
        GameManager manager = GameManager.Instance;
        manager.EndPuzzle();
        manager.SetScreen(GameManager.Status.Stage);
        LoadScene("Stage");
    }

    public void NextStage()
    {
        if(Selection.Stage == GameManager.Stage.Winter || Selection.Stage == GameManager.Stage.TowerBridge)
            next_btn.enabled = false;
        else
            Selection.Stage++;
        TryAgain();
    }

    public void TryAgain()
    {
        GameManager.Instance.NextPuzzle();
    }

    public void GameStart()
    {
        GameManager.Instance.SetScreen(GameManager.Status.Topic);
        LoadScene("Topic");
    }

    public void GameOver()
    {
        Application.Quit();
    }

    public void Topic()
    {
        Selection.SetTopic(EventSystem.current.currentSelectedGameObject.name);
        GameManager.Instance.SetScreen(GameManager.Status.Stage);
        LoadScene("Stage");
    }

    public void BackToMain()
    {
        ToMain();
    }

    private static void LoadScene(string scene)
    {
        GameManager.Instance?.ResumeGame();
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
    }


}
