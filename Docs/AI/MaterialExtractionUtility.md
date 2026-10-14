# FBX Material Extraction Utility — Remap 없는 안전 적용

2026-09-25. `Assets/Editor/MaterialExtractionUtility.cs`는 Editor 전용 도구이며 런타임 코드나 Inspector 연결을 변경하지 않는다.

## 메뉴와 처리 범위

- Tools → StackBlock → Materials → Analyze Material References: 읽기 전용 분석, Console 보고.
- Tools → StackBlock → Materials → Extract And Relink Materials: 다시 분석하고 충돌이 없을 때 확정된 항목만 적용.
- FBX: Assets/Resources/Fbx 재귀 검색.
- Prefab: Assets/Resources/Complete, Puzzle, Ground 재귀 검색. MeshRenderer / SkinnedMeshRenderer 및 비활성 자식 포함.
- Scene은 읽기 전용 보고 대상이며 저장하지 않는다. FBX Importer에는 쓰지 않는다. 기존 External Object Map은 재사용 대상을 확인할 때만 읽는다.

## Dry Run 및 적용 계획

| 항목 | 수량 |
|---|---:|
| FBX | 10 |
| 검사 Prefab | Complete 9 / Puzzle 83 / Ground 9 |
| 새 외부 Material | 82 |
| 재사용 Material | 46 (Spring 15 / Summer 15 / Desert 16) |
| 수정 Prefab | Complete 5 / Puzzle 73 / Ground 6 |
| 교체 슬롯 | 498 (유효 내장 참조 411 + 복구 87) |
| Spring 복구 | 34 |
| Summer 복구 | 26 |
| Desert 복구 | 27 |
| 매핑 충돌 | 0 |
| 자동 복구 제외 | 1 |
| 새 External Remap | 0 |

새 Material 대상: egypt_snowball_all 6, Bigben 24, Egypt 6, Opera house 12, TowerBridge 17, Fallhouse 1, Winter 16. 각 FBX 옆 `<이름>_Materials` 폴더에 원래 Material 이름으로 생성한다. Spring/Summer/Desert의 기존 파일은 이동하거나 덮어쓰지 않는다.

## 연결과 보존 방식

- 유효한 내장 Material: FBX 경로 + Material GUID/localID + 이름을 모두 확인한다.
- 이미 외부 .mat를 참조하는 슬롯은 유지한다. 전역 Material 이름 매핑은 없다.
- 복구: 깨진 PPtr의 원본 FBX와 Mesh의 FBX가 일치해야 한다. 정확히 같은 Mesh 서브에셋을 사용하는 원본 FBX Renderer들의 슬롯 개수 및 해당 슬롯 Material이 일치하고, 기존 외부 .mat로 확정될 때만 복구한다. 명시적인 빈 슬롯, Mesh 원본 불일치, 여러 후보는 연결하지 않는다.
- 내장 Material을 `new Material(source)`와 `AssetDatabase.CreateAsset`으로 복제한다. 원본 FBX 서브에셋은 남겨 둔다. AddRemap, ExtractAsset, SaveAndReimport를 사용하지 않는다.
- PrefabUtility.LoadPrefabContents/SaveAsPrefabAsset으로 지정된 슬롯만 저장한다. SerializedObject로 해당 배열 원소만 수정하여 다른 깨진 PPtr를 null로 바꾸지 않는다.
- Unity 저장 결과의 해당 슬롯 참조만 원래 Prefab 직렬화에 반영해, 구버전 Transform/Collider 등의 형식까지 갱신되지 않도록 한다. Renderer localID와 슬롯 개수가 확정되지 않는 inherited/binary 저장 구조는 충돌로 차단한다.
- 실행 직전 재분석, Prefab 파일 해시 검사, 신규 경로 덮어쓰기 차단, 기존 Prefab 백업과 실패 시 복원을 수행한다.

## 유지한 문제와 남은 참조

1. `Assets/Resources/Ground/OperaHouse.prefab` → `OperaHouse` → MeshRenderer → Element 1.
   - 기존 Missing Material: Summer.fbx GUID `fcb9733d5bc2ad74b8af75b1556868e6`, fileID `-2813467912831258332`.
   - Mesh는 `Assets/Resources/Fbx/Structure/Opera house.fbx`에서 왔다. 따라서 Summer의 어느 외부 Material이 의도되었는지 Mesh 슬롯으로 확정할 수 없다.
   - 기존 잘못된 PPtr도 그대로 보존하며 추측 연결하지 않는다.
2. `Assets/Scenes/TestScene_YS.unity`의 유효한 내장 Material 직접 참조 1개:
   `Assets/Resources/Fbx/egypt_snowball_all.fbx` → `Material #14`.
3. TestScene_YS의 `GameObject`에 기존 Missing Script 1개. 이번 Material 작업에서는 수정하지 않는다.

적용 후 Complete/Puzzle/Ground의 유효한 내장 Material 직접 참조는 0개다. 프로젝트 전체에는 TestScene_YS의 1개가 남는다. 별도로 위 Ground의 이미 깨진 FBX Material 참조 1개가 남으므로 전체 참조 정리가 끝났다고 볼 수 없다. 이후 Remap은 별도 작업이다.

## 검증과 기록

원본과 같은 Unity 6000.5.8f1 및 Assets/Packages/ProjectSettings의 격리 복사본에서 실제 Editor 코드를 실행한다. 통과한 신규 .mat/.meta 및 Prefab만 원본에 반영하며 기존 파일의 동시 변경 여부를 해시로 검사한다.

- Tests/MaterialUtilityValidation.cs: Dry Run 무변경, 이름 충돌 차단, 정확한 슬롯 목적지, 미복구 PPtr 보존, Prefab 구조/컴포넌트/메시/Transform/슬롯 개수 및 비재질 YAML 보존, Material 속성 보존, FBX와 전체 meta 및 Scene 보존, 재실행 무변경, 전체 Prefab/Scene Missing 검사.
- Tests/PrepareMaterialValidation.ps1: 격리 검증 프로젝트 준비.
- Tests/ApplyValidatedMaterials.ps1: 검증 성공과 원본 해시 일치를 확인한 뒤 결과 반영. 기존 Material 및 FBX/meta/Scene은 복사 대상이 아니다.
- MaterialExtractionAnalysis.json: 변경 전 Dry Run.
- MaterialExtractionApplied.json: 적용 항목 및 원본 백업 정보.
- MaterialExtractionAfter.json: 적용 후 재분석.
- MaterialExtractionValidation.txt: 검증 증거와 원본 반영 결과.

Material 비교에서 외부 에셋으로 바뀌며 제거되는 내장 에셋 소유 정보(m_PrefabAsset)와 hideFlags는 제외한다. 색상/Shader/Texture/키워드/렌더 큐 등 나머지 직렬화된 Material 설정은 원본과 비교한다. FBX/meta, 기존 .mat/meta 및 Scene은 파일 해시로 비교한다.

현재 작업 시작 전에 이미 수정된 Fallhouse_mat.mat, New Material.mat와 UserSettings 등 사용자 변경은 그대로 보존한다. 이번 검증은 Editor 에셋 처리 검증이며 게임 플레이나 새 Player 빌드는 수행하지 않는다.
