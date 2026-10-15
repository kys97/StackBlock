# Puzzle 3D drag preview — 2026-09-25

후속 변경: 배치 가능 위치의 색상 점멸과 ClickImage Inspector 설정은 [PuzzlePlacementHint.md](PuzzlePlacementHint.md), 현재 방향/배치 기준은 [PuzzlePlacementAnchors.md](PuzzlePlacementAnchors.md)를 참고한다. 2026-10-15 가림 방지 변경은 이 문서 마지막 절에 있다. 아래 내용과 검증 수치는 최초 3D Preview 구현 단계의 기록이다.

현재 소유자는 BlockUI와 GameManager.TryCompletePiece다. PuzzlePieceUI, PuzzlePieceView, PuzzleController는 이 프로젝트에 없으며 이번 기능을 위해 추가하지 않았다.

## 동작과 책임

- BlockUI는 기존 UI 드래그와 최종 TryCompletePiece 호출을 유지한다. 시작 시 ScrollRect의 viewport(미지정이면 Scroll View 자체), Canvas의 UI 카메라, Puzzle Camera, Ground와 Surface 오브젝트를 보관한다.
- UI 목록 안에서는 기존 MoveImage와 크기를 사용한다. 목록 밖이면서 카메라 화면 안에 있고 Raycast가 맞으면 UI Image만 숨기고 3D preview를 표시한다. 목록으로 돌아오거나 Raycast가 빗나가면 3D를 숨기고 UI를 다시 표시한다. 경계 왕복은 같은 드래그의 미리보기를 재사용한다.
- PuzzleDragPreview는 실제 Block.Object의 MeshFilter.sharedMesh와 Transform 계층을 읽어 독립된 시각 오브젝트를 만든다. 부모의 localPosition/localRotation/localScale까지 빈 Transform으로 복제하므로 회전과 비균일 스케일 조합도 보존한다. 원본의 부모나 정답 Transform을 수정하지 않는다. 게임 스크립트/Collider/종속 조각 표시 로직은 복제하지 않는다. SkinnedMeshRenderer가 있을 때는 시작 시 자세만 BakeMesh하여 소유한 임시 Mesh를 정리한다. 현재 조각들은 MeshRenderer를 사용한다.
- PuzzleDragRaycaster는 드래그 중 Ground·Surface의 기존 Collider를 재사용한다. 9개 Ground의 기존 Collider는 실제 지형보다 아래쪽의 배경판이며 Surface에는 Collider가 없다. 실제 보이는 지형/배치 표면에 맞추기 위해 Collider가 없는 MeshFilter에는 별도 오브젝트의 임시 MeshCollider를 만든다. 원본 씬 오브젝트, Mesh, Prefab에는 Collider를 추가하지 않는다.
- 입력은 Camera.ScreenPointToRay → 캐시된 Collider.Raycast → 가장 가까운 hit.point 순서다. 가려진 Surface는 원본 activeInHierarchy를 따라 제외한다. preview가 지형에 반쯤 묻히지 않도록 hit.normal 방향으로 자신의 실제 bounds 하단만큼 띄우며, 원본 회전은 그대로 둔다. 이 보정은 시각적 위치에만 적용된다.
- 드래그 중 LateUpdate는 저장된 포인터 위치로 투영을 갱신하므로 마우스가 정지한 상태에서 카메라가 회전해도 미리보기가 따라온다. Find/GetComponent/Resources.Load는 프레임 루프에서 반복하지 않는다. MeshCollider 제작 시 한 번 Physics.SyncTransforms를 호출한다.

## 표현과 수명

Resources/Shaders/PuzzleDragPreview.shader는 Built-in 전용 반투명 Lambert 셰이더다. 원본의 색과 MainTex·UV 스케일/오프셋을 읽어 별도의 런타임 Material에 복사하며 불투명도는 0.55다. 그림자는 투사하지 않는다. 원본 Material의 Shader/Color/Emission/Glossiness나 Texture는 변경하지 않는다. 셰이더는 첫 사용 시 한 번 읽고 재사용하며 Resources에 두어 빌드에서도 포함된다.

