# Preview world pose and placement anchors — 2026-09-25

## Confirmed causes

The preview was already an independent Scene Root object. Its copied renderer rotations did not follow the camera: baseline Play Mode measured **0 degrees** rotation drift. However, BlockUI.LateUpdate unconditionally projected the unchanged screen pointer through the moving camera, translating the preview by **0.706395 world units in Spring** and **0.388063 in Bigben** during a 90-degree turn. The fix removes that camera-dependent reprojection, not an imaginary camera parent.

The old rule compared mouse screen coordinates to the target Transform pivot projected on screen, within **90 pixels**. The old preview aligned its pivot/normal-based bottom correction with a collider hit, not the same target anchor. Perspective, height and horizontal pivot offsets could therefore separate the visible preview from the position tested for placement.

Measured examples (world coordinates, full values in Logs/anchor-baseline-Spring.json and Logs/anchor-baseline-Bigben.json):

| Piece | Pivot | Bounds center | Bounds size | Bottom-center anchor | Pivot/anchor screen gap |
|---|---|---|---|---|---|
| Bigben ClockTower | (0.16472, 0.07912, 0.21973) | (0.16472, 0.29337, 0.21973) | (0.21331, 0.42850, 0.21331) | (0.16472, 0.07912, 0.21973) | 0 px |
| Bigben ClockTowerClock | (0.16472, 0.50762, 0.21973) | (0.16472, 0.70979, 0.21973) | (0.21032, 0.40449, 0.21032) | (0.16472, 0.50754, 0.21973) | 0.04 px |
| Spring Tree00 | (-0.06428, -0.09990, 0.54158) | (-0.06428, 0.18716, 0.54158) | (0.72650, 0.57433, 0.72650) | (-0.06428, -0.10000, 0.54158) | 0.05 px |
| Spring Tree02 | (0.20926, -0.08980, 0.38104) | (0.19540, 0.07607, 0.38725) | (0.44824, 0.33895, 0.44824) | (0.19540, -0.09341, 0.38725) | 7.47 px |

The clock tower and several trees already had near-bottom pivots, so a universally bad pivot was **not** the cause. Tree02 has a measurable horizontal offset; all pieces also shared the camera/preview mismatch and pixel-based tolerance.

Real full-stage testing exposed a second projection issue: a ray through a hollow elevated Surface can hit the distant background slab, while an unrelated foreground Surface can intercept a small target. With collider-only projection, the unlocked ClockTowerClock missed its target XZ by 5.68867 world units; Desert/Deco by 2.84323; Fall/Roof by 2.10585. A lower-hit-only support plane resolved those but Egypt/Broken_small_pillar still missed by 0.29739 due to a nearer occluding Surface. These failing runs are preserved in Logs/anchor-results.xml and Logs/anchor-final-results.xml; they are not the final validation result.

## Final behavior

- The preview stays at Scene Root with a snapshot of the target's transform hierarchy. Empty copied parents preserve authored local rotation, scale and nonuniform parent shear; they are not parented to live camera or puzzle transforms. The 2026-09-28 camera-relative rotation update below supersedes the original world-rotation lock.
- BlockUI caches PuzzleCameraController once. It projects only a genuinely changed pointer position, except the initial drag projection. While IsRotating, pointer movement is consumed without moving the placement anchor. LateUpdate updates camera-relative orientation, advances the hint and checks input state, but does not reproject a stationary pointer. Rotation completion alone cannot trigger an anchor jump.
- PuzzlePlacementAnchor caches the combined world bounds of the piece's visible MeshRenderer/SkinnedMeshRenderer geometry. It transforms renderer local-bounds corners so inactive target objects do not rely on stale world bounds. Default anchor is `(center.x, min.y, center.z)`, stored in piece-local space.
- PuzzleDragRaycaster still requires a hit on the active Ground/Surface candidates to recognize the puzzle area. For placement, the camera ray is intersected with a horizontal world plane at the selected piece's target-anchor Y. This fixes both holes and foreground interception. No hit, a parallel/behind-camera plane, or a plane beyond the camera ray limit produces no preview. This is a drag-position projection, not a snap or correctness rule; the original target is never moved.
- PuzzleDragPreview translates by `hit.point - AnchorWorldPosition`. Its anchor, not its Transform pivot, lies exactly on the projected point. Source geometry, authored rotation, world scale and the existing MPB color hint are retained.
- GameManager.CanPlacePiece takes a **world-space Vector3 preview anchor**. It retains play/pause, prior completion, active prerequisite Surface and camera direction checks; only the distance check becomes XZ anchor-to-anchor distance. Y is intentionally ignored. Both the hint and TryCompletePiece use that same query and the same current preview anchor. Cached Block.ScreenPosition remains an anchor projection for compatibility/diagnostics; no pixel distance participates in placement.
- UI return, ray miss, invalid drop, valid drop, Pause, expiry and scene exit retain their preview/hint cleanup. A UI-only release cannot place a piece because there is no visible world preview.

