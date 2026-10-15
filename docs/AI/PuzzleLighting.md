# Puzzle lighting adjustment (phase 1)

## Evidence and scope

Unity 6000.5.8f1, Built-in Render Pipeline, saved Puzzle scene. Directional Light already existed with intensity 0.7, warm RGB (1, 0.95686275, 0.8392157), rotation (50, -30, 0), soft shadows and shadow strength 1. Ambient Source was Skybox with intensity 1, no assigned LightingDataAsset, and realtime GI disabled. Main Camera uses Skybox clear flags, HDR, perspective FOV 30; no brightness/exposure controller exists in the runtime scripts.

Rendering completed Spring and Bigben models in an isolated PlayMode copy showed very dark unlit faces, roofs and ground sides. Raising direct intensity alone brightened the lit faces but left the opposite faces difficult to read. A small Skybox intensity increase did not adequately solve this. Gradient ambient lighting improved roof and wall detail while preserving face-to-face contrast. This supports insufficient direct/fill illumination as the practical cause; it does not claim to prove an engine GI defect.

## Saved changes

| Inspector setting | Before | After |
| --- | --- | --- |
| Puzzle/Directional Light, Intensity | 0.7 | 1.05 |
| Puzzle/Directional Light, Shadows Strength | 1 | 0.8 |
| Lighting/Environment, Environment Lighting Source | Skybox | Gradient |
| Ambient Sky RGB | (0.212, 0.227, 0.259) | (0.36, 0.386, 0.44) |
| Ambient Equator RGB | (0.114, 0.125, 0.133) | (0.228, 0.25, 0.266) |
| Ambient Ground RGB | (0.047, 0.043, 0.035) | (0.141, 0.129, 0.105) |

Before values for the three colors were serialized but not the active lighting source under Skybox mode. Ambient intensity remains serialized as 1; under Gradient, adjust the three colors rather than the Skybox intensity multiplier. Skybox material/reflections, light color/direction, soft-shadow type/bias, camera settings, pipeline, materials and emission are unchanged. No bake or extra Light GameObject is required.

## Inspector workflow

Exit Play Mode and open the saved Puzzle scene. Select Directional Light to adjust Intensity or Shadows Strength. Open Window > Rendering > Lighting > Environment and adjust Sky, Equator and Ground colors under Environment Lighting (Source: Gradient). Equator is the primary control for side-face fill. Save the scene after editing. If Puzzle was already open with unsaved edits, preserve those before reloading the saved scene.

StageData settings remain in Assets/Resources/StageData; lighting is deliberately stored in the Puzzle scene, outside the requested StageData fields. See StageDataMigration.md for all nine assets and consumer connections.

## Validation and captures

Tests/PuzzleLightingCaptureTests.cs extends the existing FullFlowTests fixture. SavedPuzzleLightingRenders loads real navigation and puzzle assets, then displays all pieces in the isolated test scene for Spring, Bigben and Winter and captures the initial and rotated camera views. These diagnostic arrangements do not modify production gameplay or assets. The PNG captures are Camera.Render output at 960x540 and exclude screen-overlay UI; they are not physical-display screenshots.

CapturePuzzleLighting is an explicit, optional comparison diagnostic, selected with `-testFilter CapturePuzzleLighting`. It renders original, direct-light-only and ambient-assisted variants without saving those runtime changes. Normal full validation excludes these experimental comparison cases.

Captures: Logs/PuzzleLighting/*-before.png and *-after.png. The saved pre-edit scene at Logs/PuzzleLighting/Puzzle-before.unity allows an exact diff: only six lighting fields changed. The complete test copy uses Tests/PrepareFullFlowValidation.ps1; production assembly definitions are unchanged.

This addition changes Assets/Scenes/Puzzle.unity, Tests/FullFlowTests.cs (partial fixture declaration only), Tests/PrepareFullFlowValidation.ps1 and supporting documentation; it adds Tests/PuzzleLightingCaptureTests.cs and this document. StageData and runtime scripts from the preceding phase remain intact. No GameSession/PuzzleController split is included.

Final Unity 6000.5.8f1 result: Logs/phase1-lighting-results.xml, Passed, 24 passed / 0 failed / 2 explicit diagnostic cases skipped (26 discovered). The two optional comparison cases had passed separately in Logs/lighting-ambient-results.xml before saving the selected settings. The final run includes all nine stage completions, StageData settings/cache/overrides, countdown, score, timeout, retry, preview, real Stage button raycasts and the three saved-lighting capture cases. No C# compilation errors were found. Existing deprecated API and intentionally preserved unused-field warnings remain. Saved Spring, Bigben and Winter renders at both orientations were visually inspected; shapes and shadow contrast remain visible, including the white snow surfaces.
