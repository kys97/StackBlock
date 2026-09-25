using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class GameManager : MonoBehaviour
{
    public enum Status { Topic, Stage, Puzzle }
    public enum Topic { Weather = 0, Structure = 1 }
    public enum Stage { Spring = 0, Summer = 1, Desert = 2, Fall = 3, Winter = 4, Bigben = 5, Egypt = 6, OperaHouse = 7, TowerBridge = 8 }

    public sealed class Block
    {
        public GameObject Object { get; }
        public GameObject Surface { get; }
        public Sprite Image { get; }
        public Vector2 ScreenPosition { get; private set; }
        public int Direction { get; }
        public bool IsComplete { get; private set; }

        public Block(GameObject pieceObject, GameObject surface, Sprite image, Vector2 screenPosition, int direction, bool isComplete)
        {
            Object = pieceObject;
            Surface = surface;
            Image = image;
            ScreenPosition = screenPosition;
            Direction = direction;
            IsComplete = isComplete;
        }

        public void Position(Vector2 position) => ScreenPosition = position;
        internal void Complete(bool isComplete) => IsComplete = isComplete;
    }
    public readonly Dictionary<string,Block> Puzzle = new Dictionary<string, Block>();


    [SerializeField] private Status status;
    public Status CurrentScreen => status;
    public void SetScreen(Status screen) => status = screen;
    [SerializeField, FormerlySerializedAs("topic")]
    [Tooltip("Initial selection used only when creating GameSession. Live selection belongs to GameSession.")]
    private Topic initialTopic;
    [SerializeField, FormerlySerializedAs("stage")]
    [Tooltip("Initial selection used only when creating GameSession. Live selection belongs to GameSession.")]
    private Stage initialStage;
    private GameSession session;

    public GameSession Session => session != null ? session : (session = GameSession.GetOrCreate(initialTopic, initialStage));

    public StageData CurrentStageData => Session.CurrentStageData;

    [Header("Camera Settings")]
    [SerializeField, Range(1, 10)] private int cameraRotationSpeed = 3;
    public int CameraRotationSpeed => Mathf.Clamp(cameraRotationSpeed, 1, 10);

    public bool IsPaused { get; private set; }
    public event System.Action<bool> PauseChanged;

    public void PauseGame()
    {
        if (IsPaused || status != Status.Puzzle || !start || success) return;
        IsPaused = true;
        Time.timeScale = 0f;
        PauseChanged?.Invoke(true);
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
        if (!IsPaused) return;
        IsPaused = false;
        PauseChanged?.Invoke(false);
    }

    public void TogglePause()
    {
        if (IsPaused) ResumeGame();
        else PauseGame();
    }

    [SerializeField] private int score;
    public int Score => score;
    [SerializeField, FormerlySerializedAs("playing_time")] private float playingTime;
    public float PlayingTime => playingTime;
    [SerializeField] private bool success;
    public bool IsSuccessful => success;
    [SerializeField, FormerlySerializedAs("puz_num")] private int totalPieceCount;
    public int TotalPieceCount => totalPieceCount;
    [SerializeField, FormerlySerializedAs("comlete_num")] private int completedPieceCount;
    public int CompletedPieceCount => completedPieceCount;
    public event System.Action<int> PieceCompleted;
    private void NotifyPieceCompleted() => PieceCompleted?.Invoke(completedPieceCount);
    private PuzzleCameraController puzzleCamera;
    public int CurrentDirection => puzzleCamera != null ? puzzleCamera.CurrentDirection : 0;
    public void BindCamera(PuzzleCameraController controller) => puzzleCamera = controller;
    [SerializeField] private bool start = false;
    public bool HasStarted => start;
    [SerializeField] private GameObject click_ui_prefab;
    [SerializeField] private GameObject contents;
    public GameObject PieceContent => contents;
    [SerializeField] private GameObject move_canvas;
    public GameObject DragCanvas => move_canvas;

    [SerializeField] private GameObject block_parent;
    public GameObject BlockParent => block_parent;
    [SerializeField] private GameObject ground;
    public GameObject Ground => ground;
    public void BindPuzzleScene(GameObject pieceContent, GameObject blockParent, GameObject dragCanvas)
    {
        contents = pieceContent;
        block_parent = blockParent;
        move_canvas = dragCanvas;
    }

    private static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (!_instance)
            {
                _instance = FindAnyObjectByType<GameManager>();

            }
            return _instance;
        }
    }
    private void Awake()
    {
        if (_instance == null)
            _instance = this;
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
        session = Session;
        ResumeGame();
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (_instance == this) SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        ResumeGame();
        if (scene.name != "Puzzle") start = false;
    }


    void Update()
    {
        if (start && !IsPaused)
        {
            playingTime += Time.deltaTime;
        }
    }


    private void BlockComplete()
    {
        success = true;
        start = false;
        score -= (int)playingTime;
        PuzzleSucceeded?.Invoke();
    }

    public event System.Action PuzzleSucceeded;
    public bool CanAcceptInput => start && !success && !IsPaused;

    public bool TryCompletePiece(string key, Vector2 dropPosition)
    {
        if (!CanAcceptInput || !Puzzle.TryGetValue(key, out Block piece) || piece.IsComplete
            || !piece.Surface.activeInHierarchy || piece.Direction != CurrentDirection
            || Vector2.Distance(piece.ScreenPosition, dropPosition) > CurrentStageData.SnapDistancePixels)
            return false;

        piece.Complete(true);
        piece.Object.SetActive(true);
        completedPieceCount++;
        score += CurrentStageData.PointsPerPiece;
        NotifyPieceCompleted();
        if (completedPieceCount == totalPieceCount) BlockComplete();
        return true;
    }

    public void BlockFail()
    {
        start = false;
        success = false;
        ResumeGame();
    }

    private int GetSpriteDirection(string n)
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

    public void RecalculatePlacementPositions()
    {
        Camera camera = puzzleCamera != null ? puzzleCamera.ControlledCamera : Camera.main;
        if (camera == null) return;
        foreach (KeyValuePair<string, Block> p in Puzzle)
        {
            if(!(p.Value.IsComplete))
                if (p.Value.Direction == CurrentDirection)
                    p.Value.Position(camera.WorldToScreenPoint(p.Value.Object.transform.position));
                else
                    p.Value.Position(new Vector2(-3000, -3000));
        }
    }

    private Coroutine readyCountdown;
    private GameObject countdownPanel;
    private Image countdownImage;
    private Sprite[] countdownSprites;

    public void ConfigureCountdown(GameObject panel, Image numberImage)
    {
        if (panel == null || numberImage == null)
            throw new System.InvalidOperationException("StageLoad requires a countdown panel and number image.");

        countdownPanel = panel;
        countdownImage = numberImage;
        countdownSprites = new Sprite[3];
        for (int i = 0; i < countdownSprites.Length; i++)
        {
            countdownSprites[i] = Resources.Load<Sprite>("UI/Number/" + (3 - i));
            if (countdownSprites[i] == null)
                throw new System.InvalidOperationException("Missing countdown sprite: UI/Number/" + (3 - i));
        }

        countdownImage.sprite = countdownSprites[0];
        countdownPanel.SetActive(true);
    }

    public void StartPuzzle()
    {
        ResetPuzzleState();
        LoadPuzzleBlocks();
        HideDependentSurfaces();
        CreateGround();
    }

    // Release persistent manager references before the scene destroys its puzzle objects.
    // Selection remains in GameSession so the Stage screen keeps the current topic.
    public void EndPuzzle()
    {
        ResetPuzzleState(); // Resumes time first, then cancels countdown and clears progress.
        puzzleCamera = null;
        contents = null;
        block_parent = null;
        move_canvas = null;
        countdownPanel = null;
        countdownImage = null;
        countdownSprites = null;
    }

    private void ResetPuzzleState()
    {
        ResumeGame();
        CancelReadyCountdown();
        start = false;
        success = false;
        playingTime = 0f;
        score = 0;
        totalPieceCount = 0;
        completedPieceCount = 0;

        ground = null;
        Puzzle.Clear();
    }

    private void LoadPuzzleBlocks()
    {
        string resourcePath = "Puzzle/" + Session.Stage;
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        GameObject[] parts = Resources.LoadAll<GameObject>(resourcePath);
        totalPieceCount = sprites.Length;
        Camera puzzleCamera = Camera.main;

        // Preserve the existing resource ordering and prefab hierarchy contract.
        for (int i = 0; i < sprites.Length; i++)
        {
            CreateBlockUI(parts[i].name, sprites[i]);
            CreatePuzzleBlock(parts[i], sprites[i], puzzleCamera);
        }
    }

    private void CreateBlockUI(string key, Sprite sprite)
    {
        GameObject blockUI = Instantiate(click_ui_prefab, contents.transform);
        blockUI.GetComponent<Image>().sprite = sprite;
        blockUI.GetComponent<BlockUI>().SetKey(key);
    }

    private void CreatePuzzleBlock(GameObject prefab, Sprite sprite, Camera puzzleCamera)
    {
        GameObject instance = Instantiate(prefab);
        instance.transform.SetParent(block_parent.transform, false);

        GameObject block = instance.transform.Find("object").gameObject;
        GameObject surface = instance.transform.Find("surface").gameObject;
        block.AddComponent<BlockObj>().SetKey(prefab.name);
        surface.AddComponent<Surface>().SetKey(prefab.name);
        block.SetActive(false);

        Vector2 position = puzzleCamera.WorldToScreenPoint(surface.transform.position);
        Puzzle.Add(prefab.name, new Block(block, surface, sprite, position, GetSpriteDirection(sprite.name), false));
    }

    private void HideDependentSurfaces()
    {
        foreach (Block block in Puzzle.Values)
        {
            foreach (Transform child in block.Object.transform)
            {
                Block dependentBlock = Puzzle[child.name];
                dependentBlock.Surface.SetActive(false);
                dependentBlock.Position(new Vector2(-3000, -3000));
            }
        }
    }

    private void CreateGround()
    {
        ground = Instantiate(CurrentStageData.GroundPrefab);
        ground.transform.SetParent(block_parent.transform, false);
    }

    public void ReadyCount()
    {
        if (start || readyCountdown != null)
            return;

        readyCountdown = StartCoroutine(CountDownToStart());
    }

    private IEnumerator CountDownToStart()
    {
        // The manager survives scene changes; do not start a puzzle after leaving it.
        Scene puzzleScene = SceneManager.GetActiveScene();
        for (int i = 0; i < countdownSprites.Length; i++)
        {
            countdownImage.sprite = countdownSprites[i];
            yield return new WaitForSeconds(1f);
            if (SceneManager.GetActiveScene() != puzzleScene || countdownPanel == null || countdownImage == null)
            {
                readyCountdown = null;
                yield break;
            }
        }

        countdownPanel.SetActive(false);
        success = false;
        start = true;
        readyCountdown = null;
    }

    private void CancelReadyCountdown()
    {
        if (countdownPanel != null)
            countdownPanel.SetActive(false);

        if (readyCountdown == null)
            return;

        StopCoroutine(readyCountdown);
        readyCountdown = null;
    }

    private void OnDisable()
    {
        CancelReadyCountdown();
        if (_instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ResumeGame();
    }

    public void NextPuzzle()
    {
        ResumeGame();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

}
