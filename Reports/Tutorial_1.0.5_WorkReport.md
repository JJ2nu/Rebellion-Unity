# 튜토리얼 개편 및 전투 연출 조정 작업 보고서 (빌드 1.0.5)

- 작업일: 2026-10-18
- 브랜치: `assetRefactor` (커밋 전, 작업 트리 상태)
- 빌드: Windows Release **1.0.5** (`Builds/1.0.5/ReBellion.exe`, 빌드 결과 Success)

---

## 1. 요약

| 구분 | 내용 |
|---|---|
| 튜토리얼 구조 | 기존 1스테이지를 **1-1 / 1-2** 두 단계로 분리하고, 조작법 팝업 대신 **스테이지 위 대사(미연시형)** 로 진행 |
| 1-1 | 권총 기물을 지정 칸·지정 방향으로 강제 배치 → 공격 규칙 설명 → 나머지 대원 자유 배치 |
| 1-2 | 적 3기 + 단검 1기. T자 구도로 단검을 강제 배치해 **적 오인사격**을 보여줌 |
| 공격 기준 UI | 배치·호버 시 보드 위에 **사선**(공격 대상 / 막힘 / 대상 없음 / 관통 아군 경고) 표시 |
| 전투 연출 | 페이즈를 겹쳐 **한순간에 일어나는 전투**처럼 보이도록 개편 (총성 시점 약 3.9초 → 약 1초) |
| 버그 수정 | 총알 90도 회전, 총알 발사 지연(리코일 불일치), 튜토리얼 중 재시도 미대응 |
| 리소스 | 엘리자 idle 시트 흔들림 보정 + 텍스처 메모리 약 1/3.3 |

---

## 2. 튜토리얼 설계

### 2.1 확정 방향
- 튜토리얼에서 보여줄 것: **각 기물의 행동 방식 / 공격 판정 기준 / 오인사격**
- "시작의 총성"은 기능이 단순해 튜토리얼에서 제외 (기획 확정)
- 강조 연출은 시작의 총성 스코프와 시각 언어가 겹치지 않도록 **어두운 덮개 + 구멍 + 맥동하는 모서리 괄호**로 결정

### 2.2 공격 판정 규칙 (코드 기준 정리)
- 정면 직선, 사거리 안에서 **가장 가까운 기물이 적일 때만** 공격 (주먹 1칸 / 단검 2칸 / 권총 6칸)
- 공격 여부는 시뮬레이션 시작 순간 한 번 정해지고, 실행은 **주먹 → 단검 → 권총** 순서
- 실제 피해는 물리 판정이며 진영 확인이 없음. **총알은 관통**하므로 사선 위 아군도 맞음 → 오인사격의 원리

### 2.3 스테이지 흐름
```
1-1 (stage_001)  ──확정──▶  1-2 (stage_001_2)  ──확정──▶  Stage 1 대사(Dialogue)  ──▶  stage_002
       └ 대사 Scene 없이 Stage Scene에서 바로 이어짐
```

### 2.4 1-1 진행
1. 도입 대사 3줄 (공격 규칙)
2. **권총 강제 배치**: 권총 슬롯만 열림 → 지정 칸(35)·지정 방향(남)에서만 배치 가능
3. 규칙 설명 대사 (사거리, 관통, 행동 순서, 사선 읽는 법) — 방금 놓은 권총의 사선을 고정 표시
4. 남은 대원 자유 배치 → 시작
5. 결과별 대사 → 다시 하기 시 자유 배치 단계로 복귀

### 2.5 1-2 진행 (T자 오인사격)
```
          [적 주먹]          ← 단검 쪽을 보지 않음
              ↑ 단검 돌진 (2칸)
[적 권총 →]  [단검]  [← 적 권총]
```
- 배치 전: 두 사수는 서로가 가장 가까운 기물(같은 편)이라 공격 안 함
- 단검을 가운데 놓는 순간 두 사수가 모두 단검을 겨눔 (적 사선 상시 표시로 확인 가능)
- 단검이 먼저 움직여 자리를 비우면 → 두 사수의 총알이 서로를 맞힘 → 3킬, 아군 생존
- 오인사격 순간 "오인사격! 적 사수들이 서로를 쐈습니다." 안내

