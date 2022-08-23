using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneControl : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnClickStartGame()
    {
        SceneManager.LoadScene("ThemeSelection");
    }

    public void OnClickQuitGame()
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            UnityEditor.EditorApplication.isPlaying = false;
        }
        else
        {
            Application.Quit();
        }

    }

    public void OnClickSeason()
    {
        SceneManager.LoadScene("Season");
    }

    public void OnClickWorld()
    {
        SceneManager.LoadScene("World");
    }
    
    public void OnClickMenuButton()
    {
        SceneManager.LoadScene("TitleScene");
    }
}
