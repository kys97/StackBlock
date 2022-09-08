using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Surface : MonoBehaviour
{
    [SerializeField]private string key;

    private PointMove pointMove;
    private bool update_pos;

    void Start()
    {
        pointMove = GameObject.Find("cameraPoint").GetComponent<PointMove>();
    }

    private void OnEnable()
    {
        if (GameManager.Instance.start)
            if (GameManager.Instance.camera_dir == GameManager.Instance.Puzzle[key].dir)
                GameManager.Instance.Puzzle[key].Position(Camera.main.WorldToScreenPoint(GameManager.Instance.Puzzle[key].block.transform.position));
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
