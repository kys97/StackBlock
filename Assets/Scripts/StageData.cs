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
    [SerializeField, Min(0.01f)]
    [Tooltip("Degrees per second. Existing stages were migrated from 1 degree/frame at a 60 FPS reference to 60 degrees/second.")]
    private float rotationSpeedDegreesPerSecond = 60f;
    [SerializeField, Min(0f)] private float snapDistancePixels = 90f;
    [SerializeField] private Sprite previewImage;
    [SerializeField] private GameObject groundPrefab;

    public GameManager.Stage Stage => stage;
    public GameManager.Topic Topic => topic;
    public float TimeLimitSeconds => timeLimitSeconds;
    public float TimeBonusSeconds => timeBonusSeconds;
    public int PointsPerPiece => pointsPerPiece;
    public Vector3 InitialCameraPosition => initialCameraPosition;
    public Vector3 InitialCameraEulerAngles => initialCameraEulerAngles;
    public float RotationSpeedDegreesPerSecond => rotationSpeedDegreesPerSecond;
    public float SnapDistancePixels => snapDistancePixels;
    public Sprite PreviewImage => previewImage;
    public GameObject GroundPrefab => groundPrefab;
}
