# Actual scene validation — 2026-09-08

## Findings and fixes

- Puzzle/Canvas/Scroll View/Content referenced missing script GUID `5539dc8c8780e3a438dc009b934f8dad`. The original scene at HEAD also contains it, with no matching source/meta in available history. Removed the unresolved component entry; preserved both layout components. A baseline Play Mode scene test failed explicitly with `Missing script: Content`.
- Winter initialization failed in `GameManager.HideDependentSurfaces` because igloo's child marker `Door` did not match the resource key `door`. Corrected the marker.
- The next run exposed the same mismatch for snowmobile's `Sheet` marker and the `sheet` resource. Corrected that marker. Both errors interrupted StartPuzzle before countdown configuration; they explain the subsequent countdown null exception in those runs.

## Executed checks

Unity 6000.5.8f1, Play Mode, headless. `Tests/PrepareFullFlowValidation.ps1` copies actual Assets, Packages and ProjectSettings, preserving asset GUIDs, into a separate project. Tests load real scenes, invoke their button callbacks, dispatch drag events to real spawned BlockUI components, rotate using the real camera button, and assert visible-state flags and game state.

- Main → Topic → Stage → Puzzle navigation for both topics.
- All nine stages completed: Spring, Summer, Desert, Fall, Winter, Bigben, Egypt, OperaHouse, TowerBridge.
- Countdown completion, hidden Ready panel and expected spawned block count.
- Block placement, camera rotation completion, score/success panel flow.
- Spring completion → next stage Summer → completion → retry.
- Timer expiration → failure panel → retry → return to selection.
- Five focused regression tests for 3/2/1 presentation, cancellation, duplicate countdown calls, Surface initialization/teardown and replacement-block identity.

## Evidence and limits

- `Logs/fullflow-baseline.xml`: 0/2 before removing the missing Content component; teardown errors in this baseline were caused by the test fixture destroying the manager before unloading the scene and were corrected in the fixture.
- `Logs/fullflow-pass1.xml`: 8/9; Winter failed on Door.
- `Logs/fullflow-final.xml`: 13/14; Winter failed on Sheet.
- `Logs/winter-final.xml`: 1/1 after Sheet correction. The other 13 checks already passed and their relevant assets/code were unchanged by that correction.
- All 14 distinct checks passed across the final relevant runs. Logs/XML are local ignored artifacts. Test sources and preparation instructions are tracked under `Tests`.
- No claim of physical mouse hit testing, rendered visual QA, or a WebGL player build. The tests validate actual scene runtime flow through automated event dispatch.

## Stage back-button regression

The Stage back button already had a valid UI.GameStart callback to Topic. Its Canvas used fixed-pixel sizing, putting the button outside the smaller test Game view. After switching this scene's CanvasScaler to Scale With Screen Size, 1920×1080, Expand, EventSystem.RaycastAll reproduced a second failure: the top hit at the button center was cloud `Image (1)`, with no click handler. The initial raycast-disable workaround was reverted at the user's direction: all six images retain their original enabled raycast targets. Stage cloud roots now wait at ±2600 instead of ±1920, beyond the complete child-image extents. StageLoad uses anchored-position MoveTowards for closing to ±480 and opening to ±2600, avoiding world-space scaling and overshoot.

`StageBackReceivesPointerAndReturnsToTopic` now runs with rendering enabled, queries EventSystem raycasts at actual button centers, dispatches the pointer click to the first hit, asserts Stage → Topic, reopens Stage and clicks a stage button the same way, then verifies Puzzle countdown completion. Both Weather and Structure passed (`Logs/stageback-final.xml`, 2/2). Baseline logs are `stageback-rendered.xml` (no hit at the off-screen button) and `stageback-scaling.xml` (cloud intercepted hit). This validates Unity UI hit testing, though not physical mouse hardware input or visual appearance review.

Final position-based correction: the same two tests now additionally assert that every cloud Image retains raycastTarget=true and that every corner of each left/right image lies beyond the corresponding canvas edge. Both passed after the correction (`Logs/cloud-position-final.xml`, 2/2), including return/reselection and the modified opening transition through countdown completion.

## Puzzle reference image

Added GameManager.CurrentStageImage as a read-only query of existing topic/stage data. PuzzleReferenceImage observes selection changes and displays the corresponding existing Resources/UI sprite. The scene includes a bottom-left anchored Image with 24-unit margins, a 240×180 box, preserveAspect enabled and raycastTarget disabled. No progression, scoring or block-placement logic was modified for this feature.

The full render-enabled Unity Play Mode suite passed 17/17 (`Logs/reference-image-results.xml`). It now checks all nine stage images, bottom-left anchor geometry/margins, aspect/raycast settings, next-stage and retry image updates, and live selection changes without changing score, completion count or existing puzzle data. The existing gameplay and cloud/back-button regression tests passed in the same run.
