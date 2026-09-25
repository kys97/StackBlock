# Unity project context — final cleanup, 2026-09-09

Project: StackBlock. Unity 6000.5.8f1, Built-in Render Pipeline, legacy Input Manager, uGUI. Runtime scripts use the default Assembly-CSharp assembly. Target: WebGL. Build scenes remain Main, Topic, Puzzle, Stage; normal navigation is Main → Topic → Stage → Puzzle. TestScene_YS is an old diagnostic scene outside Build Settings.

## Actual owners

There are 14 runtime scripts. PuzzleController, PuzzleUIController, PuzzlePieceUI, PuzzlePieceView, StageSelectionController, SceneNavigator and SceneTransitionView have NOT been introduced. Do not assume these types exist or remove their current owners.

- GameSession: persistent Topic/Stage selection, one cached StageData loader, selection-change event.
- StageData: nine existing stage configurations and asset references.
- GameManager: existing puzzle construction/dictionary, countdown, elapsed time, score, completion/failure and scene references. Retained because those responsibilities were not migrated. Initial serialized Topic/Stage are bootstrap seeds, not live duplicate state.
- PuzzleTimer: plain C# time model; no scene/UI dependency.
- Timer: active scene component that advances PuzzleTimer during play, observes piece completion, displays Slider and invokes failure handling/panel. Its old MonoScript GUID remains valid. It is not a second timer implementation.
- PuzzleCameraController: A/LeftArrow/Left and D/RightArrow/Right routing; one 90-degree orbit at a time; GameManager.CameraRotationSpeed (integer levels 1–10, default 3); canonical poses and authoritative direction. No Q/E and no GameManager direction copy.
- StageLoad: stage buttons, cloud animation, scene bindings/initialization, event-driven success display. Still the actual owner of these tasks.
- UI: existing UnityEvent navigation methods; method names preserved.
- PuzzleReferenceImage: selection-change-driven preview, bottom-left anchor and preserved aspect.
- BlockUI: drag image and validity feedback; requests GameManager.TryCompletePiece instead of writing score/count.
- BlockObj / Surface: dependent-surface activation and placement position visibility.
- ButtonUI / Sound: existing hover and audio controls.

## Configuration and behavior contracts

All nine StageData assets: 20-second limit, 5-second observed-completion bonus, 50 points/piece, 90-pixel snap range. Camera speed now belongs only to GameManager (levels 1–10, default 3); the former StageData speed field was removed. Weather camera (0.22,1.5,-3), Euler (23,0,0); Structure (0.3,2,-3), Euler (32,0,0). Resources array ordering and prefab child-name dependencies are unchanged. Ground and preview are asset references, never runtime scene objects.

GameManager's counters, score and play flags are private serialized fields with read-only properties. Old serialized counter names migrate using FormerlySerializedAs. TryCompletePiece checks play state, direction, active prerequisite surface, distance and prior completion before granting a reward. Success and failure reject further drag/rotation requests. Final score still deducts integer elapsed time. Timer keeps the earlier once-per-observed-count-change bonus rule.

Puzzle lighting is unchanged in this cleanup: Directional Light intensity 1.1, shadow strength 0.65, Gradient ambient lighting. Materials, pipeline, scenes, StageData and ProjectSettings are preserved. Only two inert OnClick entries were removed from ClickImage/StageButton prefabs; their real runtime listeners and other serialized data are preserved. Clouds retain their raycast settings and move between ±2600 and ±480 anchored positions.

## Validation

Tests live outside Assets. PrepareFullFlowValidation.ps1 copies the real project and installs test-only assembly definitions in Temp/FullFlowValidation. PrepareWebGLValidation.ps1 creates a separate production-assembly copy and installs the scene/prefab audit/build entry point only there. Neither saves original scenes nor changes original Build Settings.

See FinalCleanup.md for exact results: first suite 57 passed; final runtime suite 56 passed plus one Fall countdown timeout; unchanged-timeout follow-up 5 passed. Final asset audit: 5 scenes, 105 prefabs, 48 events, no dangling references/events, one pre-existing TestScene_YS missing script. Actual WebGL build succeeded with zero BuildReport errors/warnings. Earlier migration reports describe historical stages and may name APIs which final cleanup removed. Known historical automation issue: some prior scene countdown waits exceeded their 15-second wall-clock timeout; never claim a rerun erased that evidence.

## Deliberately retained limitations

GameManager is still persistent and has substantial puzzle responsibilities. The mutable Dictionary structure and Resources array pairing remain legacy contracts. Score UI has digit sprites only; negative-score presentation is not redesigned. Last stage's NextStage button retains existing reload-the-same-stage behavior. Physical browser keyboard focus, audio autoplay, hosting compression headers and visual layout require target-browser checks after a successful WebGL build.



## Pause and camera settings update — 2026-09-25

GameManager owns IsPaused and PauseGame/ResumeGame/TogglePause plus the camera speed setting. PuzzlePauseUI owns Esc/button routing and PausePanel visibility. BlockUI cancels in-flight drag on PauseChanged; Timer defers ticking and bonus processing while paused. Navigation restores timeScale before scene loads; scene cleanup/loading also restores it. The latest user-authored materials and lighting were preserved. Camera speed is now a 1–10 level converted internally by PuzzleCameraController as 90 degrees/second times the level. PausePanel/BackButton reuses UI.ToStage; GameManager.EndPuzzle restores time, clears puzzle runtime data and releases scene references before returning to Stage. See PuzzlePauseAndCameraSettings.md for current validation and Inspector setup.
