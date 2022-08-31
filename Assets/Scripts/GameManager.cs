using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    enum Topic { Spring, Summer,Desert, Fall, Winter, Bigben, Egypt, OperaHouse, TowerBridge }

    public struct Block
    {
        public GameObject block, surface;
        public Sprite ui;
        public Vector2 position;
        public int dir;
        public bool complete;
        
        public Block(GameObject b, GameObject s, Sprite u, Vector2 p, int d, bool c)
        {
            block = b;
            surface = s;
            ui = u;
            position = p;
            dir = d;
            complete = c;
        }
    }
    public Dictionary<string,Block> Puzzle = new Dictionary<string, Block>();

    //스테이지 변수
    Topic stage;
    //선택된 블록 변수
    public string drag_block_id;
    //카메라 방향 변수
    public int camera_dir = 0;
    //거리 변수
    public float block_distance;


    //프리펩 생성 변수
    public GameObject click_ui_prefab;
    public GameObject move_ui_prefab;
    public GameObject contents;
    public GameObject move_canvas;

    public GameObject block_parent;
    [HideInInspector]public GameObject ground;


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
        stage = 0;
        StartPuzzle();
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    private int Cal_Dir(string n)
    {
        n = n.Substring(n.Length - 1, 1);
        switch (n)
        {
            case "F": return 0;
            case "R": return 1;
            case "B": return 2;
            case "L": return 3;

        }
        return -1;
    }


    private void StartPuzzle()
    {
        //변수 초기화
        drag_block_id = null;
        camera_dir = 1;
        ground = null;
        //블록 리스트 초기화
        Puzzle.Clear();


        //UI 이미지 로드
        Sprite[] sprites = Resources.LoadAll<Sprite>("Puzzle/" + stage.ToString());
        //블록 프리펩 로드
        GameObject[] parts = Resources.LoadAll<GameObject>("Puzzle/" + stage.ToString());
        //Dictionary에 추가
        for (int i = 0; i < sprites.Length; i++)
        {
            //UI 생성
            click_ui_prefab.GetComponent<Image>().sprite = sprites[i];
            GameObject temp_ui = Instantiate<GameObject>(click_ui_prefab, contents.transform);
            //블록 생성
            GameObject temp_block = Instantiate<GameObject>(parts[i]);
            temp_block.transform.SetParent(block_parent.transform, false);
            int temp_dir = Cal_Dir(sprites[i].name);
            //퍼즐 추가
            Puzzle.Add(parts[i].name, new Block(temp_block.transform.Find("object").gameObject,
                temp_block.transform.Find("surface").gameObject, sprites[i], new Vector2(-100, -100),
                temp_dir, false));
        }
        //땅 생성
        ground = Instantiate<GameObject>(Resources.Load<GameObject>("Ground/" + stage.ToString()));
        ground.transform.SetParent(block_parent.transform, false);


        /*UI
            for (int i = 0; i < sprites.Length; i++)
            {
                block_parts_image.Add(sprites[i]);
                click_ui_prefab.GetComponent<Image>().sprite = sprites[i];
                GameObject temp = Instantiate<GameObject>(click_ui_prefab, contents.transform);
                temp.GetComponent<BlockUI>().SetUiId(i + 1);
            }
            
            for (int i = 0; i < parts.Length; i++)
            {
                GameObject temp = Instantiate<GameObject>(parts[i]);
                temp.transform.SetParent(block_parent.transform, false);
                block_parts.Add(temp);
                temp.transform.Find("object").gameObject.SetActive(false);
                temp.transform.Find("surface").gameObject.SetActive(true);
                temp.AddComponent<BlockParts>().SetBlockId(i + 1);
                parts_pos.Add(Camera.main.WorldToScreenPoint(temp.transform.GetChild(1).gameObject.transform.position));
            }*/
    }

    public void PuzzleClear()
    {
        //퍼즐 클리어 화면 UI

    }

    public void NextPuzzle()
    {
        //다음 스테이지
        if (stage == Topic.Winter || stage == Topic.TowerBridge)
            EndTopic();
        else 
        { 
            //스테이지 변수
            stage++;

            //UI 셋팅

            //게임 시작
            StartPuzzle();
        }
    }

    public void EndTopic()
    {
        //주제 끝났다는 UI
        //주제 선택창으로 돌아가기 버튼
    }
}