각 드래그는 시각 root와 별도 Raycast root, 임시 Material(및 필요한 경우 baked Mesh)을 소유한다. 레이어는 Ignore Raycast이며 Raycast 후보는 명시적으로 보관한 Collider 목록으로 한정된다. Drop/취소/Pause/비활성화/씬 이탈 때 즉시 숨기고 Destroy한다. 시간 만료 때는 다음 LateUpdate에서 정리되어 멈춰 있는 포인터에도 잔상이 남지 않는다.

Drop은 기존 GameManager.TryCompletePiece(key, screenPosition)만 호출한다. 방향, 선행 조건, snap distance, 중복 완료, 점수, 시간 보너스, 성공/실패 규칙을 새로 구현하지 않았다. 성공하면 기존 실제 Object 활성화·UI 조각 삭제가 수행되고, 실패하면 원래 자리를 유지하던 UI 조각이 남는다. Pause 중 새 드래그는 기존 CanAcceptInput으로 거부하며 Resume 뒤 예전 pointer release는 무시된다.

## 파일과 연결

- 수정: Assets/Scripts/BlockUI.cs. 기존 key/move_prefab 및 MonoScript GUID 보존.
- 추가: Assets/Scripts/PuzzleDragPreview.cs, PuzzleDragRaycaster.cs와 각각의 meta.
- 추가: Assets/Resources/Shaders/PuzzleDragPreview.shader 및 폴더/셰이더 meta.
- 검증: Tests/DragPreviewTests.cs, PrepareFullFlowValidation.ps1.
- Scene/Prefab/StageData/Material/Lighting/FBX 수정 없음. 수동 Inspector 연결 없음.

## 검증

Unity 6000.5.8f1 격리 복사본에서 실제 9개 스테이지를 사용했다. 기존 작업에 남아 있던 검증 복사본의 Egypt 에셋 차이 3개는 현재 원본을 복사해 일치시켰다. 원본은 변경하지 않았다.

초기 11개 동작 테스트는 통과했으나 화면 점검에서 배경판 Collider만 사용하면 preview가 실제 지형에 가려지는 것을 발견했다. 실제 Mesh 기반 임시 Raycast 표면으로 보완했고, 후속 시각 테스트 1개 통과 및 Logs/drag-preview-world-surface.png에서 반투명 나무 조각을 확인했다. 초기 결과는 Logs/drag-preview-results.xml, 보완 시각 검증은 Logs/drag-surface-visual-results.xml에 보존했다.

최종 테스트는 UI↔3D 전환, 실제 크기·원근, 원본 Transform/Material 보존, 복잡한 부모 스케일, 카메라 4방향 회전, 정답/오답 Drop, Pause/시간 만료/씬 이탈 정리와 기존 9개 스테이지의 배치·보너스·성공·실패·재시작 흐름을 포함한다. UI 드래그는 실제 BlockUI의 EventSystem 핸들러에 PointerEventData를 전달한다. 물리 마우스나 WebGL 브라우저 동작/Player 빌드는 별도 검증하지 않았다.

최종 결과: `Logs/drag-preview-final-results.xml` — 30/30 통과, 실패/건너뜀 0, 380.61초. 구성은 미리보기 통합 12개, 비균일 Transform 복제 1개, 9개 스테이지 전체 진행 회귀 9개, Pause/전환/성공·실패 회귀 8개다. `Logs/drag-preview-final.log`에서 C# 컴파일/셰이더 오류 및 런타임 예외가 없음을 확인했다.

작업 시작 시점 `Logs/drag-preview-baseline.json`의 기존 Assets/ProjectSettings 파일과 비교했을 때 변경된 기존 파일은 BlockUI.cs 하나다. 원본 Material, FBX, Lighting, Scene, Prefab, StageData는 동일하다. 최종 런타임 코드·셰이더는 검증 복사본과 파일 해시가 일치한다.

