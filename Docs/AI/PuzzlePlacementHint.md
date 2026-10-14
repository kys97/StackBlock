# 3D Preview placement hint — 2026-09-25

후속 변경: 위치 판정은 이제 World Anchor의 XZ 거리로 통일되었다. 카메라 회전 중 Preview 고정과 변경 후 검증은 [PuzzlePlacementAnchors.md](PuzzlePlacementAnchors.md)를 참고한다. 아래 검증은 색상 Hint 도입 당시의 기록이다.

## Scope and Inspector

Modified runtime scripts: `BlockUI.cs`, `GameManager.cs`, `PuzzleDragPreview.cs`. `Resources/UI/ClickImage.prefab` stores two new BlockUI settings under **Placement Hint**:

- `validPlacementColor`: (1, 0.85, 0.2, 1), yellow. Alpha is ignored during tinting to preserve the original preview opacity.
- `hintBlinkSpeed`: 4 radians/second; one smooth original → highlight → original cycle takes about 1.57 seconds. Minimum Inspector value 0.1.

The prefab's existing MonoScript GUID and move_prefab reference are retained. No additional scene references or manual wiring are needed. No Material assets, production shaders, FBX or lighting settings are edited.

## Shared correctness decision

The project's actual placement owner remains GameManager; PuzzleController has not been introduced. The exact rejection guard formerly in TryCompletePiece now lives in the read-only CanPlacePiece(key, screenPosition). TryCompletePiece invokes it before the unchanged completion, score, bonus and success path. BlockUI invokes the same query for feedback: playing/not paused, existing and incomplete piece, active prerequisite Surface, current camera direction, StageData SnapDistancePixels.

No 3D distance rule, extra dependency rule or alternate placement check was added. The preview is still a visual copy projected with the existing PuzzleDragRaycaster. UI-list and ray-miss handling are unchanged. The pointer event updates validity immediately with zero elapsed animation time; LateUpdate repeats the check and advances with Time.deltaTime once per frame. Stationary pointer, direction changes and prerequisite changes therefore cannot leave a stale hint.

## Color and resource ownership

At preview creation, the source material is read using a Color property tagged MainColor if present, otherwise a supported Color-typed _BaseColor or _Color. HasProperty and shader metadata are checked. A material without a supported color retains the preview shader's default white base. The existing independent preview shader supports _Color; no shader replacement was added by this hint feature.

Every temporary renderer/material slot caches its base color, property ID and one MaterialPropertyBlock. MeshRenderer sources and baked SkinnedMeshRenderer sources both use this path. A cosine blend controls RGB only. SetPropertyBlock(block, slotIndex) never changes source assets, shared material properties, opacity, textures, emission or shaders. No Renderer.material access or per-frame material construction is used.

Leaving the valid range, returning to the UI, ray misses and Hide clear all per-slot property blocks and reset the phase. Drop, Pause, expiry and scene cleanup dispose the same preview; property blocks are cleared immediately before hiding and destruction. Actual placed objects receive no property blocks. A new drag starts with fresh phase and base colors.

The old BlockUI PulseDragImage coroutine and its duplicated validity condition had no other callers. They were removed; the UI image no longer changes alpha for placement hints.

## Validation

Tests are outside production Assets: `Tests/PlacementHintTests.cs` and three minimal shader fixtures in `Tests/Fixtures`. PrepareFullFlowValidation.ps1 installs these only in an isolated validation project. They cover actual Spring/Egypt placement, stationary-pointer animation, direction/prerequisite/snap checks, UI return, reentry, successful/failed release, sequential pieces and Pause/Resume. Synthetic geometry covers multiple MeshRenderers plus a SkinnedMeshRenderer, three material slots each, original/midpoint/peak/full-cycle colors, alpha preservation, hidden/zero-time behavior and unchanged source/preview material serialization. Shader fixtures cover _BaseColor, a custom MainColor property and no color property, in addition to Standard.

Unity 6000.5.8f1 Play Mode ran in `Temp/MaterialValidation`, leaving the user's open editor untouched:

- `Logs/placement-hint-results.xml`: 6/6 passed, no skipped/failed cases, 22.09 seconds. Two actual scene-flow cases (Spring/Egypt) and four multi-renderer shader/property cases.
- `Logs/placement-hint-regression-results.xml`: 30/30 passed, no skipped/failed cases, 401.07 seconds. Includes all nine stages' UI/world preview transitions, original geometry/material preservation, valid/invalid drops, camera direction, points, time bonus, success/failure, retry/next-stage and Pause/navigation regressions.
- Neither run's log contains C# compiler errors, shader errors or runtime exceptions. Existing deprecated test-API warnings and legacy folder-meta parser warnings remain outside this change.
- `Logs/placement-hint-base.png` and `Logs/placement-hint-highlight.png` were visually inspected: the Spring preview changes from its original red/brown to bright yellow across its visible shape.
- Against `Logs/placement-hint-baseline.json`, the only changed existing Assets/ProjectSettings files are the three runtime scripts and ClickImage.prefab listed above. All 149 existing Material/FBX/Scene/lighting files in the protected-file scan are unchanged; the production preview shader is unchanged. Runtime scripts and the prefab match the tested copy by hash.

These are actual Unity Play Mode tests using the existing pointer handlers and scene flows. Physical mouse operation and a WebGL player build were not run for this change. Test shader fixtures are outside production Assets and do not enter the game build.