---

## 3. 구현 내용

### 3.1 대사형 튜토리얼 시스템
- 스테이지 JSON에 `tutorialSteps` 배열 추가 → **대사 수정은 JSON만 고치면 됨**
- 단계 종류

| type | 동작 |
|---|---|
| `line` | 대사 한 줄 (클릭/Space로 넘김) |
| `place` | 지정 기물을 지정 칸·방향으로 강제 배치 (`selectText` → `text` 2단계 안내) |
| `placeAll` | 남은 슬롯을 모두 배치할 때까지 대기 |
| `start` | 시작 버튼 개방·강조 후 시작 대기 |
| `result` | 시뮬레이션 종료 대기, 같은 진영 피격 시 `calloutText` 표시 |
| `waitRetry` | 다시 하기 시 `gotoId` 단계로 복귀 |
| `enemyLines` | 이후 적 사선 상시 표시 |

- `condition`(`win` / `fail` / `perfect`)으로 결과별 대사 분기
- 진행자: `GuidedTutorialDirector` (Stage Scene 소속, Scene이 내려가면 잠금·모달 모두 해제)

### 3.2 대사창 UI (미연시형)
- `ResultDialogPanel`의 대사창 아트·폰트·진행 버튼·SFX를 재사용한 전용 Prefab `Assets/Resources/UI/TutorialDialoguePanel.prefab`
- 대사창 배경: **화면 전체 너비**, 높이 250 (반투명이라 보드가 보임)
- 엘리자: 대사창 **앞**에 표시, idle 루프 애니메이션 (12fps)
- **슬롯 조작이 필요한 단계에서만** 대사창을 대원 버튼 뒤로 내리고 글자 폭을 줄임 (평소 1260 / 슬롯 단계 680)
- 진행 삼각형: 대사창 오른쪽 끝, 넘길 수 있을 때만 표시
- 타자 효과, **문장이 끝날 때마다 자동 줄바꿈**
- 결과 판정 패널이 뜨면 튜토리얼 대사창 자동 숨김

### 3.3 입력 제한 (`StageTutorialGate`)
- 단계별 허용 슬롯 / 전체 슬롯 잠금(대사 중) / 시작 버튼 잠금 / 강제 배치 칸·방향 / 강제 배치 기물 회수 금지
- 대사 진행 중 대원 슬롯 클릭 차단 (모달이 떠 있을 때 슬롯 클릭 무시 — 튜토리얼 외 화면에도 적용)
- 튜토리얼이 없는 스테이지에서는 모든 검사가 기존 동작을 그대로 통과

### 3.4 공격 기준 UI — 사선 표시 (`AttackLinePresenter`)
| 표시 | 의미 |
|---|---|
| 빨간 선 + 빨간 원 | 공격함, 원이 대상 |
| 빨간 선(보드 끝까지) + 주황 원 | 권총 관통 — 사선 위 아군도 맞음 |
| 회색 선 + 회색 X | 같은 편에 막혀 공격 안 함 |
| 흐린 선 | 사거리 안 대상 없음 |
- 배치 미리보기 중·기물 호버 중 표시, 1-2에서는 적 사선 상시 표시
- 현재 튜토리얼 스테이지에서만 동작

### 3.5 전투 타임라인 개편 (전 스테이지 적용)
| 시점 | 권총 | 주먹 | 단검 |
|---|---|---|---|
| 시작 | 조준 자세로 올리고 정지 대기 | 공격 시작 | 웅크린 자세로 정지 대기 |
| 0.52초 | 대기 | 타격 | 튀어나감 |
| 단검 돌진 30% 이후 | 카메라 포커스 도달 시 발사 | 복귀 | 돌진 |

- 페이즈 순서(주먹 → 단검 → 권총)는 유지 → 승패 판정·오인사격 동작 동일
- 다음 페이즈는 앞 기물의 **인계 지점**(주먹 타격, 단검 돌진 30%)에서 출발
- 단검 웅크림: `Slash.anim` 0.2초(몸이 가장 낮은 지점)에서 정지 후 이어서 돌진
- 권총: `Shoot2`를 1.6배속으로 올려 **반동 직전(1.17초)** 에서 정지 → 전투 카메라가 권총에 포커스(이동 70%, 최대 1초)를 잡으면 재생 → **반동 시작 프레임(1.19초)에 총알 생성**

