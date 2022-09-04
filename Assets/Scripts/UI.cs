using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UI : MonoBehaviour
{
    public void GameStart()
    {
        SceneManager.LoadScene("Topic");
    }

    public void GameOver()
    {
        Application.Quit();
    }
    public void Setting()
    {

    }
}