## Inspector and serialization

All nine StageData assets have `Snap Distance World = 0.2` Unity world units. Change that value to tune a stage. The old serialized `snapDistancePixels: 90` is retained hidden for asset preservation, but its public accessor and runtime use are removed. Pixels are not converted or reinterpreted as world units.

Default pieces require no manual wiring: GameManager adds PuzzlePlacementAnchor at runtime if the prefab has none. For an exceptional piece:

1. Open its `Assets/Resources/Puzzle/<Stage>/<Piece>.prefab`.
2. Add **PuzzlePlacementAnchor** to its `object` GameObject.
3. Create a child marker (e.g. `PlacementAnchor`) at the desired contact point; enable **Use Custom Placement Anchor** and assign that marker. The marker must be the same object or a descendant of `object`. An unassigned/outside marker falls back to automatic bounds.
4. Leave **Snap Distance Override = -1** for StageData, or set a nonnegative world distance for this piece. Zero means exact XZ coincidence.

GameManager's dependent-surface initialization and BlockObj's activation now skip child transforms that are not registered puzzle keys. Existing named dependency markers continue to work; a custom anchor marker cannot become a bogus dependency or throw a KeyNotFoundException. No prefab hierarchy or existing Inspector reference was rewritten.

## Files

New runtime: PuzzlePlacementAnchor.cs and .meta.

Modified runtime: BlockUI.cs, GameManager.cs, PuzzleDragPreview.cs, PuzzleDragRaycaster.cs, StageData.cs, Surface.cs, BlockObj.cs. PuzzleCameraController's behavior is reused without changes in this task.

Data: nine StageData assets, one new world-distance field each. Existing GUIDs, settings and asset references are retained.

Tests: PlacementAnchorTests.cs plus existing drag/hint/flow/pause/StageData tests migrated from pixel coordinates to anchor coordinates. The camera regression formerly expecting stationary-pointer movement now verifies the newly requested fixed pose. No weakening of remaining score, timer, success or cleanup assertions.

Material assets, production shaders, Lighting, FBX and scenes are not edited.

## Camera-relative preview rotation — 2026-09-28

The user replaced the fixed-world-rotation requirement with a fixed camera-relative orientation. Previously, the copied world rotation stayed unchanged while PuzzleCameraController.Update orbited the camera, so another face of the held piece became visible.

PuzzleDragPreview.UpdateCameraRelativeRotation captures `Inverse(cameraRotation) * root.rotation` exactly once at first display. Every subsequent BlockUI.LateUpdate applies `cameraRotation * cameraRelativeRotation`, including intermediate IsRotating frames, a stationary pointer and hidden UI-list visits. PuzzleCameraController updates the actual camera in Update, so no LateUpdate ordering override or camera-speed change is needed. Pointer-event updates use the same method for immediate visual consistency.