### 3.6 버그 수정
| 증상 | 원인 | 수정 |
|---|---|---|
| 총알이 90도 돌아간 채 날아감 | 총알을 손 본(FlarePoint) 회전으로 생성 | 발사 방향을 바라보게 생성 |
| 총알이 리코일보다 늦게 나감 | 조준 정지 지점(1.3초)이 반동(1.19초) 이후 | 정지 1.17초, 반동 프레임에 생성 |
| 튜토리얼 결과 대사 중 다시 하기를 누르면 진행이 멈춤 | 결과 대사 중 Retry 입력이 버려짐 | 결과 이후 어느 시점의 Retry도 즉시 복귀 처리 |

### 3.7 UI·리소스 조정
- **HUD 다이아몬드 아이콘**: 크기 0.85배, HUD 높이 2.5 → 2.65 (아군·적·가이드 9개 Prefab)
- **확정 버튼**: 전용 리소스 전까지 플레이 버튼 이미지 임시 사용 (`StageSimulationControlsView` → `Use Play Sprite For Confirm`)
- **TMP 한글 줄바꿈**: `Use Modern Hangul Line Breaking Rules` 켬 (단어 중간 끊김 해결, **프로젝트 전역 설정**)
- **엘리자 idle 시트** (`idle.png` → `idle_packed.png`)

| 항목 | 원본 | 최적화 |
|---|---|---|
| 시트 | 13000×1500 (가로 13장) | 3020×1968 (5×3 격자) |
| 프레임 | 1000×1500 전신 | 604×656 상반신 |
| 텍스처 메모리(압축 추정) | 약 19.5MB | 약 5.9MB |
| 흔들림 | 프레임마다 몸 전체 최대 7px 좌우 이동 | 다리 중심 기준 정렬 (편차 0px) |

### 3.8 에디터 도구
- `Rebellion/Tutorial/Build Tutorial Dialogue Panel`: 튜토리얼 대사창 Prefab 재생성 (idle 시트 분할 포함)
- `Rebellion/Tutorial/Validate Tutorial Steps`: `tutorialSteps` 데이터 검사 (단계 종류, gotoId, 칸 범위, 가이드 기물 일치) — 현재 2개 스테이지, 문제 0건

---

## 4. 변경 파일

### 신규
- `Assets/02_Scripts/UI/InGame/Tutorial/`
  - `GuidedTutorialDirector.cs`, `TutorialDialogueView.cs`, `TutorialSpotlightOverlay.cs`, `AttackLinePresenter.cs`, `StageTutorialGate.cs`
  - `Editor/TutorialDialoguePanelBuilder.cs`, `Editor/TutorialStepValidator.cs`
- `Assets/02_Scripts/UI/ImageFrameLoop.cs`
- `Assets/Resources/UI/TutorialDialoguePanel.prefab`
- `Assets/StreamingAssets/Stages/stage_001_2.json`
- `Assets/04_Images/UI/new/idle_packed.png` (원본 `idle.png`는 소스로 보존, 빌드 미포함)

### 수정
- 흐름: `GameFlowManager.cs`, `StageSceneFlowBinder.cs`, `PlaytestLogger.cs`(하위 스테이지 번호 진행도 계산)
- 데이터: `StageDataModels.cs`, `stage_001.json`
- 배치·UI: `PlacementController.cs`, `InGameStorageUIController.cs`, `StageSimulationControls.cs`, `StageSimulationControlsView.cs`
- 전투: `SimulationController.cs`, `PieceBase.cs`, `BrawlerPiece.cs`, `SlasherPiece.cs`, `GunmanPiece.cs`, `CombatCameraPresentation.cs`, `CombatPresentationDirector.cs`
- Prefab: HUD 3종, 기물 9종
- 설정: `TMP Settings.asset`, `ProjectSettings.asset`(bundleVersion 1.0.5)

---

## 5. 조절 가능한 주요 값