## Geometry-based preview visibility — 2026-10-15

The user requested raycast-based avoidance of already placed objects. BlockUI now owns a small disposable PuzzlePreviewOcclusion query for each drag. Its inputs are the existing Ground and other Block.Object roots, cached in Start. It makes mesh-only collider copies once when first needed; there is no dependency on the source having a Collider. Disabled/unplaced renderers do not block the preview, and the dragged source is excluded. Collider transforms are snapshots of the static puzzle geometry; this does not implement moving/deforming obstacles. Skinned geometry is baked once per drag. Original colliders, renderer materials, prefabs and transforms are untouched.

The query uses a camera-oriented **BoxCastNonAlloc**, the volume equivalent of a raycast. A center ray alone would miss thin occluders covering the edges of a large piece. The combined bounds of every preview renderer, including the logical anchor, define the swept box. A small 0.005-world-unit clearance separates it from the first obstacle. OverlapBoxNonAlloc also checks the initial box because a sweep starting in overlapping geometry is insufficient. Queries use Ignore Raycast layer explicitly and accept only this drag's owned proxy colliders; unrelated colliders and the separate placement-query proxies cannot influence the result. Result buffers grow when full instead of silently omitting hits and are otherwise reused without per-frame array creation.

PuzzleDragPreview.AnchorWorldPosition is now the stored **logical** anchor from the unchanged placement projection. VisualAnchorWorldPosition reports the disposable root's rendered anchor. After camera-relative rotation resets the base pose, the visual is translated along the camera-to-anchor ray, ahead of the nearest occluder. This preserves the anchor's screen position, world scale, authored image direction and child local transforms. Returning to a clear view restores the original pose without accumulating offsets. Camera motion refreshes the query in LateUpdate even without pointer events. Hint and TryCompletePiece still use the logical anchor, so moving only the visual cannot award a false snap or move the actual target on Drop.

If the initial box is blocked or the required offset would cross the camera near plane, a full-size 3D preview cannot safely fit; the existing UI drag image is shown instead. A saved logical position allows recovery on the next frame without pointer movement. Such a fallback cannot place the piece until the 3D view becomes available again. The projection-list boundary behavior is unchanged. Pause, expiry, Drop, disable and scene exit dispose of both visual and occlusion proxies immediately. No shader/depth-test override or material change is used.

Display tradeoffs: camera pull-in can make the preview appear larger through normal perspective, and conservative whole-piece bounds can move it slightly earlier than exact triangle visibility would require. This keeps the existing world scale and placement rules. No Inspector wiring, package, layer-settings, render-pipeline, Material, FBX, Lighting, scene or prefab edit is required. Runtime scope: BlockUI.cs, PuzzleDragPreview.cs and new PuzzlePreviewOcclusion.cs/.meta. The user's pre-existing Spring_BackGround.mat edit is retained.

Initial Play Mode validation: `Logs/preview-occlusion-targeted-initial-results.xml`, 3/3 passed. Covers a thin edge occluder with no source Collider, clearing/restoring without drift, camera-overlap fallback/recovery, and an actual Egypt Big_pyramid/Small_pyramid drag with UI re-entry, Pause, unchanged target matrix, hint, score and time bonus. `Logs/preview-occlusion-egypt.png` was visually inspected: the translucent small pyramid remains visible over the already placed large pyramid.

Final validation used Unity 6000.5.8f1 with graphics enabled in the isolated `Builds/CameraRelativeValidation` copy. Existing preview/anchor/direction/hint/placement regression: `Logs/preview-occlusion-regression-results.xml`, 44/44 passed, 359.57 seconds. Final targeted geometry/integration/cleanup checks: `Logs/preview-occlusion-targeted-results.xml`, 6/6 passed, 46.75 seconds. One cleanup test is shared between the two runs (49 distinct tests). The final run includes the scene-teardown null guard and checks wrong Drop, Pause, expiry and scene exit.

