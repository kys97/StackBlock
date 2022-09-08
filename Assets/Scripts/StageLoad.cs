using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class StageLoad : MonoBehaviour
{
    public GameObject stage_btn_prefab;
    public GameObject contents;

    public GameObject block_par;
    public GameObject move_can;

    public GameObject success_P;
    public GameObject score_contents;
    public GameObject score_prefab;

    // Start is called before the first frame update
    void Start()
    {
        if(GameManager.Instance.status == GameManager.Status.Stage)//스테이지 선택 화면
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>("UI/" + GameManager.Instance.topic.ToString());
            for (int i = 0; i < sprites.Length; i++)
            {
                //UI 생성
                stage_btn_prefab.GetComponent<Image>().sprite = sprites[i];
                GameObject temp_ui = Instantiate<GameObject>(stage_btn_prefab, contents.transform);
                temp_ui.name = sprites[i].name.Substring(1, sprites[i].name.Length - 1);
                temp_ui.GetComponent<Button>().onClick.AddListener(delegate ()
                {
                    GameObject click_btn = EventSystem.current.currentSelectedGameObject;
                    GameManager.Instance.SetStage(click_btn.name);
                    GameManager.Instance.status = GameManager.Status.Puzzle;
                    SceneManager.LoadScene("Puzzle");
                });
            }
        }
        else if(GameManager.Instance.status == GameManager.Status.Puzzle)//퍼즐 화면
        {
            GameManager.Instance.contents = contents;
            GameManager.Instance.block_parent = block_par;
            GameManager.Instance.move_canvas = move_can;
            if (GameManager.Instance.topic == GameManager.Topic.Weather)
            {
                Camera.main.transform.position = GameManager.Instance.weather_campos;
                Camera.main.transform.localEulerAngles = new Vector3(GameManager.Instance.weather_camrot,0,0);
            }
            else if (GameManager.Instance.topic == GameManager.Topic.Structure)
            {
                Camera.main.transform.position = GameManager.Instance.structure_campos;
                Camera.main.transform.localEulerAngles = new Vector3(GameManager.Instance.structure_camrot,0,0);
            }
            GameManager.Instance.StartPuzzle();
        }
    }

    private void Update()
    {
        if (GameManager.Instance.status == GameManager.Status.Puzzle)
        {
            if(GameManager.Instance.success && GameManager.Instance.start)
            {
                success_P.SetActive(true);
                GameManager.Instance.start = false;
                //점수 표시
                Sprite[] sprites = Resources.LoadAll<Sprite>("UI/Number");
                string s = GameManager.Instance.score.ToString();
                for(int i = 0; i < s.Length; i++)
                {
                    Debug.Log(int.Parse(s.Substring(i, 1)));
                    score_prefab.GetComponent<Image>().sprite = sprites[int.Parse(s.Substring(i,1))];
                    Instantiate<GameObject>(score_prefab, score_contents.transform);
                }
                
            }
        }
    }
}