| 위치 | 항목 | 현재값 | 의미 |
|---|---|---|---|
| 단검 Prefab | Crouch Hold Normalized Time | 0.128 | 웅크린 자세 정지 지점 |
| 단검 Prefab | Crouch Root Motion Distance | 0.28 | 웅크림 구간 이동량(돌진 거리 보정) |
| 단검 Prefab | Handoff Normalized Time | 0.3 | 권총이 쏴도 되는 돌진 진행도 |
| 권총 Prefab | Ready Aim Speed Multiplier | 1.6 | 조준 올리는 속도 |
| 권총 Prefab | Aim Hold Clip Time / Recoil Clip Time | 1.17 / 1.19 | 조준 정지 / 총알 생성 시점 |
| Simulator (CombatCameraPresentation) | Gunman Focus Settled Ratio | 0.3 | 카메라 포커스 판정(낮을수록 더 기다림) |
| 대사창 Prefab (TutorialDialogueView) | Full / Compact Text Width | 1260 / 680 | 글자 폭 |
| 대사창 Prefab (TutorialDialogueView) | Break After Sentence | 켬 | 문장 단위 줄바꿈 |
| 대사창 Prefab (Img_Char) | Frames Per Second | 12 | 엘리자 idle 속도 |
| HUD Prefab (Active/Inactive) | Scale | 0.85 | 다이아몬드 크기 |

※ 대사창 Prefab 값을 직접 고친 뒤 `Build Tutorial Dialogue Panel` 메뉴를 다시 실행하면 빌더 값으로 덮어써짐

---

## 6. 검증 상태

| 항목 | 상태 |
|---|---|
| Unity 컴파일 | 에러 없음 (Unity MCP로 매 변경 후 확인) |
| 튜토리얼 데이터 검사 | 2개 스테이지, 문제 0건 |
| Release 1.0.5 빌드 | Success |
| 플레이테스트 | 진행 중 피드백(총알 방향·타이밍, 캐릭터 흔들림, 대사창 배치 등)은 반영 완료. **최종 반영분 전체는 플레이 모드 재확인 필요** |
| 커밋 | **미커밋** (플레이테스트 후 커밋 예정) |

---

## 7. 남은 작업 및 확인 필요 사항

### 확인 필요
- 1-2에서 두 총알이 맵 소품에 막히지 않고 서로에게 닿는지
- 전투 타임라인: 히트스톱·시뮬레이션 스킵과 겹칠 때 모션 꼬임 여부
- TMP 한글 줄바꿈 전역 변경이 대사 Scene·결과 패널 등 다른 화면에 미치는 영향
- 사선 표시가 URP에서 정상 렌더되는지 (`Sprites/Default` 셰이더)

### 미구현 / 후속 과제
- HUD 행동 순서 숫자(1/2/3) — 현재는 대사로만 설명
- 엘리자 표정 전환 — idle 루프로 바꾸면서 표정별 Animator 제거됨 (표정별 시트가 오면 재도입 가능)
- 사선 표시 일반 스테이지 확대 적용 여부 결정
- 확정 버튼 전용 리소스 교체 후 임시 옵션 해제
- `idle_packed.png`는 Python으로 생성한 파일 — 원본 그림 변경 시 재생성 필요

### 작업 트리 정리 필요 (이번 작업과 무관)
- `Assets/04_Images/UI/new/I3GSvcManager (1).exe` — 출처 불명의 실행 파일이 에셋 폴더에 있음. 커밋 전 확인 후 제거 권장
- `Assets/_Recovery/` — Unity 크래시 복구 씬. 필요 없으면 삭제
- `Assets/03_Prefabs/UI/OutroGame/DialoguePanel.prefab`, `Assets/01_Scenes/Stage.unity`, `Assets/08_Materials/SoftVignette.mat` — 이번 작업에서 수정하지 않은 변경 사항

---

## 8. 참고: 작업 환경
- Unity 6000.4.2f1
- Unity MCP(MCPForUnity) 브리지(포트 6400)로 컴파일 확인·Prefab 생성·빌드 수행
- 빌드 시 MCP 호출이 타임아웃되며 같은 빌드 요청이 여러 번 재실행됨 (결과물 동일, 모두 Success). 다음부터는 긴 빌드를 다른 방식으로 실행 예정
