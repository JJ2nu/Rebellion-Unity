# Rebellion-Unity

Unity 포팅 프로젝트 — 4Q-Rebellion 프로토타입을 Unity로 새롭게 구현합니다.

## 프로젝트 개요

기존 [4Q-Rebellion](https://github.com/JJ2nu/4Q-Rebellion) 프로토타입을 참고하되, 코드를 재사용하지 않고 Unity에서 새로 구현하는 포팅 프로젝트입니다.

- **엔진**: Unity 2022.3 LTS
- **언어**: C# (.NET Standard 2.1)
- **렌더 파이프라인**: Built-in Render Pipeline (2D)

---

## 폴더 구조

```
Assets/
├── Art/
│   ├── Audio/          # BGM, SFX 오디오 클립
│   └── Sprites/        # 스프라이트, 스프라이트 시트
├── Materials/          # 머티리얼
├── Prefabs/            # 프리팹 (플레이어, 적, UI, 이펙트 등)
├── Resources/          # 런타임 로드 에셋
├── Scenes/             # Unity 씬 파일
└── Scripts/
    ├── Core/           # GameManager, SceneLoader, AudioManager, ObjectPool
    ├── Gameplay/       # PlayerController, PlayerAttack, HealthSystem, EnemyBase, Projectile
    ├── UI/             # HUDManager, MainMenuUI, PauseMenuUI
    └── Utils/          # Helpers, Singleton, EventBus

Packages/
└── manifest.json       # Unity 패키지 의존성

ProjectSettings/        # 프로젝트 설정 (렌더, 물리, 태그/레이어 등)
```

---

## 핵심 시스템

### Core
| 스크립트 | 역할 |
|---|---|
| `GameManager` | 게임 상태 머신 (Boot → MainMenu → Playing → Paused/GameOver) |
| `SceneLoader` | 비동기 씬 로딩 및 전환 |
| `AudioManager` | BGM 크로스페이드, SFX 재생 |
| `ObjectPool` | 오브젝트 풀링 (총알, 이펙트 등 재활용) |

### Gameplay
| 스크립트 | 역할 |
|---|---|
| `PlayerController` | 이동, 점프, 대시 (New Input System) |
| `PlayerAttack` | 근접/원거리 공격 |
| `HealthSystem` | 체력, 피격, 사망, 무적 프레임 |
| `EnemyBase` | 적 AI 기반 클래스 (순찰 → 추적 → 공격) |
| `Projectile` | 투사체 이동 및 히트 판정 |

### UI
| 스크립트 | 역할 |
|---|---|
| `HUDManager` | 체력바, 점수, 보스 체력바 |
| `MainMenuUI` | 메인 메뉴 (시작, 옵션, 종료) |
| `PauseMenuUI` | 일시정지 메뉴 (재개, 재시작, 메인 메뉴) |

### Utils
| 스크립트 | 역할 |
|---|---|
| `Helpers` | 수학 유틸리티, 컬렉션 유틸리티 |
| `Singleton<T>` | 싱글톤 기반 클래스 |
| `EventBus` | 시스템 간 느슨한 결합을 위한 이벤트 버스 |

---

## Unity 패키지

| 패키지 | 용도 |
|---|---|
| Input System | 플레이어 입력 처리 |
| TextMeshPro | 고품질 텍스트 렌더링 |
| Cinemachine | 카메라 제어 |
| 2D Animation | 스프라이트 애니메이션 |
| Timeline | 컷씬 / 이벤트 시퀀스 |

---

## 레이어 설정

| 레이어 | 설명 |
|---|---|
| Default | 기본 |
| Player (8) | 플레이어 |
| Enemy (9) | 적 |
| Projectile (10) | 투사체 |
| Interactable (11) | 상호작용 오브젝트 |
| Ground (12) | 지형 |
| Wall (13) | 벽 |

## 소팅 레이어

`Background → Midground → Player → Enemy → Projectile → UI`

---

## 시작하기

1. **Unity Hub**에서 Unity 2022.3 LTS 버전으로 이 프로젝트 폴더를 엽니다.
2. 패키지가 자동으로 설치됩니다 (`Packages/manifest.json` 기준).
3. `Assets/Scenes/` 폴더에 씬을 생성하고 개발을 시작합니다.
