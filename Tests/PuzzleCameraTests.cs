using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class PuzzleCameraTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject cameraObject;
    private GameObject pivot;
    private GameObject target;
    private GameManager manager;
    private StageData data;
    private PuzzleCameraController controller;

    [SetUp]
    public void Setup()
    {
        data = Object.Instantiate(Resources.Load<StageData>("StageData/Spring"));
        manager = new GameObject("Camera test owner").AddComponent<GameManager>();
        TestState.Set(manager, "start", true);
        pivot = new GameObject("Ground center");
        pivot.transform.position = new Vector3(0.1f, 0f, 0.2f);
        cameraObject = new GameObject("Camera", typeof(Camera));
        controller = cameraObject.AddComponent<PuzzleCameraController>();
        controller.Initialize(data, pivot.transform, manager);
    }

    private void Advance(float seconds) => typeof(PuzzleCameraController).GetMethod("AdvanceRotation", Private)
        .Invoke(controller, new object[] { seconds });

    private void Press(KeyCode key, bool runWholeUpdate = false)
    {
        typeof(PuzzleCameraController).GetField("getKeyDown", Private).SetValue(controller, new Func<KeyCode, bool>(candidate => candidate == key));
        controller.SendMessage(runWholeUpdate ? "Update" : "ReadKeyboardInput");
        typeof(PuzzleCameraController).GetField("getKeyDown", Private).SetValue(controller, new Func<KeyCode, bool>(_ => false));
    }

    [TestCase(KeyCode.A, 3)]
    [TestCase(KeyCode.LeftArrow, 3)]
    [TestCase(KeyCode.D, 1)]
    [TestCase(KeyCode.RightArrow, 1)]
    [TestCase(KeyCode.Q, 0)]
    [TestCase(KeyCode.E, 0)]
    public void KeyboardUpdateRoutesToSharedRequestsAndIgnoresQE(KeyCode key, int direction)
    {
        Vector3 origin = cameraObject.transform.position;
        Quaternion rotation = cameraObject.transform.rotation;
        Press(key, true);
        Assert.AreEqual(direction, controller.CurrentDirection);
        Assert.AreEqual(direction, manager.CurrentDirection);
        Assert.AreEqual(direction != 0, controller.IsRotating);
        Advance(10f);
        Vector3 keyboardPosition = cameraObject.transform.position;
        Quaternion keyboardRotation = cameraObject.transform.rotation;
        if (direction == 0)
        {
            Assert.AreEqual(origin, keyboardPosition);
            Assert.AreEqual(rotation, keyboardRotation);
            return;
        }
        controller.Initialize(data, pivot.transform, manager);
        if (direction == 3) controller.RotateLeft(); else controller.RotateRight();
        Advance(10f);
        Assert.AreEqual(keyboardPosition, cameraObject.transform.position);
        Assert.AreEqual(keyboardRotation, cameraObject.transform.rotation);
    }

    [Test]
    public void MixedKeyboardAndButtonRequestsAreIgnoredDuringRotation()
    {
        controller.RotateRight();
        for (int i = 0; i < 20; i++)
        {
            Press(KeyCode.A); Press(KeyCode.D);
            Press(KeyCode.LeftArrow); Press(KeyCode.RightArrow);
            controller.RotateLeft(); controller.RotateRight();
        }
        Assert.AreEqual(1, controller.CurrentDirection);
        Assert.AreEqual(1, manager.CurrentDirection);
        Advance(10f);
        Assert.IsFalse(controller.IsRotating);
        Assert.AreEqual(1, controller.CurrentDirection);
        Assert.That(Quaternion.Angle(Quaternion.Euler(data.InitialCameraEulerAngles), cameraObject.transform.rotation), Is.EqualTo(90f).Within(0.001f));
    }

    [TestCase(30, 60f)]
    [TestCase(60, 60f)]
    [TestCase(144, 60f)]
    [TestCase(30, 73.5f)]
    [TestCase(144, 120f)]
    public void RotationDurationUsesSecondsAtDifferentFrameRates(int fps, float speed)
    {
        typeof(StageData).GetField("rotationSpeedDegreesPerSecond", Private).SetValue(data, speed);
        controller.RotateLeft();
        int frames = 0;
        while (controller.IsRotating && frames < 1000) { Advance(1f / fps); frames++; }
        Assert.IsFalse(controller.IsRotating);
        Assert.That(frames / (float)fps, Is.EqualTo(90f / speed).Within(1f / fps + 0.0001f));
        Assert.That(Quaternion.Angle(Quaternion.Euler(data.InitialCameraEulerAngles), cameraObject.transform.rotation), Is.EqualTo(90f).Within(0.001f));
    }

    [Test]
    public void FourTurnsAndRepeatedAlternatingTurnsReturnToExactInitialPose()
    {
        Vector3 origin = cameraObject.transform.position;
        Quaternion rotation = cameraObject.transform.rotation;
        for (int cycle = 0; cycle < 25; cycle++)
        {
            for (int i = 0; i < 4; i++) { controller.RotateRight(); Advance(10f); }
            Assert.AreEqual(origin, cameraObject.transform.position);
            Assert.AreEqual(rotation, cameraObject.transform.rotation);
            for (int i = 0; i < 4; i++) { controller.RotateLeft(); Advance(10f); }
            controller.RotateLeft(); Advance(10f);
            controller.RotateRight(); Advance(10f);
            Assert.AreEqual(0, controller.CurrentDirection);
            Assert.AreEqual(origin, cameraObject.transform.position);
            Assert.AreEqual(rotation, cameraObject.transform.rotation);
        }
    }

    [Test]
    public void CompletionRefreshesScreenCoordinatesOnlyAtTheFinalPose()
    {
        cameraObject.tag = "MainCamera";
        target = new GameObject("Puzzle piece");
        target.transform.position = new Vector3(0.5f, 1f, 0.2f);
        var block = new GameManager.Block(target, target, null, new Vector2(-77, -77), 1, false);
        manager.Puzzle.Add("piece", block);
        controller.RotateRight();
        Advance(0.5f);
        Assert.AreEqual(new Vector2(-77, -77), block.ScreenPosition);
        Advance(1f);
        Assert.IsFalse(controller.IsRotating);
        Vector2 expected = controller.ControlledCamera.WorldToScreenPoint(target.transform.position);
        Assert.AreEqual(expected, block.ScreenPosition);
        block.Position(new Vector2(-55, -55));
        Advance(1f);
        Assert.AreEqual(new Vector2(-55, -55), block.ScreenPosition, "Completion callback fires once");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Object.Destroy(cameraObject);
        Object.Destroy(pivot);
        if (target != null) Object.Destroy(target);
        Object.Destroy(manager.gameObject);
        Object.Destroy(data);
        foreach (var session in Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None)) Object.Destroy(session.gameObject);
        yield return null;
    }
}

public partial class FullFlowTests
{
    [UnityTest]
    public IEnumerator CameraButtonsUseNewControllerAndIgnoreBursts()
    {
        yield return EnterPuzzle("Weather", "Spring");
        var controller = Camera.main.GetComponent<PuzzleCameraController>();
        Vector3 origin = Camera.main.transform.position;
        Quaternion rotation = Camera.main.transform.rotation;
        Click("RotateLeft");
        for (int i = 0; i < 10; i++) { Click("RotateRight"); Click("RotateLeft"); }
        Assert.AreEqual(3, controller.CurrentDirection);
        yield return WaitFor(() => !controller.IsRotating, "Left rotation complete", 8f);
        Assert.That(Quaternion.Angle(rotation, Camera.main.transform.rotation), Is.EqualTo(90f).Within(0.001f));
        Click("RotateRight");
        yield return WaitFor(() => !controller.IsRotating, "Right rotation complete", 8f);
        Assert.AreEqual(0, controller.CurrentDirection);
        Assert.AreEqual(origin, Camera.main.transform.position);
        Assert.AreEqual(rotation, Camera.main.transform.rotation);
    }
}
