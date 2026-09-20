using UnityEngine;
using UnityEngine.UI;

using UnityEngine.SceneManagement;

public class StageLoad : MonoBehaviour
{
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private Image countdownImage;
    [SerializeField] private Timer puzzleTimerUI;
    [SerializeField] private PuzzleCameraController puzzleCamera;

    private bool isStageSelected;
    public bool IsStageSelected => isStageSelected;
    private GameManager owner;
    private RectTransform leftCloudRect;
    private RectTransform rightCloudRect;
    [SerializeField] private GameObject left_cloud;
    public GameObject LeftCloud => left_cloud;
    [SerializeField] private GameObject right_cloud;
    public GameObject RightCloud => right_cloud;

    [SerializeField] private GameObject stage_btn_prefab;
    [SerializeField] private GameObject contents;

    [SerializeField] private GameObject block_par;
    [SerializeField] private GameObject move_can;

    [SerializeField] private GameObject success_P;
    public GameObject SuccessPanel => success_P;
    [SerializeField] private GameObject score_contents;
    [SerializeField] private GameObject score_prefab;

    void Start()
    {
        owner = GameManager.Instance;
        leftCloudRect = left_cloud != null ? left_cloud.GetComponent<RectTransform>() : null;
        rightCloudRect = right_cloud != null ? right_cloud.GetComponent<RectTransform>() : null;
        GameSession session = owner.Session;
        if(owner.CurrentScreen == GameManager.Status.Stage)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>("UI/" + session.Topic.ToString());
            for (int i = 0; i < sprites.Length; i++)
            {

                GameObject stageButton = Instantiate(stage_btn_prefab, contents.transform);
                stageButton.GetComponent<Image>().sprite = sprites[i];
                stageButton.name = sprites[i].name.Substring(1, sprites[i].name.Length - 1);
                stageButton.GetComponent<Button>().onClick.AddListener(delegate ()
                {
                    isStageSelected = true;

                    session.SetStage(stageButton.name);
                });
            }
        }
        else if(owner.CurrentScreen == GameManager.Status.Puzzle)
        {
            owner.BindPuzzleScene(contents, block_par, move_can);
            StageData stageData = session.CurrentStageData;
            puzzleCamera.ApplyInitialPose(stageData);
            owner.StartPuzzle();
            puzzleCamera.Initialize(stageData, owner.Ground.transform, owner);
            puzzleTimerUI.Initialize(owner, stageData);
            owner.ConfigureCountdown(countdownPanel, countdownImage);
            owner.PuzzleSucceeded += ShowSuccess;
        }
    }

    // Includes the full child-image bounds (up to 2371 units from the root).
    private const float CloudOpenPosition = 2600f;
    private const float CloudClosedPosition = 480f;
    private const float CloudSpeed = 1000f;

    private bool MoveClouds(float leftX, float rightX)
    {
        RectTransform left = leftCloudRect;
        RectTransform right = rightCloudRect;
        Vector2 leftTarget = new Vector2(leftX, 0f);
        Vector2 rightTarget = new Vector2(rightX, 0f);
        float step = CloudSpeed * Time.deltaTime;
        left.anchoredPosition = Vector2.MoveTowards(left.anchoredPosition, leftTarget, step);
        right.anchoredPosition = Vector2.MoveTowards(right.anchoredPosition, rightTarget, step);
        return left.anchoredPosition == leftTarget && right.anchoredPosition == rightTarget;
    }

    private void Update()
    {
        if (owner.CurrentScreen == GameManager.Status.Stage && isStageSelected)
        {
            if (MoveClouds(-CloudClosedPosition, CloudClosedPosition))
            {
                owner.SetScreen(GameManager.Status.Puzzle);
                SceneManager.LoadScene("Puzzle");
            }
        }
        else if (owner.CurrentScreen == GameManager.Status.Puzzle)
        {
            if (left_cloud != null && right_cloud != null
                && MoveClouds(-CloudOpenPosition, CloudOpenPosition))
            {
                Destroy(left_cloud);
                Destroy(right_cloud);
                left_cloud = null;
                right_cloud = null;
                owner.ReadyCount();
            }

        }
    }
    private void ShowSuccess()
    {
        success_P.SetActive(true);
        Sprite[] digits = Resources.LoadAll<Sprite>("UI/Number");
        foreach (char digit in owner.Score.ToString())
        {

            GameObject scoreDigit = Instantiate(score_prefab, score_contents.transform);
            scoreDigit.GetComponent<Image>().sprite = digits[int.Parse(digit.ToString())];
        }
    }

    private void OnDestroy()
    {
        if (owner != null) owner.PuzzleSucceeded -= ShowSuccess;
    }
}
