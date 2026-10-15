# Unity project context — final cleanup, 2026-09-09

Project: StackBlock. Unity 6000.5.8f1, Built-in Render Pipeline, legacy Input Manager, uGUI. Runtime scripts use the default Assembly-CSharp assembly. Target: WebGL. Build scenes remain Main, Topic, Puzzle, Stage; normal navigation is Main → Topic → Stage → Puzzle. TestScene_YS is an old diagnostic scene outside Build Settings.

## Actual owners

PuzzleController, PuzzleUIController, PuzzlePieceUI, PuzzlePieceView, StageSelectionController, SceneNavigator and SceneTransitionView have NOT been introduced. Do not assume these types exist or remove their current owners.

- GameSession: persistent Topic/Stage selection, one cached StageData loader, selection-change event.
- StageData: nine existing stage configurations and asset references.
- GameManager: existing puzzle construction/dictionary, countdown, elapsed time, score, completion/failure and scene references. Retained because those responsibilities were not migrated. Initial serialized Topic/Stage are bootstrap seeds, not live duplicate state.
- PuzzleTimer: plain C# time model; no scene/UI dependency.
- Timer: active scene component that advances PuzzleTimer during play, observes piece completion, displays Slider and invokes failure handling/panel. Its old MonoScript GUID remains valid. It is not a second timer implementation.
- PuzzleCameraController: A/LeftArrow/Left and D/RightArrow/Right routing; one 90-degree orbit at a time; saved GameSettings.CameraRotationSpeedLevel (integer levels 1–10, default 3), read through the compatibility GameManager.CameraRotationSpeed property; canonical poses and authoritative direction. No Q/E and no GameManager direction copy.
- StageLoad: stage buttons, cloud animation, scene bindings/initialization, event-driven success display. Still the actual owner of these tasks.
- UI: existing UnityEvent navigation methods; method names preserved.
- PuzzleReferenceImage: selection-change-driven preview, bottom-left anchor and preserved aspect.
- BlockUI: UI drag image in the Scroll View; independent world-scale PuzzleDragPreview outside it, projected through PuzzleDragRaycaster; validity feedback and existing GameManager.TryCompletePiece request. No writes to score/count or the source piece transform. See PuzzleDragPreview.md for transient geometry/material ownership and validation.
- BlockObj / Surface: dependent-surface activation and placement position visibility.
- ButtonUI / Sound: existing hover and audio controls.

## Configuration and behavior contracts

All nine StageData assets: 20-second limit, 5-second observed-completion bonus, 50 points/piece, 0.2 world-unit XZ anchor snap range. The former 90-pixel value is retained only as hidden legacy serialized data. Camera speed belongs to PlayerPrefs via GameSettings (levels 1–10, default 3); the former StageData and GameManager Inspector speed fields were removed. Weather camera (0.22,1.5,-3), Euler (23,0,0); Structure (0.3,2,-3), Euler (32,0,0). Resources array ordering and named piece dependencies are unchanged; unregistered child transforms such as custom anchor markers are ignored. Ground and preview are asset references, never runtime scene objects.

GameManager's counters, score and play flags are private serialized fields with read-only properties. Old serialized counter names migrate using FormerlySerializedAs. CanPlacePiece is the read-only query shared by the preview hint and TryCompletePiece: it takes the preview's world anchor and checks play state, direction, active prerequisite surface, XZ anchor distance and prior completion before a drop can grant a reward. Success and failure reject further drag/rotation requests. Final score still deducts integer elapsed time. Timer keeps the earlier once-per-observed-count-change bonus rule.

Puzzle lighting is unchanged in this cleanup: Directional Light intensity 1.1, shadow strength 0.65, Gradient ambient lighting. Materials, pipeline, scenes, StageData and ProjectSettings are preserved. Only two inert OnClick entries were removed from ClickImage/StageButton prefabs; their real runtime listeners and other serialized data are preserved. Clouds retain their raycast settings and move between ±2600 and ±480 anchored positions.

## Validation

Tests live outside Assets. PrepareFullFlowValidation.ps1 copies the real project and installs test-only assembly definitions in Temp/FullFlowValidation. PrepareWebGLValidation.ps1 creates a separate production-assembly copy and installs the scene/prefab audit/build entry point only there. Neither saves original scenes nor changes original Build Settings.

See FinalCleanup.md for exact results: first suite 57 passed; final runtime suite 56 passed plus one Fall countdown timeout; unchanged-timeout follow-up 5 passed. Final asset audit: 5 scenes, 105 prefabs, 48 events, no dangling references/events, one pre-existing TestScene_YS missing script. Actual WebGL build succeeded with zero BuildReport errors/warnings. Earlier migration reports describe historical stages and may name APIs which final cleanup removed. Known historical automation issue: some prior scene countdown waits exceeded their 15-second wall-clock timeout; never claim a rerun erased that evidence.

