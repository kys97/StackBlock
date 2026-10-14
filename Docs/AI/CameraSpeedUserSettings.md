# Main 카메라 속도 사용자 설정 — 2026-09-25

기존 Main/Canvas/환경설정창을 사용한다. 음악 Slider와 mute, close 버튼 및 환경설정창 배경은 그대로 유지한다. 기존 저장/적용 버튼이 없어 변경 즉시 저장한다.

## 코드와 데이터

- GameSettings.cs: MonoBehaviour나 Singleton이 아닌 작은 static 설정 클래스. Key `CameraRotationSpeedLevel`은 이 파일의 상수 한 곳에만 있다. PlayerPrefs GetInt/SetInt와 Save, 기본값 3, 1~10 보정을 담당한다. 읽기만 할 때 저장값을 덮어쓰지 않는다.
- CameraSpeedSettingUI.cs: 창이 켜질 때 저장값을 읽어 Slider와 숫자를 표시한다. SetValueWithoutNotify로 초기 표시에 저장 이벤트가 발생하지 않게 한다. OnEnable/OnDisable에서 리스너를 연결/해제한다. Update나 GameManager 참조가 없다.
- GameManager.cs: 기존 CameraRotationSpeed 읽기 API를 유지하되 GameSettings를 읽는 호환 Property로 변경했다. 기존 Inspector 필드와 Main 씬에 저장된 값 5를 제거했다. 첫 실행은 요청대로 3이며 이전 개발자 Inspector 값이 사용자 저장값과 경쟁하지 않는다. 별도 default 필드도 만들지 않았다.
- PuzzleCameraController.cs: Initialize와 유효한 회전 요청 시 설정을 읽어 기존 `90f * level` 변환을 사용한다. 프레임마다 PlayerPrefs를 읽지 않는다. Time.deltaTime과 최종 정규 위치/90도 스냅은 그대로다.

## UI

Main/Canvas/환경설정창/CameraSpeedSetting 아래에 TitleText, SlowText, FastText, ValueText, CameraSpeedSlider를 추가했다. 제목은 카메라 회전 속도, 양끝은 느림/빠름, 숫자는 정수 1~10만 표시한다.

기존 음악 Slider의 이미지, 색상, 핸들을 재사용하고 오디오 콜백은 새 Slider에서 제거했다. 제목/숫자는 기존 배경 음악 라벨의 폰트·색상·크기 50을 사용한다. 느림/빠름은 보조 라벨 크기 32다. 기존 버튼, Panel, 음악 설정의 위치와 직렬화 참조는 보존했다. 설정 컨트롤과 Text 참조는 씬에 연결되어 있어 수동 Inspector 설정이 필요 없다.

Main 씬은 격리된 Unity Editor에서 생성한 신규 UI 블록과 Panel 자식 참조만 병합했다. 기존 GameManager의 폐기된 속도 필드를 제외한 다른 원본 블록은 변경하지 않았다. Material, Lighting, FBX, Resources, Puzzle 씬, 점수·Timer·배치 로직은 변경하지 않았다.

## 검증 방법

Unity 6000.5.8f1, Temp/MaterialValidation 복사본에서 실제 Main/Topic/Stage/Puzzle 씬을 사용한다. 테스트 복사본만 productName을 StackBlock_CameraSettingsValidation으로 변경하여 실제 사용자 PlayerPrefs와 격리했다. 일반 테스트는 해당 키를 보관·복원한다.

- GameSettingsTests: 첫 실행 기본값, 읽기/쓰기 범위 보정.
- FullFlowTests: 기존 환경설정 버튼을 포인터로 열고 Slider 포인터 입력, 즉시 숫자/저장 갱신, 반복 열기, 오디오 연결 보존, Main→Topic→Stage→Puzzle의 1/10 속도와 90도, Main 복귀 후 7 유지.
- PuzzleCameraTests 및 기존 Pause 테스트: 1~10 단계 × 30/60/144 FPS, A/D/화살표/버튼, Q/E 무반응, 정지·재개·Back·드래그·타이머 동작.
- GameSettingsRestartTests.SaveSevenBeforeExit와 ReadSevenAfterRestart는 각각 별도의 Unity 프로세스에서 실행한다. 첫 실행에서 UI로 7을 저장하고 에디터가 종료된 뒤 새 Play Mode에서 Slider와 숫자가 7인지 확인한다.

WebGL 브라우저 재실행·물리 마우스/키보드 검증은 포함하지 않는다. 자동화는 실제 EventSystem/Slider 포인터 경로와 기존 키 입력 테스트 경로를 사용한다.

## 검증 결과

- Unity C# 컴파일 오류 0. 실제 변경된 Script 4개와 Main 씬은 검증 복사본과 SHA256 일치.
- `Logs/camera-settings-playmode-results.xml`: 62개 중 58 통과, Main UI 4개 실패. 배치 Game View가 640×480으로 실행되어 기존 환경설정 버튼 중심이 (709, -114), depth 3으로 화면 밖에 있었다. 렌더링 대기만 추가한 진단 재실행 1개도 같은 이유로 실패했다. 이 증거는 `camera-settings-ui-diagnostic-results.xml`과 로그에 보존했다.
- 테스트 전용 Game View 크기를 1920×1080으로 지정했다. 제품 Canvas나 기존 UI 위치를 수정하지 않았다. `Logs/camera-settings-ui-results.xml`: UI 4개와 별도 재실행 전 저장 1개, 총 5/5 통과. 실제 UI 이미지 `Logs/camera-settings-main.png`도 확인했다.
- `Logs/camera-settings-restart-results.xml`: 이전 Unity 프로세스 종료 후 새 프로세스에서 저장된 7을 읽어 Slider/ValueText 확인, 1/1 통과.
- 따라서 서로 다른 64개 검증 항목을 확인했다(58 + UI 재실행 4 + 저장/새 프로세스 읽기 2). 하나의 실행에서 64개를 모두 통과한 것으로 표현하지 않는다.
- Resources/ProjectSettings/Puzzle 씬 797개 파일은 작업 시작 시점의 해시와 같았다. Main의 RenderSettings/Light 및 기존 UI 블록도 보존했다. 기존 로컬 Egypt Material 편집은 건드리지 않았다.
- 작업 전 원본 Editor 로그에 StageLoad.Update NullReferenceException과 Sound의 자식 GameObject DontDestroyOnLoad 경고가 있었다. 검증한 정상 Main 진입 흐름에서는 예외가 발생하지 않았으며, 기존 Sound 경고는 유지된다. 이번 작업에서 해당 로직을 변경하지 않았다.

API 참고: [Unity PlayerPrefs](https://docs.unity.com/engine/6000.0/script-reference/unityengine/playerprefs).