Only the temporary root rotates. Its position is rebuilt from the last ray-projected anchor minus the rotated anchor offset, without incremental accumulation. Copied child local transforms and scale, the source piece, placement conditions and Drop remain unchanged. Hide/Show retains the baseline; cancellation/disposal ends the session. The valid-placement MPB hint is evaluated after anchor compensation. Successful drops still enable the original target object and never copy the preview rotation.

Runtime changes: BlockUI.cs and PuzzleDragPreview.cs. Regression coverage adds PreviewCameraRotationTests.cs and updates former world-pose assertions to camera-relative orientation plus fixed-anchor assertions. Historical validation below describes the previous requirement, not the new behavior.

### Camera-relative validation

- Unity 6000.5.8f1, actual Play Mode in the isolated `Builds/CameraRelativeValidation` copy. The former Temp validation directory disappeared after the baseline run; the final copy lives outside Unity's temporary directory.
- Before the fix, all four Spring/Bigben × speed 1/10 cases failed with approximately 90 degrees of camera-relative drift after the first turn (`Logs/camera-relative-baseline-results.xml`).
- With the fix, the four rotation cases passed: **3,814 moving frames, maximum measured camera-relative angle error 0 degrees**, maximum anchor error **4.214685e-8 world units**. Checks run in a test probe after BlockUI.LateUpdate and cover A/D, Left/Right arrows, button callbacks, intermediate animation, repeated turns/full orbits, stationary pointer, UI return/re-entry, source/child transforms and Pause cancellation.
- `Logs/camera-relative-visual-{Spring,Bigben}-{before,during,after}.png`: all six captures visually inspected. The board reveals different sides while the held tree/tower retains its camera-relative orientation. Screen position/depth still change with the existing fixed world anchor, as requested.
- `Logs/camera-relative-asset-changes.json`: hashes of existing Assets/ProjectSettings before/after identify only BlockUI.cs and PuzzleDragPreview.cs as changed. Source materials, shaders, FBX, lighting, scenes, prefabs, StageData and camera controller are unchanged.
- Initial validation-copy setup mistakenly included an Editor-only material utility in the test assembly; that copy-only setup was corrected. The first post-fix targeted run was 4 passed/1 failed because the new reward test had disabled Timer and therefore unsubscribed its completion event. The test now re-enables Timer before Drop; production timer/reward code was not changed. The original failure logs are retained.
- Final related run: **36/36 passed, 0 failed, 0 skipped**, 304.74 seconds (`Logs/camera-relative-regression-results.xml`). Together with the four passing rotation cases, **40 distinct tests pass**. The corrected rotated-preview Drop test passes, preserving the actual target transform, MPB cleanup, score and time bonus. Regression also covers all **83 real pieces in nine stages**, world-distance and prerequisite checks, UI transitions, wrong drops, Pause/expiry/scene-exit cleanup, real sizes, multiple renderers/material slots and nested nonuniform transforms. Unity compilation completed without C# errors; no new runtime exceptions were reported. Existing obsolete test API and legacy folder-meta warnings remain.
- Test source changes: PreviewCameraRotationTests.cs (new), PlacementAnchorTests.cs, DragPreviewTests.cs and PrepareFullFlowValidation.ps1. Documentation: this report and UnityProjectContext.md. Manual physical-input testing and a WebGL player build are not part of this validation.

## Image suffix direction and board bounds — 2026-10-14

The F/R/B/L suffix was already parsed by GameManager.GetSpriteDirection and stored in Block.Direction, but only placement eligibility used it. The preview previously copied the target's world pose and captured whichever camera side was active on first display. BlockUI now passes the canonical image-view rotation from PuzzleCameraController's existing directionRotations array to the preview constructor. `Inverse(imageViewRotation) * initialRootRotation` is captured once; each frame still applies `currentCameraRotation * cameraRelativeRotation`. At the initial F camera the visible-copy yaw offsets are F=0, R=+90, B=180 and L=-90 degrees. Starting a drag from any camera side yields the same image-view orientation. The source object's authored local rotation and all child transforms remain intact; only the temporary root rotates about its retained anchor. At the valid placement direction the preview and real target orientations coincide.