## Deliberately retained limitations

GameManager is still persistent and has substantial puzzle responsibilities. The mutable Dictionary structure and Resources array pairing remain legacy contracts. Score UI has digit sprites only; negative-score presentation is not redesigned. Last stage's NextStage button retains existing reload-the-same-stage behavior. Physical browser keyboard focus, audio autoplay, hosting compression headers and visual layout require target-browser checks after a successful WebGL build.



## Pause and camera settings update — 2026-09-25

GameManager owns IsPaused and PauseGame/ResumeGame/TogglePause plus the camera speed setting. PuzzlePauseUI owns Esc/button routing and PausePanel visibility. BlockUI cancels in-flight drag on PauseChanged; Timer defers ticking and bonus processing while paused. Navigation restores timeScale before scene loads; scene cleanup/loading also restores it. The latest user-authored materials and lighting were preserved. Camera speed is now a 1–10 level converted internally by PuzzleCameraController as 90 degrees/second times the level. PausePanel/BackButton reuses UI.ToStage; GameManager.EndPuzzle restores time, clears puzzle runtime data and releases scene references before returning to Stage. See PuzzlePauseAndCameraSettings.md for current validation and Inspector setup.

## Preview placement hint — 2026-09-25

BlockUI uses GameManager.CanPlacePiece for the same validity decision as Drop. PuzzleDragPreview smoothly tints every temporary renderer/material slot via cached MaterialPropertyBlocks and clears overrides on invalid placement, hiding and disposal. The old UI alpha-blink coroutine is removed. ClickImage.prefab → BlockUI → Placement Hint exposes validPlacementColor (1, .85, .2, 1) and hintBlinkSpeed (4 radians/second, about 1.57 seconds/cycle). Source shader colors are read through MainColor metadata or _BaseColor/_Color after property/type checks; preview shader color and opacity remain independently owned. See PuzzlePlacementHint.md for validation.

## World pose and anchors — 2026-09-25

PuzzlePlacementAnchor owns automatic renderer bottom-center geometry and optional custom anchor/snap configuration on the piece's object. Preview child transforms/scale are captured independently. As of 2026-09-28, the preview captures its rotation relative to the puzzle camera once at first display and follows that camera rotation in BlockUI.LateUpdate, after the camera's Update. Only the disposable root rotates; its translation is compensated to keep the last projected placement anchor fixed. The former fixed-world-rotation requirement is superseded. Pointer movement during rotation is still consumed; a new movement after rotation is required to reproject. Ground/Surface ray hits gate the puzzle area; an anchor-height horizontal plane supplies the drag point consistently for tall/hollow/occluded geometry. Hint and Drop use the same world anchor through CanPlacePiece. See PuzzlePlacementAnchors.md for measured clock/tree evidence, Inspector instructions and validation.

## Image suffix orientation and bounded preview — 2026-10-14

GameManager already parses image suffixes as F=0, R=1, B=2, L=3 for placement eligibility. BlockUI now also uses that existing Direction to select PuzzleCameraController.GetDirectionRotation when creating PuzzleDragPreview. The preview stores its camera-relative orientation against that authored image view, rather than against whichever side the player happened to be viewing. Its camera-relative direction stays constant through camera animation and UI re-entry; at the correct placement view it aligns with the real object's untouched orientation. No duplicate filename parser or placement rule is introduced.

PuzzleDragRaycaster now receives the cached Ground root MeshFilter and limits anchor-plane projection to that mesh's oriented local bounds, ignoring height for the footprint check. Background child colliders can still support ray gating for hollow/elevated targets, but no longer allow projected anchors outside the board bounds. Outside the bounds the existing UI drag image is shown and Drop cannot place the piece. This is an outer-bounds check, not an exact mesh-silhouette/cutout test. Camera animation is unchanged. Egypt Ground also had a missing authored height offset: its local Y is corrected from 0 to -0.42 to match the identical ground mesh in Complete/Egypt.prefab. The pyramid's ground surface is now Y=0.08 instead of Y=0.50, matching the unchanged piece anchors and fixing geometry occlusion. Materials, FBX and lighting remain unchanged. See PuzzlePlacementAnchors.md for evidence and validation.

## Drag preview occlusion — 2026-10-15

