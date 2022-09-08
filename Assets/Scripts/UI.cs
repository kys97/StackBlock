using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class UI : MonoBehaviour
{
    public Slider timer;

    public void ToMain()
    {
        SceneManager.LoadScene("Main");
    }

    public void ToStage()
    {
        GameManager.Instance.status = GameManager.Status.Stage;
        SceneManager.LoadScene("Stage");
    }

    public void NextStage()
    {
        GameManager.Instance.stage++;
        timer.value = 10f;
        GameManager.Instance.NextPuzzle();
    }

    public void TryAgain()
    {
        timer.value = 10f;
        GameManager.Instance.NextPuzzle();
    }

    public void GameStart()
    {
        GameManager.Instance.status = GameManager.Status.Topic;
        SceneManager.LoadScene("Topic");
    }

    public void GameOver()
    {
        Application.Quit();
    }

    public void Topic()
    {
        GameManager.Instance.SetTopic(EventSystem.current.currentSelectedGameObject.name);
        GameManager.Instance.status = GameManager.Status.Stage;
        SceneManager.LoadScene("Stage");
    }

    public void BackToMain()
    {
        SceneManager.LoadScene("Main");
    }

    
}
