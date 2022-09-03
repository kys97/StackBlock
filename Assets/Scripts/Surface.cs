using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Surface : MonoBehaviour
{
    private string key;
    private GameObject test_text;

    private void Start()
    {
        test_text = Instantiate<GameObject>(GameManager.Instance.test_text, GameManager.Instance.canvas.transform);
        test_text.transform.position = transform.position;
    }
    private void Update()
    {
        if (GameManager.Instance.start && gameObject.activeSelf == true)
        {
            //GameManager.Instance.Puzzle[key].Position(Camera.main.WorldToScreenPoint(transform.position));
            test_text.GetComponent<Text>().text = key + "\n" + GameManager.Instance.Puzzle[key].position;
            test_text.transform.position = Camera.main.WorldToScreenPoint(transform.position);
            Debug.Log(key + "현재 화면 위치 : " + test_text.transform.position);
            
            GameManager.Instance.Puzzle[key].position = (Camera.main.WorldToScreenPoint(transform.position));
            Debug.Log(key + "Dictionary 위치 : " + GameManager.Instance.Puzzle[key].);
        }
    }

    private void OnEnable()
    {
        if (GameManager.Instance.start)
        {
            GameManager.Instance.Puzzle[key].Position(Camera.main.WorldToScreenPoint(transform.position));
            Instantiate<GameObject>(GameManager.Instance.test_text, GameManager.Instance.canvas.transform);
            GameManager.Instance.test_text.GetComponent<Text>().text = key + "\n" + GameManager.Instance.Puzzle[key].position;
            GameManager.Instance.test_text.transform.position = GameManager.Instance.Puzzle[key].position;
        }
    }
    private void OnDisable()
    {
        if(GameManager.Instance.start)
            GameManager.Instance.Puzzle[key].Position(new Vector2(-3000, -3000));
    }

    public void SetKey(string n) { key = n; }
}
