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


    //ī�޶� ��ġ ���
    public Vector3 weather_campos;
    public float weather_camrot;
    public Vector3 structure_campos;
    public float structure_camrot;


    //���� ��Ȳ ����
    public Status status;
    //���� ����
    public Topic topic;
    public void SetTopic(string n) { topic = (Topic)System.Enum.Parse(typeof(Topic), n); }
    //�������� ����
    public Stage stage;


    //���� ����
    public int score;
    //�� �ɸ� �ð� ����
    public float playing_time;
    //���� ���� ���� ����
    public bool success;


    //��� ���� ����
    public int puz_num;
    public int comlete_num;
    //���õ� ��� ����
    public string drag_block_id;
    //ī�޶� ���� ����
    public int camera_dir = 0;
    //�Ÿ� ����
    public float block_distance;
    //���� ���� �Ϸ� ����
    public bool start = false;


    //������ ���� ����
    public GameObject click_ui_prefab;
    public GameObject contents;
    public GameObject move_canvas;

    public GameObject block_parent;//����
    [HideInInspector]public GameObject ground;//��


    //�̱���
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
        //���� �ʱ�ȭ
        playing_time = 0f;
        score = 0;
        puz_num = 0;
        comlete_num = 0;
        drag_block_id = null;
        camera_dir = 0;
        ground = null;
        //��� ����Ʈ �ʱ�ȭ
        Puzzle.Clear();


        //UI �̹��� �ε�
        Sprite[] sprites = Resources.LoadAll<Sprite>("Puzzle/" + stage.ToString());
        //��� ������ �ε�
        GameObject[] parts = Resources.LoadAll<GameObject>("Puzzle/" + stage.ToString());
        puz_num = sprites.Length;

        //Dictionary�� �߰�
        for (int i = 0; i < sprites.Length; i++)
        {
            //UI ����
            click_ui_prefab.GetComponent<Image>().sprite = sprites[i];
            GameObject temp_ui = Instantiate<GameObject>(click_ui_prefab, contents.transform);
            temp_ui.GetComponent<BlockUI>().SetKey(parts[i].name);
            //��� ����
            GameObject temp_block = Instantiate<GameObject>(parts[i]);
            temp_block.transform.SetParent(block_parent.transform, false);
            //���� ���
            GameObject temp_obj = temp_block.transform.Find("object").gameObject;
            GameObject temp_surf = temp_block.transform.Find("surface").gameObject;
            temp_obj.AddComponent<BlockObj>().SetKey(parts[i].name);
            temp_surf.AddComponent<Surface>().SetKey(parts[i].name);
            temp_obj.SetActive(false);
            //ȭ�� ��ġ ���
            Vector2 temp_pos = Camera.main.WorldToScreenPoint(temp_surf.transform.position);
            //���� ���� ���
            int temp_dir = Cal_Dir(sprites[i].name);

            //���� �߰�
            Puzzle.Add(parts[i].name, new Block(temp_obj, temp_surf, sprites[i], temp_pos, temp_dir, false));
        }

        //�ʿ���� �ٴڸ� �Ⱥ��̰�
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

        //�� ����
        ground = Instantiate<GameObject>(Resources.Load<GameObject>("Ground/" + stage.ToString()));
        ground.transform.SetParent(block_parent.transform, false);

        //��� ����� �ִϸ��̼� ���� ���� ���� ����

        //���� ���� ����
        start = true;
        success = false;
    }

    public void NextPuzzle()
    {
        
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void EndTopic()
    {
        //���� �����ٴ� UI
        //���� ����â���� ���ư��� ��ư
    }
}
