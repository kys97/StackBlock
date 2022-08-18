using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    //스테이지?맵? 변수
    public int stage = 0;
    //스테이지?맵? 블록 변수
    public GameObject block;
    //선택된 블록 변수
    public int drag_block_id = 0;
    //카메라 방향 변수
    public int camera_dir = 0;


    //화면 블록 위치 리스트
    public List<Vector2> block_pos = new List<Vector2>();
    //블록 리스트
    public List<GameObject> block_parts = new List<GameObject>();
    //블록 파츠 이미지 리스트
    public List<Sprite> block_parts_image = new List<Sprite>();
    //블록 거리 리스트
    public List<float> block_distance = new List<float>();

    //프리펩 생성 변수
    public GameObject click_ui_prefab;
    public GameObject move_ui_prefab;
    public GameObject contents;
    public GameObject move_canvas;
    public GameObject block_parent;
    private GameObject block_script;
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
        block_pos.Clear();
        block_parts.Clear();
        block_parts_image.Clear();
        //스테이지 변수 Up
        stage++;
        //변수 초기화
        drag_block_id = 0;
        camera_dir = 1;

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

        
        //블록 생성
        GameObject[] parts = Resources.LoadAll<GameObject>(sprite_path);
        for (int i = 0; i < parts.Length; i++)
        {
            block_parts.Add(parts[i]);
            GameObject temp = Instantiate<GameObject>(parts[i]);
            temp.transform.SetParent(block_parent.transform, false);
            temp.AddComponent<BlockParts>().SetBlockId(i + 1);
            temp.SetActive(false);
            block_pos.Add(Camera.main.WorldToScreenPoint(temp.transform.position));
        }
    }
}