The Egypt reproduction was confirmed in Play Mode: board world-bounds extents about .84, background extents about 21.21; a pointer at 55% screen width/80% height displayed Big_pyramid at (.53, .08, 1.55), outside the board. All four camera sides reproduced the unbounded projection, for both pyramids. Raw evidence is in Logs/egypt-drag-probe-results.xml and Logs/egypt-drag-probe.log; the temporary probe was removed from the validation project afterwards.

BlockUI caches the Ground root MeshFilter once. PuzzleDragRaycaster snapshots that mesh's local bounds and inverse world matrix once per drag. After calculating the existing target-anchor-height plane intersection, it checks XZ containment against those oriented bounds, using the board center height only for the containment test. This avoids including the huge Background child or enlarging the allowed rectangle into a world AABB when the board rotates. Elevated anchors retain their actual Y; they are not clamped to the ground. Off-board projection returns false, using the existing UI-image fallback, hiding/resetting the preview and preventing off-board Drop. The check is the mesh's outer bounds, not its exact silhouette or holes. Background ray hits inside that bounded footprint remain available for the existing hollow/occluded-piece projection behavior.

Visual captures exposed a second Egypt defect that numerical placement tests alone had missed: the Ground root was at local Y=0, while the identical mesh in Complete/Egypt.prefab is authored at Y=-0.42. A vertical collider ray at the pyramid anchor hit Ground at world Y=0.50, above the target base Y=0.08 and even above the big pyramid's top. Both preview and actual target were occluded; hiding Ground in an isolated diagnostic confirmed this. Ground/Egypt.prefab now uses the complete model's Y=-0.42, so that surface is Y=0.08. The actual piece and surface transforms are unchanged. Captures before correction are Logs/direction-visual-camera.png, direction-visual-actual-object.png and direction-visual-no-ground.png; the refreshed direction-preview-*.png files show the corrected board with visible previews. This is a Ground positioning repair, not a shader/depth-test workaround.

Runtime changes: BlockUI.cs, PuzzleDragPreview.cs, PuzzleDragRaycaster.cs, PuzzleCameraController.cs (read-only pose accessor). One prefab field changed: Ground/Egypt.prefab root localPosition.y, 0 to -0.42. No GameManager placement/reward logic, scene, material, shader, FBX, Lighting, StageData or Inspector wiring changes. Unknown image suffixes keep the old neutral-preview fallback rather than indexing the camera's direction array with -1.

Targeted Play Mode validation: Logs/direction-bounds-targeted-results.xml, **3/3 passed**, 22.51 seconds. Tests observe actual camera navigation poses as an independent reference for all four image suffixes at all four starting camera sides, exercise both Egypt pyramids off-board/return/on-board/exit at all four sides, and reject a point inside a rotated board's world AABB but outside its oriented bounds while allowing elevated anchors.

Validation follow-ups:

- The broad run initially passed 41/43 tests (Logs/direction-bounds-regression-results.xml). The two failures used unbounded screen coordinates that now legitimately left the board. PlacementAnchorTests now resumes projection at explicit in-board world points after each camera turn, and DragPreviewTests compares perspective at two in-board depths. Assertions for orientation, anchor movement, size, depth and unchanged source materials remain. Both affected cases plus the Bigben variant passed on rerun (Logs/direction-bounds-followup-results.xml, 4/4 including the isolated visual diagnostic).
- The broad run exercised all **83 real pieces in nine stages**, including hint, Drop, prerequisites, snap distance, rewards and completion. Spring/Bigben rotation at speed 1/10 sampled **7,175 moving frames**, maximum camera-relative angle error 0 degrees, maximum anchor error 4.214685e-8 world units. UI re-entry, repeated turns, Pause, expiry and disposal tests passed.
- After the Ground correction, Logs/direction-bounds-final-results.xml passed **4/4**: direction views, both pyramids on/off board at all camera sides, oriented board bounds, and a new comparison against the complete model plus actual collider height at both pyramid bases. F/R/B/L captures were generated in Play Mode. Shader/material/lighting assets are unchanged, including the user's existing bright color settings.
- Logs/direction-bounds-egypt-results.xml passed **2/2** after the Ground correction: all nine Egypt pieces through hint/Drop/completion and the Egypt UI-transition/reward regression. Across the related runs, **44 distinct non-diagnostic tests pass on their latest run, none remain failing** (Logs/direction-bounds-validation-summary.json). All four final direction captures were visually inspected. Existing Assets and ProjectSettings hashes changed only for the four named scripts and Egypt Ground prefab; all validation-copy source assets match the workspace (Logs/direction-bounds-asset-changes.json and direction-bounds-copy-differences.json).
- The first attempt to launch the follow-up Editor inside the sandbox stalled at licensing initialization; only that owned test process was stopped and the same validation was launched with the approved Unity runner outside the sandbox. Temporary visual probes are retained under ignored Logs for evidence and removed from the validation project's Assets/Tests. The original open editor was not controlled.
- Unity compiled the runtime and test scripts with no C# errors. Existing obsolete test API and legacy folder-meta warnings remain. Physical mouse operation and a WebGL player build were not run.

## Historical validation before image-direction/bounds update

Unity **6000.5.8f1**, actual Play Mode in the isolated `Temp/MaterialValidation` project. The user's open original editor was not driven or stopped.

- **Logs/anchor-regression-results.xml: 100/100 passed, failed 0, skipped 0, 586.63 seconds.** Includes final anchor/camera/override/projection tests, the preceding MPB hint/preview tests, nine-stage completion/retry regression, custom StageData settings, score/time bonus, Pause/expiry/navigation, startup/Surface lifecycle, StageData references and camera inputs/poses.
- **All 83 pieces in all nine stages** were dragged through the real BlockUI handlers at their projected bottom-center anchor, checked for the MPB hint, dropped and completed. Spring 11, Summer 8, Desert 10, Fall 9, Winter 8, Bigben 9, Egypt 9, OperaHouse 8, TowerBridge 11. Every piece also checked inside/outside world snap distance and Y-independent distance. Maximum measured target/preview XZ error: **0.000106931 world units**.
- `Logs/anchor-<Stage>.json` records each piece's pivot, world bounds, target anchor, preview anchor, old pivot screen point, new anchor screen point, XZ error and shared CanPlace result. The values are diagnostic evidence only; no model-name exceptions exist in runtime code.
- Camera tests cover A/D and Left/Right buttons, a pointer moving during rotation, unchanged pointer events after rotation, idle frames after rotation, and a fresh movement restarting projection. Preview parent, world position, anchor and renderer rotations are asserted. Source object transforms remain untouched.
- `Logs/anchor-visual-Spring-before.png`, `...Spring-after.png`, `...Bigben-before.png`, `...Bigben-after.png` were visually inspected. Tree and clock-tower previews remain in the same place in the scene while the camera reveals another side.
- `Logs/anchor-regression.log` has no C# compiler errors, shader errors or runtime exceptions. Existing obsolete test-API and legacy folder-meta warnings are not introduced by this fix.
- `Logs/anchor-validation-safety.json` verifies the tested runtime code matches the workspace. Against `Logs/anchor-baseline.json`, 16 existing Assets/ProjectSettings files changed: seven scripts and nine StageData assets. The new anchor script/meta are additional. **No Material, Shader, FBX, Lighting, Scene or Prefab asset changed during this task.**

Physical mouse operation and a WebGL player build were not run for this change. Input tests use the actual pointer handlers plus the existing keyboard input seam and UI button callbacks in Unity Play Mode. Earlier failing experiments remain in their original result files; the final 100-test run uses the completed implementation.