BlockUI now gives the drag-owned PuzzlePreviewOcclusion query the existing Ground and other piece objects. It uses mesh-only collider copies and a camera-oriented BoxCastNonAlloc for the complete preview bounds, checking initial overlap separately. Only the temporary visual moves toward the camera along its anchor ray; PuzzleDragPreview.AnchorWorldPosition retains the original logical projection used by hint and Drop. VisualAnchorWorldPosition exposes the displayed location. Rotation and visibility are updated after the camera's Update, including stationary pointers and ongoing turns. If geometry leaves no safe depth before the near clip plane, the existing UI drag image remains visible instead. Ordinary perspective can enlarge the pulled-in preview. Proxy geometry is static during each drag and is disposed on cancellation/Drop/Pause/scene exit. No source material, prefab, scene, camera controller or placement-rule changes. See PuzzleDragPreview.md for the detailed behavior and validation.

Follow-up: leaving the piece-list viewport now shows a 3D preview immediately, even before a board hit. Outside the board, BlockUI uses a camera-facing plane at the target anchor's depth for presentation. The unchanged board raycaster is still required for hint/Drop, gated by hasPlacementHit; floating preview positions never authorize placement. A first list exit during camera rotation also creates the preview. Inside the list the UI image remains in use. This supersedes the earlier off-board UI fallback described above; near-camera occlusion and off-screen fallbacks remain. See PuzzleDragPreview.md for validation.

## Winter Surface visibility investigation — 2026-10-15

Confirmed rendering issue, investigated without changing source assets: Winter creates eight Surface objects with enabled renderers and valid Material #709 references (Standard shader, opaque black). Six are initially active; door and sheet are intentionally hidden by the existing prerequisite flow. No active surfaces are visible from the initial camera in the saved assets. Hiding Ground entirely or raising surfaces by 0.02 in an isolated runtime did not reveal them. Front-face collider probes also missed their visible-side samples, while allowing backfaces produced hits for the sampled meshes (the narrow fence was not covered by that grid).

The decisive visual comparison used a temporary, isolated black shader with only its Cull property changed: Cull Back reproduced the missing surfaces; Cull Off revealed their black shapes without moving them or changing Ground, lighting or camera. Captures are `Logs/PuzzleLighting/WinterSurface-baseline.png`, `WinterSurface-ground-hidden.png`, `WinterSurface-lift-002.png`, `WinterSurface-probe-cull-back.png` and `WinterSurface-probe-cull-off.png`. Diagnostic runs: `Logs/winter-surface-initial-results.xml` and `Logs/preview-occlusion-winter-diagnostic-results.xml`, one successful diagnostic each, no compile errors. These are diagnostic passes, not proof of a production fix. The cause is the Surface mesh's face orientation combined with backface culling. Earlier gameplay tests checked active/prerequisite state and placement, not actual visible Surface pixels.

No production correction was applied in response to the user's question about the cause. A future correction should address only the Surface geometry or use a Surface-specific two-sided material: Material #709 is also referenced by the actual door object and Complete/Winter, so changing that shared material globally has a wider effect. Diagnostic script/shader copies were removed from the validation Assets after use; evidence and the probe source are retained under ignored Logs.

## Winter Surface culling fix — 2026-10-15

Follow-up request implemented: all eight Winter Surface renderer slots now use dedicated materials with Cull Mode = Off. `Resources/Shaders/PuzzleSurface.shader` is an opaque, unlit placement-marker shader with an Inspector Cull Mode property. It retains depth testing/writing and the existing black marker color; it is not a general replacement for lit puzzle materials. `Resources/SurfaceMaterials/WinterSurface.mat` serves seven surfaces, while `WinterDoorSurface.mat` preserves the door surface's newly user-selected black source appearance separately. Both have `_Cull = 0`. Source materials, actual piece materials, FBX, Ground, lighting and placement rules are unchanged. The user's latest door/fence prefab edits, including door rotation and actual-object material selection, were preserved.

Only one Surface material reference changed in each of the eight prefabs relative to the task-start baseline. Reversing those references reproduces the backed-up prefab bytes exactly; an Assets/ProjectSettings baseline hash comparison found no other existing asset changes during this fix. Door and sheet still wait for their existing prerequisites; disabling culling does not bypass activation or occlusion by other geometry.

Graphics-enabled isolated Play Mode validation: `WinterSurfacesRenderBothSidesAndKeepProgression` passed (1/1, 11.84 seconds). It rendered all eight Surface meshes from both sides, verified material separation and initial prerequisite visibility, then placed all eight pieces through the real UI drag handlers and reached success. Fence is intrinsically narrow (17/18 visible pixels in the diagnostic views), and no thickness/geometry change was made. No C# or shader compilation errors were reported. Actual scene captures from all four camera directions are under `Logs/PuzzleLighting/WinterSurface-fixed-*.png`; initial and opposite views were visually inspected. Result: `Logs/preview-occlusion-winter-surface-results.xml`. No player build or physical-mouse test was performed for this change.
