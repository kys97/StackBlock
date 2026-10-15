# StageData phase 1

Runtime configuration is read through `GameSession.CurrentStageData`, with `GameManager.CurrentStageData` retained as a compatibility forwarding property after the subsequent session extraction. GameSession lazily loads `Resources/StageData/<Stage>` and caches the asset until the selected stage changes. An absent asset or mismatched Stage raises an explicit configuration error. Keep each asset filename equal to the existing Stage enum name. Topic is descriptive stage metadata; topic/stage selection now belongs to GameSession. The remaining sections record the initial StageData-only phase; see GameSessionMigration.md for current ownership.

## Migrated values

All nine assets use **20 seconds**, **5 bonus seconds**, **50 points per piece**, **1 degree per frame** camera rotation and **90 screen pixels** snap distance. The 20-second value comes from the saved Puzzle scene, not the legacy Timer field's 10-second C# initializer.

| Assets | Topic | Camera position | Initial Euler rotation |
| --- | --- | --- | --- |
| Spring, Summer, Desert, Fall, Winter | Weather | (0.22, 1.5, -3) | (23, 0, 0) |
| Bigben, Egypt, OperaHouse, TowerBridge | Structure | (0.3, 2, -3) | (32, 0, 0) |

Each asset references its matching sprite under `Resources/UI/Weather` or `Resources/UI/Structure` and its matching `Resources/Ground/<Stage>.prefab`. Ground references are prefab assets; the existing GameManager creates runtime instances. No scene object or runtime state is stored in StageData.

## Existing components

- GameManager retains its responsibilities and existing serialized fields. It resolves StageData, uses its Ground prefab and exposes its preview Sprite through the existing CurrentStageImage property.
- StageLoad reads initial camera position/rotation.
- Timer reads the selected data once in Start for the limit and completion bonus; decrement, cap, failure and restart behavior are unchanged.
- CameraControl reads rotation speed. The original frame-based rotation and arrival algorithm remain unchanged. Speeds that do not reach its exact 90-degree target can expose the existing arrival limitation; this phase does not redesign camera movement.
- BlockUI reads snap distance and points per completed piece.
- PointMove, PuzzleReferenceImage, Surface, BlockObj and UI retain their existing connections and behavior.

## Inspector

Select an asset in `Assets/Resources/StageData` to edit effective settings. Check Stage/Topic identity, Preview Image and Ground Prefab. Initial Camera Euler Angles are degrees; rotation speed is degrees **per frame**, not per second. Snap distance is in screen pixels.

GameManager camera fields and block_distance and Puzzle/Canvas/Timer.timeLimitSeconds are retained with their serialized values for migration safety, but no longer override StageData. No scene or prefab rewiring is required. The existing Left/Right UnityEvent targets, A/D shared methods, preview anchors/aspect and Resources piece-array ordering are unchanged.

## Validation

Prepare the complete isolated project using `Tests/PrepareFullFlowValidation.ps1`. Run Unity PlayMode tests without `-nographics` to include real EventSystem raycasts. StageDataTests checks all nine assets, settings, prefab references and cached selection. StartupRegressionTests tests a transient 30.5-second/2.5-second-bonus data instance. FullFlowTests checks real scene camera/limit/Ground bindings, all nine puzzle completions, scores, countdown, preview, navigation, retry and timeout.

No GameSession/PuzzleController split, script removal or responsibility migration is included.

Verified on Unity 6000.5.8f1: `Logs/stagedata-results.xml` passed 20/20 full-suite checks. After adding the non-default piece/camera test and making the timer fixture independent of asset loading, `Logs/stagedata-overrides-results.xml` passed both targeted checks (2/2). Together, 21 distinct checks passed. Runtime compilation had no errors; existing obsolete/unused warnings remain, plus the deliberately retained legacy Timer.timeLimitSeconds field is now unused.

Files added: Assets/Scripts/StageData.cs and its meta, Assets/Resources/StageData folder/meta and nine asset/meta pairs, Tests/StageDataTests.cs, this document. Runtime files changed: GameManager.cs, StageLoad.cs, Timer.cs, CameraControl.cs and BlockUI.cs. Supporting files updated: Tests/FullFlowTests.cs, Tests/StartupRegressionTests.cs, Tests/PrepareFullFlowValidation.ps1, Tests/PrepareStartupValidation.ps1, Tests/README.md and Docs/AI/UnityProjectContext.md. The StageData-only work did not edit scenes, existing prefabs or existing script metas. The subsequently requested Puzzle lighting adjustment is documented separately in PuzzleLighting.md.
