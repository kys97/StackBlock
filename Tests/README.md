# Startup regression checks

`StartupRegressionTests.cs` runs in an isolated Unity project so the main game's default assembly and open Editor session are not changed.

1. Run `Tests/PrepareStartupValidation.ps1` from PowerShell.
2. Run Unity 6000.5.8f1 with `-batchmode -nographics -projectPath <repository>/Temp/StartupValidation -runTests -testPlatform PlayMode -testResults <repository>/Logs/startup-results.xml -logFile <repository>/Logs/startup-tests.log`.
3. Inspect the XML result. Do not add `-quit`; the test runner exits after completion.

Coverage: 3 → 2 → 1 images, hidden panel before gameplay starts, duplicate countdown requests, cancellation on manager disable, surface disable after manager destruction, disabling an uninitialized surface, and avoiding changes to a replacement block under an old key.

Scene integration check (manual): reopen the saved Puzzle scene after exiting Play Mode if it was open during this edit. Its StageLoad component must reference Canvas/Ready and its child image named 3. Start through Main → Topic → Stage. After the clouds open, verify the countdown changes once per second and the overlay disappears after 1. Retry, leave during countdown, and stop Play Mode with no Surface.OnDisable exceptions.

## Full game scene tests

Run `Tests/PrepareFullFlowValidation.ps1` to copy the actual Assets, Packages and ProjectSettings to `Temp/FullFlowValidation`, preserving asset GUIDs. Then run Unity with the same test arguments above, using that project path and separate fullflow result/log filenames. The copy uses a runtime assembly definition solely to let the test assembly reference game scripts; production assembly boundaries are unchanged.

`FullFlowTests.cs` loads Main, clicks the real UI callbacks to reach Topic/Stage/Puzzle, checks loaded objects for missing scripts, waits for countdown completion, dispatches drag events to instantiated block UI, and invokes the real camera buttons. It covers all nine stage assets, completion, next-stage navigation, retry, timeout/failure and return to selection. Headless automation exercises scene behavior and callbacks; it does not establish rendered appearance or physical mouse hit testing.

For Stage back-button hit testing, run without `-nographics` and add `-testFilter StageBack`. These two cases query EventSystem.RaycastAll at the Back and generated stage-button centers, dispatch clicks to the first raycast hit and verify returning/re-entering for both topics. They must not bypass the raycaster with direct onClick invocation at those tested buttons.

Puzzle reference-image coverage is included in the stage-flow tests: every stage's loaded sprite is checked, along with preserveAspect, disabled raycasts, bottom-left anchors/pivot and 24-unit margins. Next-stage and retry paths also check the image. `ReferenceImageObservesStageChangesWithoutChangingPuzzle` changes the selected topic/stage in an already loaded scene and checks automatic image refresh while score, completion count and the existing puzzle collection remain unchanged.

`PuzzleTimerTests.cs` replaces the former Slider-authoritative timer test. It checks a standalone model and the UI bridge with a transient StageData (30.5-second limit, 2.5-second bonus), start/stop/reset, zero-time expiry, cap, duplicate/coalesced completion notifications, missing Slider and Slider tampering. Real assets remain 20 seconds / 5 bonus seconds. `RealPieceRequestsOneTimerBonus` checks the actual BlockUI event path; full flows check countdown, success freeze and timeout/retry. See `Docs/AI/PuzzleTimerMigration.md`.

`StageDataTests.cs` verifies all nine asset settings, prefab asset references and the central cached selection. `PieceAndCameraReadNonDefaultStageSettings` uses a transient asset copy to check a 40-pixel snap range, 73-point reward and 120-degree/second rotation through actual Puzzle scene components. Full-flow entry checks also verify initial camera transforms, Slider limits and Ground instances. No source StageData asset is mutated by tests.

`PuzzleLightingCaptureTests.cs` adds saved-scene camera captures for Spring, Bigben and Winter to the full suite (rendering required). Optional `CapturePuzzleLighting` comparisons are marked Explicit and only run when directly selected by name. See `Docs/AI/PuzzleLighting.md` for settings, capture limitations and comparison evidence.

`GameSessionTests.cs` adds serialized Topic/Stage migration, manager-independent session lifetime, duplicate rejection and return-to-Main/reselection integration coverage. Existing fixture teardown destroys its test sessions; configuration override tests now inject their transient data into the session cache. Current lighting and ownership are documented in `Docs/AI/GameSessionMigration.md`.

`PuzzleCameraTests.cs` covers key routing through injected input, Q/E removal, busy-input rejection, frame-rate-independent timing, drift-free canonical poses and placement-coordinate refresh. The scene test verifies the new saved RotateLeft/RotateRight button bindings. See `Docs/AI/PuzzleCameraMigration.md` for migration details and the distinction from physical keyboard testing.

## Final cleanup validation

`FinalRegressionTests.cs` adds one case per stage. Each enters via the real stage buttons, rejects an out-of-range and wrong-direction drag, places one piece, checks exactly one score/bonus, rejects duplicate completion, checks dependent surfaces, expires during a drag, rejects post-failure camera input, retries, completes the puzzle, checks post-success input/time, and follows NextStage (the final stage retains its existing same-stage reload behavior).

The runtime fields now have read-only properties. TestState seeds private state only in unit fixtures; actual full-flow tests still use countdowns, drag callbacks and scene buttons. Production has no test-state API.

For a production-assembly WebGL check:

1. Run `Tests/PrepareWebGLValidation.ps1`.
2. Run Unity with `-batchmode -nographics -projectPath <repository>/Temp/WebGLValidation -buildTarget WebGL -executeMethod ProjectValidationEditor.AuditAndBuildWebGL -logFile <repository>/Logs/final-webgl-build.log`.
3. Inspect `Logs/final-asset-audit.json`, `Logs/final-webgl-summary.txt` and `Temp/FinalWebGLBuild/index.html`.

The editor helper is only copied into the isolated project's Assets/Editor. It inspects all five scenes, all prefabs, dangling serialized references and persistent UnityEvents (including EventTrigger lists), then builds the original enabled scenes with the original player settings. It does not save the original scenes or publish anything.