An independent visibility oracle raycasts from the camera to every final renderer-bounds corner against the actual placed Egypt pyramid mesh, sampling after BlockUI.LateUpdate with a stationary pointer during four alternating camera turns. Speed 1: 1,030 animated frames; speed 10: 105 frames. Both recorded zero obstructed corner rays, zero hidden frames and unchanged logical placement anchors. This supplements the existing camera-relative rotation regression; it does not claim exhaustive visibility at every screen position on every stage.

No C# compile errors, shader errors or unexpected runtime exceptions were reported. Existing imported-folder `.meta` parser warnings remain unrelated to this change. Runtime source hashes match the validated copy. Comparing existing Assets/ProjectSettings files against `Logs/preview-occlusion-baseline.json` shows only BlockUI.cs and PuzzleDragPreview.cs changed; PuzzlePreviewOcclusion.cs/.meta are new. Source Material, FBX, Lighting, scene and prefab contents are unchanged, including the user's pre-existing material edit. EventSystem drag handlers were exercised programmatically; physical mouse interaction and a standalone Player build were not tested.

## Preview immediately outside the piece list — 2026-10-15

This follow-up supersedes the earlier requirement to hide the 3D preview outside the board. BlockUI still shows the UI image inside the ScrollRect viewport. Immediately outside that viewport, within the Puzzle Camera pixel rectangle, it now displays the 3D preview even when Ground/Surface projection misses. A camera-facing plane through the current piece's target anchor supplies a finite visual position at the piece's usual camera depth. It uses ScreenPointToRay and Plane.Raycast, without moving the real target or changing world scale. This avoids extending a ground-plane ray far behind the map.

The original PuzzleDragRaycaster and its board-boundary/anchor-height projection are unchanged. BlockUI stores a separate hasPlacementHit flag; both the hint and Drop require that flag before calling the existing GameManager placement checks. A floating preview cannot snap or earn rewards, even if its visual position happens to be within snap distance. Returning to the board resumes the original placement projection; returning to the list hides the same preview and restores the UI image. Camera-relative direction, occlusion avoidance and drag cleanup remain shared. The first exit from the list during a camera turn can create a floating preview immediately; otherwise the existing rule that freezes projected position during camera animation is retained. The exceptional near-camera occlusion fallback and handling outside the camera viewport remain as before.

Runtime change for this follow-up: BlockUI.cs only. No Inspector wiring or asset changes are required. PreviewListExitTests.cs covers all nine stages at one pixel beyond the list edge, off-board pointer following, no floating-position hint/Drop even with a deliberately permissive runtime snap override, list re-entry, valid Drop, and exiting during rotation at speed 1/10 followed by Pause. The Egypt boundary regression now expects a visible floating preview while continuing to reject off-board placement.

Validation: Unity 6000.5.8f1, isolated graphics-enabled Play Mode. `Logs/preview-occlusion-list-exit-results.xml` initially recorded 20 passes and 12 failures: the new boundary helper incorrectly chose the outside-screen edge of the saved right-hand list, and one screen-position assertion exceeded its overly strict 0.01-pixel tolerance by 0.0045 pixels. The helper now chooses the edge toward the puzzle for either list side; screen correspondence permits at most 0.1 pixel. Runtime code was unchanged between runs. The corrected follow-up `Logs/preview-occlusion-list-exit-followup-results.xml` passed all 12 affected tests in 101.57 seconds. Together with the 20 unchanged regression passes, all 32 relevant cases passed. The initial result remains preserved. Screenshots `Logs/preview-list-exit-egypt.png` and `Logs/egypt-floating-preview.png` were visually inspected for boundary and off-board display. No C# compile errors, shader errors or unexpected runtime exceptions were found; tested runtime files match the source hashes. Physical mouse input and a Player build were not tested.
