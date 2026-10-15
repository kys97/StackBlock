# PuzzleCameraController consolidation

## Responsibility and flow

PuzzleCameraController replaces PointMove input/target logic and CameraControl movement. It owns CurrentDirection (0..3), IsRotating, keyboard routing, 90-degree orbit targets, movement and final-pose snapping. Public RotateLeft and RotateRight are the only rotation request entry points. Left means direction -1 modulo 4 / +90 degrees around world up; Right means +1 modulo 4 / -90 degrees, matching the old piece-direction convention.

LeftBtn → RotateLeft; RightBtn → RotateRight. A/LeftArrow use RotateLeft, D/RightArrow use RotateRight. Q/E are not polled. Requests while rotating are ignored, including mixed button/keyboard requests. Keyboard input is read before movement advancement, so keys observed while still rotating at the start of the finishing Update are ignored rather than queued.

StageLoad calls ApplyInitialPose(selectedStageData) before StartPuzzle so existing initial screen-coordinate calculations use the correct camera pose. After Ground creation, it calls Initialize(selectedStageData, ground.transform, existingManager). Camera settings are implemented inside the controller; StageLoad only orders initialization and otherwise keeps its existing responsibilities.

The controller caches its Camera, StageData, Ground pivot and compatibility manager. No per-frame Find, Camera.main or GetComponent is used. A captured horizontal orbit center matches the old Ground x/z and initial camera height. Four canonical poses are calculated from the original pose, never from a previously rounded endpoint. Each frame advances by rotationSpeedDegreesPerSecond * Time.deltaTime, clamps the remaining angle, then snaps exactly to the cached direction pose when complete. Disabling during rotation snaps to its selected endpoint and cancels movement; re-enable refreshes placement coordinates.

CurrentDirection is authoritative. GameManager.camera_dir is retained and synchronized at initialization and accepted requests, preserving the old immediate direction-index update. GameManager.Cal_Pos is called once after rotation reaches and snaps to its endpoint; it is not called every movement frame. Its implementation and all placement/scoring/timing systems remain unchanged.

## StageData speed migration

The serialized `cameraRotationDegreesPerFrame` field was replaced by `rotationSpeedDegreesPerSecond`, exposed through RotationSpeedDegreesPerSecond. All nine existing assets contained 1 degree/frame and were explicitly migrated to 60 degrees/second using a **60 FPS reference assumption**. A default turn takes 1.5 seconds independently of rendering FPS. This cannot preserve every prior FPS-dependent duration simultaneously.

No FormerlySerializedAs alias is used for the old speed name because its unit differs: silently interpreting old 1 as 1 degree/second would be incorrect. All nine source assets were migrated explicitly, retaining their GUIDs and other settings. InitialCameraPosition and InitialCameraEulerAngles are unchanged. Speed and initial pose are not duplicated in the controller Inspector. Runtime movement reads the supplied StageData speed, so edits to that same asset's speed apply to subsequent movement steps.

## Files and serialized references

- Added Assets/Scripts/PuzzleCameraController.cs, Tests/PuzzleCameraTests.cs and this document.
- CameraControl.cs was deleted. Its `.meta` was moved to PuzzleCameraController.cs.meta, preserving GUID b0c0092b64cd1b04aa3f8b16e344a565 and the existing Main Camera component file IDs.
- PointMove.cs and its `.meta` were deleted. Both scenes' empty cameraPoint target GameObjects and their components were removed after checking references.
- Updated Assets/Scripts/StageLoad.cs, StageData.cs, all nine Assets/Resources/StageData/*.asset files, Assets/Scenes/Puzzle.unity and TestScene_YS.unity.
- Puzzle/EventSystem StageLoad.puzzleCamera references the existing camera component fileID 381959728. The controller controlledCamera field references the Main Camera component fileID 381959729.
- Puzzle LeftBtn/RightBtn UnityEvents now reference component 381959728 and RotateLeft/RotateRight on PuzzleCameraController. TestScene_YS equivalents now reference its camera component 932415869 and the same methods.
- Tests/FullFlowTests.cs, PuzzleLightingCaptureTests.cs, StageDataTests.cs and both validation preparers were updated. Preparers remove explicitly retired scripts from reused isolated copies so obsolete code cannot compile/run alongside the replacement. Tests/README.md and current project context were updated.

TestScene_YS is an old diagnostic scene with no StageLoad bootstrap and a previously null Ground reference; its serialized camera/button references are migrated to avoid missing scripts, but this work does not rebuild that scene's missing game initialization. The new controller safely ignores requests until Initialize supplies a pivot. The production flow is Main → Topic → Stage → Puzzle.

## Inspector

On Puzzle/Main Camera verify PuzzleCameraController → Controlled Camera = Main Camera. On Puzzle/EventSystem verify StageLoad → Puzzle Camera = that controller. On LeftBtn/RightBtn verify RotateLeft/RotateRight UnityEvents. Ground is generated at runtime and its Transform is passed by StageLoad; do not put a runtime Ground into StageData. Edit Initial Camera Position, Initial Camera Euler Angles and Rotation Speed Degrees Per Second in each StageData asset. No manual production-scene rewiring is required after these saved changes.

## Validation

PuzzleCameraTests exercises actual component methods and Unity Update input routing with an injected KeyCode predicate: A/LeftArrow/D/RightArrow mappings, Q/E inactivity, mixed input bursts, 30/60/144 FPS timestep simulations, arbitrary 73.5-degree/second speed, exact return after 4 turns and 250 repeated turns, and Cal_Pos only after endpoint arrival. These are deterministic injected-input tests, not physical OS keyboard presses. CameraButtonsUseNewControllerAndIgnoreBursts invokes the actual saved UnityEvent bindings through the existing full-flow harness. Existing full flows validate all nine initial poses, camera turns, piece dragging, correct placement, completion, timer and navigation behavior.

Manual Editor checks: start from Main; test LeftBtn/A/LeftArrow and RightBtn/D/RightArrow; press Q/E; mash keys/buttons while moving; make four same-direction turns and alternate directions; place pieces after rotation. Change StageData speed and compare turn duration (90 / speed seconds). Check both Weather and Structure initial poses and verify no missing scripts/duplicate camera controllers.

No PuzzleController, PuzzleUIController, BlockUI/Surface/BlockObj, dictionary, scoring, timer, pipeline, lighting or material refactor is included.

Final results on Unity 6000.5.8f1: Logs/puzzlecamera-results.xml contains 46 passed, 2 failed and 2 existing explicit lighting comparisons skipped (50 discovered). Both failures occurred in the existing countdown-start wait before camera assertions: CameraButtonsUseNewControllerAndIgnoreBursts and the Bigben saved-lighting capture. All 14 PuzzleCameraTests and all nine stage completion flows passed in that run. A code-unchanged rerun of the button test plus all three saved-lighting captures passed 4/4 in Logs/puzzlecamera-scene-recheck-results.xml. Together 48 distinct checks passed; the combined XML is not represented as all green. The intermittent countdown wait has not been assigned a confirmed root cause and no countdown or timeout behavior was modified. No C# compile errors, NullReferenceException or MissingReferenceException were found in either validation log. Physical OS keyboard input remains a manual Editor check; injected input routing is what the automated key tests establish.
