# PuzzleTimer extraction

## Ownership and configuration

PuzzleTimer is a plain C# time model, not an additional MonoBehaviour. It owns TimeLimit, RemainingTime, TimeBonus, IsRunning and IsExpired with private setters. Reset reads StageData.TimeLimitSeconds and TimeBonusSeconds (existing assets remain 20 seconds / 5 bonus seconds). Start, Stop, Tick and AddCompletionBonus work without a Slider, GameObject or GameManager. Tick receives Time.deltaTime from the existing scene adapter. Changed and Expired events carry display/expiration notifications.

Timer.cs retains its filename, class and MonoScript GUID `23bed681ada80c343839fa8a5c3f3a64`. Its old Slider-based arithmetic has been replaced completely. It is now a small display and temporary GameManager compatibility adapter: cache its Slider once, subscribe to time/placement events, gate ticking by existing gameplay flags, forward delta time once and display the model. No second timer component runs in parallel. It reads no GameManager.Instance and performs no per-frame object lookup or completion-count polling.

The adapter still checks cached GameManager status/start/success each frame because those public flags have not yet been extracted to PuzzleController. GameManager.playing_time, final-score subtraction, puzzle dictionary, loading, placement rules and other managers remain unchanged.

## Inspector migration

- Puzzle/EventSystem StageLoad now has Puzzle Timer UI referencing the existing Timer component on Canvas/Timer (component fileID 1927446307).
- StageLoad calls Timer.Initialize(GameManager, selectedStageData) immediately after StartPuzzle. This avoids relative Start ordering and seeds the completion-count baseline only after puzzle reset.
- Canvas/Timer retains its Slider, original Timer GUID and existing failure panel reference. The serialized `fail_pan` reference maps via FormerlySerializedAs to private `failurePanel` (Inspector label Failure Panel). The public getter is FailurePanel.
- The inactive legacy `timeLimitSeconds` field and its saved 20-second scene entry were removed. The 20-second settings remain in all StageData assets; no duplicate time settings are exposed by Timer or PuzzleTimer.
- Existing UI.timer Slider links, buttons, scene object IDs and preview/camera references are retained. No manual rewiring is required.

## Runtime flows

Countdown completes → existing GameManager.start becomes true → Timer checks Puzzle status, start and !success → PuzzleTimer.Start / Tick(Time.deltaTime). Before start, outside Puzzle, after failure or after success, the model is stopped. Disabling the adapter stops it and removes subscriptions; re-enabling reconciles the completion count once and resubscribes without duplicate handlers.

BlockUI increments comlete_num → NotifyPieceCompleted() publishes that count through GameManager.PieceCompleted → Timer records the pending count → on its next eligible Update, a changed count requests one AddCompletionBonus and skips that frame's decrement. Multiple completions before that Update coalesce to one bonus, matching the old observed-count behavior. Repeated notification of the same count does not grant another bonus. BlockUI placement and score logic are unchanged apart from the notification call.

PuzzleTimer reaches zero → Expired once → Timer compatibility handler calls GameManager.BlockFail() → Timer UI activates Failure Panel. The model itself never references or activates a panel. Zero is clamped; a stopped/expired model cannot gain bonus time or restart without Reset. One intentional boundary change: failure now occurs in the Tick that reaches zero, rather than the old next Update after Slider clamping. Success blocks subsequent ticking/bonuses.

Changed → Slider range/value updated from the model using SetValueWithoutNotify. Slider changes are reset to the model value and never update gameplay time. Missing/destroyed Slider does not prevent calculations or failure handling. Retry reloads the scene and StageLoad supplies a fresh model at the configured limit with no stale expiry/subscriptions.

## Changed files

Added: Assets/Scripts/PuzzleTimer.cs and meta, Tests/PuzzleTimerTests.cs, this document.

Runtime/scene changes: Timer.cs, StageLoad.cs, GameManager.cs (completion event only), BlockUI.cs (notification only), Assets/Scenes/Puzzle.unity (one reference plus removal of the obsolete setting). Timer.cs.meta is unchanged. No existing runtime file was deleted or renamed; the old calculation implementation was replaced inside Timer.cs.

Supporting changes: Tests/StartupRegressionTests.cs (old Slider-authoritative test replaced by the new test suite), Tests/FullFlowTests.cs, Tests/PrepareFullFlowValidation.ps1, Tests/README.md and Docs/AI/UnityProjectContext.md.

## Verification

PuzzleTimerTests covers standalone start/pause/reset/configuration changes, cap, single expiry, zero limit, Slider tampering, no-Slider failure, off-Puzzle/success pause, duplicate/count-batched bonus notifications, re-enable and reinitialization. RealPieceRequestsOneTimerBonus uses a real scene piece and drag callbacks. Existing full-flow tests additionally check full time before countdown, time frozen after success and a fresh clock after timeout/retry, across all nine stages.

Manual Editor checks: begin from Main, verify the full bar through 3/2/1, observe normal drain after start, place a piece after some time elapses, confirm one capped bonus, allow timeout and retry, complete a stage and observe a frozen bar. Change a StageData Time Limit Seconds / Time Bonus Seconds outside Play Mode and reenter that stage. On Puzzle/EventSystem check Puzzle Timer UI; on Canvas/Timer check Failure Panel. Removing only the Slider in a disposable test scene must not disable time/failure logic.

Final validation: Unity 6000.5.8f1, Logs/puzzletimer-results.xml reports Passed: 33 passed, 0 failed, 2 optional explicit lighting comparisons skipped (35 discovered). No C# compilation errors or NullReferenceException appeared in Logs/puzzletimer-tests.log. All eight requested timer behaviors passed, including standalone/no-Slider operation, 30.5/17/12/0-second test configurations, bonus coalescing/deduplication/cap, zero-time failure, real piece notification, success freeze and real-scene retry. Full navigation and completion coverage still includes all nine stages. Timer.cs.meta has no diff. Rendering/lighting and StageData asset values were not modified in this step.
