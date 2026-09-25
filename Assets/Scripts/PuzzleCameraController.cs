using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class PuzzleCameraController : MonoBehaviour
{
    [SerializeField] private Camera controlledCamera;
    private const float BaseRotationSpeedDegreesPerSecond = 90f;
    private float actualRotationSpeed;
    private GameManager owner;
    private Vector3 orbitCenter;
    private Vector3 initialOffset;
    private Quaternion initialRotation;
    private readonly Vector3[] directionPositions = new Vector3[4];
    private readonly Quaternion[] directionRotations = new Quaternion[4];
    private float currentYaw;
    private float targetYaw;
    private bool initialized;
    // A single input source, replaceable by deterministic input tests.
    private Func<KeyCode, bool> getKeyDown = Input.GetKeyDown;

    public bool IsRotating { get; private set; }
    public int CurrentDirection { get; private set; }
    public Transform RotationCenter { get; private set; }
    public Camera ControlledCamera => controlledCamera;

    private void Awake()
    {
        if (controlledCamera == null) controlledCamera = GetComponent<Camera>();
    }

    // Applied before puzzle construction so its initial screen coordinates stay correct.
    public void ApplyInitialPose(StageData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        controlledCamera.transform.SetPositionAndRotation(data.InitialCameraPosition,
            Quaternion.Euler(data.InitialCameraEulerAngles));
    }

    // Ground is created by StartPuzzle; bind its pivot immediately afterwards.
    public void Initialize(StageData data, Transform rotationCenter, GameManager manager)
    {
        if (rotationCenter == null) throw new ArgumentNullException(nameof(rotationCenter));
        ApplyInitialPose(data);
        RotationCenter = rotationCenter;
        owner = manager != null ? manager : throw new ArgumentNullException(nameof(manager));
        actualRotationSpeed = BaseRotationSpeedDegreesPerSecond * owner.CameraRotationSpeed;
        if (owner != null) owner.BindCamera(this);
        orbitCenter = rotationCenter.position;
        orbitCenter.y = data.InitialCameraPosition.y;
        initialOffset = data.InitialCameraPosition - orbitCenter;
        initialRotation = Quaternion.Euler(data.InitialCameraEulerAngles);
        directionPositions[0] = data.InitialCameraPosition;
        directionRotations[0] = initialRotation;
        for (int i = 1; i < 4; i++)
        {
            Quaternion orbit = Quaternion.AngleAxis(-90f * i, Vector3.up);
            directionPositions[i] = orbitCenter + orbit * initialOffset;
            directionRotations[i] = orbit * initialRotation;
        }
        CurrentDirection = 0;
        IsRotating = false;
        initialized = true;

    }

    public void RotateLeft() => RequestRotation(-1, 90f);
    public void RotateRight() => RequestRotation(1, -90f);

    private void RequestRotation(int directionStep, float angle)
    {
        if (!initialized || !isActiveAndEnabled || IsRotating || (owner != null && !owner.CanAcceptInput)) return;
        currentYaw = -90f * CurrentDirection;
        targetYaw = currentYaw + angle;
        CurrentDirection = (CurrentDirection + directionStep + 4) % 4;

        IsRotating = true;
    }

    private void Update()
    {
        if (!initialized) return;
        ReadKeyboardInput();
        AdvanceRotation(Time.deltaTime);
    }

    private void ReadKeyboardInput()
    {
        if (getKeyDown(KeyCode.A) || getKeyDown(KeyCode.LeftArrow)) RotateLeft();
        else if (getKeyDown(KeyCode.D) || getKeyDown(KeyCode.RightArrow)) RotateRight();
    }

    private void AdvanceRotation(float deltaTime)
    {
        if (!IsRotating || owner.IsPaused) return;
        actualRotationSpeed = BaseRotationSpeedDegreesPerSecond * owner.CameraRotationSpeed;
        float step = actualRotationSpeed * Mathf.Max(0f, deltaTime);
        currentYaw = Mathf.MoveTowards(currentYaw, targetYaw, step);
        if (currentYaw == targetYaw)
        {
            SnapToDirection();
            IsRotating = false;
            if (owner != null) owner.RecalculatePlacementPositions();
            return;
        }
        Quaternion orbit = Quaternion.AngleAxis(currentYaw, Vector3.up);
        controlledCamera.transform.SetPositionAndRotation(orbitCenter + orbit * initialOffset, orbit * initialRotation);
    }


    private void SnapToDirection()
    {
        if (controlledCamera != null)
            controlledCamera.transform.SetPositionAndRotation(directionPositions[CurrentDirection], directionRotations[CurrentDirection]);
    }

    private void OnDisable()
    {
        if (!initialized || !IsRotating) return;
        SnapToDirection();
        IsRotating = false;
    }

    private void OnEnable()
    {
        if (initialized && owner != null) owner.RecalculatePlacementPositions();
    }
}
