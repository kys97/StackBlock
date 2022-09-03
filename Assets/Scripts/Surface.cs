using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Surface : MonoBehaviour
{
    private string key;

    private void OnEnable()
    {
        if (GameManager.Instance.start)
            if (GameManager.Instance.camera_dir == GameManager.Instance.Puzzle[key].dir)
                GameManager.Instance.Puzzle[key].Position(Camera.main.WorldToScreenPoint(transform.position));
            else
                GameManager.Instance.Puzzle[key].Position(new Vector2(-3000, -3000));
    }
    private void OnDisable()
    {
        if(GameManager.Instance.start)
            GameManager.Instance.Puzzle[key].Position(new Vector2(-3000, -3000));
    }

    public void SetKey(string n) { key = n; }
}
