using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public enum Status { Topic, Stage, Puzzle }
    public enum Topic { Weather, Structure }
    public enum Stage { Spring, Summer, Desert, Fall, Winter, Bigben, Egypt, OperaHouse, TowerBridge }


    public class Block
    {
        public GameObject block, surface;
        public Sprite ui;
        public Vector2 pos;
        public int dir;
        public bool complete;
        
        public Block(GameObject b, GameObject s, Sprite u, Vector2 p, int d, bool c)
        {
            block = b;
            surface = s;
            ui = u;
            pos = p;
            dir = d;
            complete = c;
        }

        public void Position(Vector2 v) { pos = v; }
        public void Complete(bool c) { complete = c; }
    }
    public Dictionary<string,Block> Puzzle = new Dictionary<string, Block>();


    //카메라 위치 상수
    public Vector3 weather_campos;
    public float weather_camrot;
    public Vector3 structure_campos;
    public float structure_camrot;


    //게임 상황 변수
    public Status status;
    //주제 변수
    public Topic topic;
    public void SetTopic(string n) { topic = (Topic)System.Enum.Parse(typeof(Topic), n); }
    //스테이지 변수
    public Stage stage;


    //점수 변수
    public int score;
    //총 걸린 시간 변수
    public float playing_time;
    //퍼즐 성공 여부 변수
    public bool success;


    //블록 갯수 변수
    public int puz_num;
    public int comlete_num;
    //선택된 블록 변수
    public string drag_block_id;
    //카메라 방향 변수
    public int camera_dir = 0;
    //거리 변수
    public float block_distance;
    //퍼즐 생성 완료 변수
    public bool start = false;


    //프리펩 생성 변수
    public GameObject click_ui_prefab;
    public GameObject contents;
    public GameObject move_canvas;

    public GameObject block_parent;//퍼즐
    [HideInInspector]public GameObject ground;//땅


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
        //test
        //stage = Topic.Summer;
        //StartPuzzle();
    }

    // Update is called once per frame
    void Update()
    {
        if (start)
        {
            playing_time += Time.deltaTime;
        }
    }

    public void SetStage(string s)
    {
        stage = (Stage)System.Enum.Parse(typeof(Stage), s);
    }


    public void BlockComplete()
    {
        success = true;
        score -= (int)playing_time; 
    }

    public void BlockFail()
    {
        start = false;
        success = false; 
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

    public void Cal_Pos()
    {
        foreach (KeyValuePair<string, Block> p in Puzzle)
        {
            if(!(p.Value.complete))
                if (p.Value.dir == camera_dir)
                    p.Value.Position(Camera.main.WorldToScreenPoint(p.Value.block.transform.position));
                else
                    p.Value.Position(new Vector2(-3000, -3000));
        }
    }


    public void StartPuzzle()
    {
        //변수 초기화
        playing_time = 0f;
        score = 0;
        puz_num = 0;
        comlete_num = 0;
        drag_block_id = null;
        camera_dir = 0;
        ground = null;
        //블록 리스트 초기화
        Puzzle.Clear();


        //UI 이미지 로드
        Sprite[] sprites = Resources.LoadAll<Sprite>("Puzzle/" + stage.ToString());
        //블록 프리펩 로드
        GameObject[] parts = Resources.LoadAll<GameObject>("Puzzle/" + stage.ToString());
        puz_num = sprites.Length;

        //Dictionary에 추가
        for (int i = 0; i < sprites.Length; i++)
        {
            //UI 생성
            click_ui_prefab.GetComponent<Image>().sprite = sprites[i];
            GameObject temp_ui = Instantiate<GameObject>(click_ui_prefab, contents.transform);
            temp_ui.GetComponent<BlockUI>().SetKey(parts[i].name);
            //블록 생성
            GameObject temp_block = Instantiate<GameObject>(parts[i]);
            temp_block.transform.SetParent(block_parent.transform, false);
            //하위 블록
            GameObject temp_obj = temp_block.transform.Find("object").gameObject;
            GameObject temp_surf = temp_block.transform.Find("surface").gameObject;
            temp_obj.AddComponent<BlockObj>().SetKey(parts[i].name);
            temp_surf.AddComponent<Surface>().SetKey(parts[i].name);
            temp_obj.SetActive(false);
            //화면 위치 계산
            Vector2 temp_pos = Camera.main.WorldToScreenPoint(temp_surf.transform.position);
            //방향 변수 계산
            int temp_dir = Cal_Dir(sprites[i].name);

            //퍼즐 추가
            Puzzle.Add(parts[i].name, new Block(temp_obj, temp_surf, sprites[i], temp_pos, temp_dir, false));
        }

        //필요없는 바닥면 안보이게
        foreach(KeyValuePair<string, Block> p in Puzzle)
        {
            int n = p.Value.block.transform.childCount;
            if (n > 0)
                for (int i = 0; i < n; i++)
                {
                    string s = p.Value.block.transform.GetChild(i).name;
                    Debug.Log(s);
                    Puzzle[p.Value.block.transform.GetChild(i).name].surface.SetActive(false);
                    Puzzle[p.Value.block.transform.GetChild(i).name].Position(new Vector2(-3000, -3000));
                }
        }

        //땅 생성
        ground = Instantiate<GameObject>(Resources.Load<GameObject>("Ground/" + stage.ToString()));
        ground.transform.SetParent(block_parent.transform, false);

        //잠시 쉬어가는 애니메이션 있음 좋고 없음 말고

        //퍼즐 조립 시작
        start = true;
        success = false;
    }

    public void NextPuzzle()
    {
        
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void EndTopic()
    {
        //주제 끝났다는 UI
        //주제 선택창으로 돌아가기 버튼
    }
}
