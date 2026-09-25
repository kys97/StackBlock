# Puzzle 일시정지와 카메라 속도 단계

2026-09-25. Unity 6000.5.8f1. 최신 요구사항: 1~10 속도 단계와 PausePanel의 BackButton.

## Inspector와 회전

Main 씬의 GameManager → Camera Settings → Camera Rotation Speed에서 정수 1~10을 선택한다. 기본값은 3이다. 이전 float 180 설정이 저장되어 있던 Main 씬은 명시적으로 3으로 이전했다. 속도 설정이 저장된 다른 Scene/Prefab은 없다.

최종 설정 원본은 GameManager.CameraRotationSpeed 하나다. PuzzleCameraController 내부 상수 90도/초에 단계를 곱하고 Time.deltaTime을 적용한다. 기준 상수와 실제 각속도는 Inspector에 표시하지 않는다. 런타임 중 단계 변경도 반영된다.

| 단계 | 90도 회전 시간 |
|---|---|
| 1 | 약 1초 |
| 3 (기본) | 약 0.333초 |
| 5 | 약 0.2초 |
| 10 | 약 0.1초 |

A/LeftArrow/Left 버튼과 D/RightArrow/Right 버튼은 기존 경로를 사용한다. 진행 중 추가 요청은 거부하며 완료 시 기존 정규 위치/회전으로 맞춘다. Q/E에는 동작이 없다.

## Pause UI와 입력

기존 Canvas/PuzzlePauseUI와 PauseButton을 유지한다. PausePanel은 기본 비활성이며 ResumeButton(계속하기)과 BackButton(돌아가기)을 가진다. 기존 버튼 이미지·폰트를 재사용하고 기존 ResumeButton 아래에 BackButton을 배치했다.

- Esc 또는 PauseButton: Playing 중에만 PauseGame.
- 다시 Esc 또는 ResumeButton: ResumeGame.
- GameManager는 IsPaused, PauseChanged 이벤트와 PauseGame/ResumeGame/TogglePause를 제공한다.
- Timer는 Pause 중 감소/보너스 처리를 멈춘다. 시간과 미처리 완료 보너스는 재개 시 이어진다.
- PuzzleCameraController는 Pause 중 회전 요청과 진행을 멈춘다.
- BlockUI는 PauseChanged로 드래그 복사 이미지를 즉시 숨기고 취소한다. 원래 조각 UI의 위치는 움직이지 않으며 Resume 후 기존 드래그/Drop이 이어지지 않는다.
- Ready/Success/Failed에서는 Pause 요청을 무시한다. Pause 여부와 퍼즐 진행 결과는 별개다.

## BackButton과 종료 순서

새 Scene 전환 클래스나 Scene 이름을 추가하지 않았다. BackButton의 UnityEvent는 기존 UI.ToStage()에 직접 연결했다.

UI.ToStage() → GameManager.EndPuzzle() → Stage 선택 상태 지정 → 기존 LoadScene("Stage") 순서다.

EndPuzzle은 기존 ResetPuzzleState를 재사용한다.

1. ResumeGame으로 Time.timeScale=1, IsPaused=false 복구 및 Pause UI 닫기.
2. 준비 카운트다운 취소 및 퍼즐 진행/점수/완료 개수/Dictionary 정리.
3. 카메라, 콘텐츠, 드래그 Canvas, 블록 부모, Ground, 카운트다운 씬 참조 해제.
4. 기존 UI 전환 코드로 Stage 씬 로드.

씬에 속한 실제 GameObject는 Unity 씬 전환이 해제한다. GameSession의 Topic/Stage 선택과 GameManager의 카메라 속도 단계는 유지한다. 기존 Restart/Main 이동과 직접 씬 전환의 timeScale 복구도 유지한다.

## 변경 범위

이번 추가 수정 Script: GameManager.cs, PuzzleCameraController.cs, UI.cs. 기존 PuzzlePauseUI.cs, BlockUI.cs, Timer.cs의 일시정지 기능은 재사용했다.

- Main.unity: cameraRotationSpeed 180 → 정수 3.
- Puzzle.unity: PausePanel에 BackButton과 이벤트 추가, ResumeButton 위치 조정.
- StageData의 rotationSpeedDegreesPerSecond 필드/Property 및 9개 asset의 값은 앞 단계에서 이미 제거했다. 이번에는 StageData를 추가 수정하지 않았다.
- Material, Lighting, FBX, Prefab Material 및 전체 Resources 파일은 이번 변경 전 해시와 동일하다.
- Inspector에서 수동 연결할 항목은 없다. 기존 Main → Topic → Stage → Puzzle 흐름으로 실행한다.

테스트 갱신: Tests/PuzzleCameraTests.cs, PauseTests.cs, FullFlowTests.cs. 검증은 실제 Unity Play Mode의 격리 복사본에서 수행한다. 키 입력은 실제 컨트롤러 입력 경로에 KeyCode를 주입하고, Pause/Resume/Back 버튼은 EventSystem Raycast로 수신 대상을 확인한 뒤 클릭한다. 물리 키보드 및 배포 브라우저 테스트나 Player 빌드는 포함하지 않는다.

## 최종 검증 결과

- Unity 6000.5.8f1 C# 컴파일 오류 0.
- Logs/pause-levels-playmode-results.xml: 최초 58항목 실행, 57 통과 / Back 클릭 테스트 1 실패.
- 해당 실패는 PausePanel 활성화와 같은 프레임의 렌더링 전 클릭이었다. BackButton Canvas depth가 활성화 직후 -1, 다음 프레임 25임을 로그로 확인했다. 화면 표시 후 클릭하도록 테스트에 한 프레임 대기를 추가했으며 제품 코드 수정은 없었다.
- Logs/pause-back-playmode-results.xml: BackButton 포인터 클릭 → timeScale/IsPaused 복구 → runtime 상태 정리 → Stage 진입 → Puzzle 재진입 테스트 재실행 1 통과 / 실패 0 (16.14초).
- 최종 58항목 모두 검증됨: 1~10 단계 × 30/60/144 FPS에서 정확한 90도; 실제 프레임에서 1/3/5/10 단계 회전 시간; A/D/화살표/좌우 버튼 및 Q/E 무반응; 회전 중 추가 입력 거부; Esc/Pause/Resume; 타이머·드래그·배치·진행 중 카메라 정지; 보너스 보존; Ready/Success/Failed Pause 차단; 모든 기존 씬 이동과 Back 복귀/재진입.
- Back 테스트는 씬 이동 이전에 Puzzle Dictionary, 점수/완료 개수/경과시간, Ground/BlockParent/PieceContent/DragCanvas 참조가 정리되었는지도 확인한다. Topic/Stage 선택은 유지된다.
- 원본의 변경된 Script/Scene은 검증 복사본과 파일 해시가 일치한다. 이번 수정 전후 Resources 파일 변경 0개, Lighting 변경 0개.
