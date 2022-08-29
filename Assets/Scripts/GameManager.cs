using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    enum Topic
    {
        Weather,
        Structure
    }
    enum Weather
    {
        Spring,
        Summer,
        Desert,
        Fall,
        Winter
    }
    enum Dir
    {
        F,
        R,
        B,
        L
    }

    public int stage;
    //선택된 블록 변수
    public int drag_block_id = 0;
    //카메라 방향 변수
    public int camera_dir = 0;
    //거리 변수
    public float block_distance;


    //화면 접착면 위치 리스트
    public List<Vector2> parts_pos = new List<Vector2>();
    //블록 리스트
    public List<GameObject> block_parts = new List<GameObject>();
    //블록 파츠 이미지 리스트
    public List<Sprite> block_parts_image = new List<Sprite>();


    //프리펩 생성 변수
    public GameObject click_ui_prefab;
    public GameObject move_ui_prefab;
    public GameObject contents;
    public GameObject move_canvas;

    public GameObject block_parent;
    public GameObject ground;
    public GameObject test_image;


    //싱글톤
    private static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (!_instance)
            {
                _instance = FindObjectOfType(typeof(GameManager)) as GameManager;

                if (_instance == null)
                    Debug.Log("no Singleton obj");
            }
            return _instance;
        }
    }
    private void Awake()
    {
        if (_instance == null)
            _instance = this;
        else if (_instance != this)
            Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
    }

    // Start is called before the first frame update
    void Start()
    {
        click_ui_prefab = Resources.Load<GameObject>("ClickImage");
        move_ui_prefab = Resources.Load<GameObject>("MoveImage");
        move_canvas.SetActive(false);

        //test
        NextGame();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void StartGame()
    {
        

    }

    public void NextGame()
    {
        //블록 리스트 초기화
        parts_pos.Clear();
        block_parts.Clear();
        block_parts_image.Clear();
        //스테이지 변수 Up
        stage++;
        //변수 초기화
        drag_block_id = 0;
        camera_dir = 1;
        ground = null;

        //블록 파츠 UI 생성
        char pad = '0';
        string sprite_path = stage.ToString().PadLeft(2, pad);
        Sprite[] sprites = Resources.LoadAll<Sprite>(sprite_path);
        for(int i = 0; i < sprites.Length; i++)
        {
            block_parts_image.Add(sprites[i]);
            click_ui_prefab.GetComponent<Image>().sprite = sprites[i];
            GameObject temp = Instantiate<GameObject>(click_ui_prefab, contents.transform);
            temp.GetComponent<BlockUI>().SetUiId(i + 1);
        }


        //땅 생성
        ground = Instantiate<GameObject>(Resources.Load<GameObject>("Ground/Weather/Spring"));
        ground.transform.SetParent(block_parent.transform, false);

        //블록 생성
        GameObject[] parts = Resources.LoadAll<GameObject>(sprite_path);
        for (int i = 0; i < parts.Length; i++)
        {
            GameObject temp = Instantiate<GameObject>(parts[i]);
            temp.transform.SetParent(block_parent.transform, false);
            block_parts.Add(temp);
            temp.transform.Find("object").gameObject.SetActive(false);
            temp.transform.Find("surface").gameObject.SetActive(true);
            temp.AddComponent<BlockParts>().SetBlockId(i + 1);
            parts_pos.Add(Camera.main.WorldToScreenPoint(temp.transform.GetChild(1).gameObject.transform.position));
        }
    }
}
