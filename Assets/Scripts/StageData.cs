using UnityEngine;

[CreateAssetMenu(fileName = "StageData", menuName = "StackBlock/Stage Data")]
public class StageData : ScriptableObject
{
    [SerializeField] private GameManager.Stage stage;
    [SerializeField] private GameManager.Topic topic;
    [SerializeField, Min(0f)] private float timeLimitSeconds = 20f;
    [SerializeField, Min(0f)] private float timeBonusSeconds = 5f;
    [SerializeField, Min(0)] private int pointsPerPiece = 50;
    [SerializeField] private Vector3 initialCameraPosition;
    [SerializeField] private Vector3 initialCameraEulerAngles;
    // Preserve serialized legacy values without interpreting pixels as world units.
    [SerializeField, HideInInspector] private float snapDistancePixels = 90f;
    [Tooltip("Placement anchor distance on the world XZ plane, in Unity units.")]
    [SerializeField, Min(0f)] private float snapDistanceWorld = 0.2f;
    [SerializeField] private Sprite previewImage;
    [SerializeField] private GameObject groundPrefab;

    public GameManager.Stage Stage => stage;
    public GameManager.Topic Topic => topic;
    public float TimeLimitSeconds => timeLimitSeconds;
    public float TimeBonusSeconds => timeBonusSeconds;
    public int PointsPerPiece => pointsPerPiece;
    public Vector3 InitialCameraPosition => initialCameraPosition;
    public Vector3 InitialCameraEulerAngles => initialCameraEulerAngles;
    public float SnapDistanceWorld => snapDistanceWorld;
    public Sprite PreviewImage => previewImage;
    public GameObject GroundPrefab => groundPrefab;
}
