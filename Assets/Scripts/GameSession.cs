using System;
using UnityEngine;

// Persistent selection only. Puzzle state and scene references remain in GameManager.
[DisallowMultipleComponent]
public sealed class GameSession : MonoBehaviour
{
    [SerializeField] private GameManager.Topic currentTopic;
    [SerializeField] private GameManager.Stage currentStage;
    private StageData currentStageData;
    private static GameSession instance;

    public event Action SelectionChanged;
    public GameManager.Topic Topic
    {
        get => currentTopic;
        set { if (currentTopic == value) return; currentTopic = value; SelectionChanged?.Invoke(); }
    }
    public GameManager.Stage Stage
    {
        get => currentStage;
        set { if (currentStage == value) return; currentStage = value; SelectionChanged?.Invoke(); }
    }

    public StageData CurrentStageData
    {
        get
        {
            if (currentStageData == null || currentStageData.Stage != currentStage)
            {
                StageData selected = Resources.Load<StageData>("StageData/" + currentStage);
                if (selected == null || selected.Stage != currentStage)
                    throw new InvalidOperationException("Missing or mismatched StageData for " + currentStage);
                currentStageData = selected;
            }
            return currentStageData;
        }
    }

    public void SetTopic(string name) => Topic = (GameManager.Topic)Enum.Parse(typeof(GameManager.Topic), name);
    public void SetStage(string name) => Stage = (GameManager.Stage)Enum.Parse(typeof(GameManager.Stage), name);

    // Called by the existing bootstrap only, not by individual puzzle components.
    public static GameSession GetOrCreate(GameManager.Topic initialTopic, GameManager.Stage initialStage)
    {
        if (instance == null) instance = FindAnyObjectByType<GameSession>();
        if (instance != null) return instance;
        var session = new GameObject("GameSession").AddComponent<GameSession>();
        session.currentTopic = initialTopic;
        session.currentStage = initialStage;
        return session;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticReference() => instance = null;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
