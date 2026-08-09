# SYSTEMS.md — 구현 현황 문서
> **코드가 진실이고 이 문서는 스냅숏이다.** 기준 시점: **2026-08-10 (D7)**, 브랜치 `main`.
> ⚠️ §1~13은 D4 기준으로 작성됐고, **D5~D6 변경은 §15, D7 변경(보스 P2)은 §16** — 충돌하면 뒤 절이 우선.
> 설계 의도·미래 계획은 `GDD.md`, 이 문서는 "지금 실제로 돌아가는 것"만 기술한다.
> 큰 시스템이 추가·변경되면 해당 절만 갱신한다 (전면 재작성 불필요).

---

## 1. 한눈에 보기 — 코어 루프

```
Title ─[시작]→ MajorSelect ─[전공 선택]→ Combat (웨이브 1)
                                            │
              ┌─────────────────────────────┘
              ▼
        웨이브 전투 (전멸형 — 적을 다 잡아야 끝. 🔴 시간제 금지)
              │ 전멸
              ▼
        WaveEnded 이벤트 → AIBrain 감쇠(×0.8) + 프로파일 스냅숏 + DDA 판정
              ▼
        WaveInterval (timeScale 0) — 업그레이드 3택 카드 + COUNTER PROTOCOL 표기
              │ 카드 선택
              ▼
        다음 웨이브 Combat … (반복, 웨이브 7 진입 시 BossIntro 2초 → 보스전)
              │
              ├─ 보스 격파 → Result (승리, "AI가 학습한 나" 프로필)
              └─ 플레이어 사망 → Result (패배)
```

전투 중 상시로 도는 **학습 루프**: 적 탄이 플레이어에 접근(TTI≤0.5s) → 위기 이벤트 트리거 → 0.6s 안에 회피 방향(LEFT/RIGHT) 판정 → `DodgePredictor`에 관측 누적 → 표본 8개·우세 70% 도달 시 HIGH CONFIDENCE → **엘리트가 예측탄 발사**(플레이어가 피할 곳으로) → 플레이어가 반대로 속이면 `PREDICTION FAILED` 연출 + 모델 약화.

---

## 2. 상태 머신 (GameManager / GameState)

- `GameState` 7종: `Title, MajorSelect, Combat, WaveInterval, BossIntro, Result, Paused`. 단일 씬 `Main.unity` (🔴 씬 분리 금지).
- `GameManager`가 전환 규칙과 `Time.timeScale`의 **유일한 소유자**. Combat만 timeScale 1, 나머지 전부 0.
- 유효하지 않은 전환은 경고 로그 후 무시 (상태 머신 보호).

| From | To | 트리거 |
|---|---|---|
| (기동) | Title | `Start()` 강제 적용 |
| Title | MajorSelect | `StartRun()` — 시작 버튼 |
| MajorSelect | Combat | `SelectMajor()` — `RunStarted` 이벤트를 **먼저** 발행 후 전환 |
| Combat | WaveInterval | `BeginWaveInterval()` — WaveManager 전멸 판정 직후 |
| WaveInterval | Combat | `ContinueToNextWave()` — 업그레이드 카드 선택 |
| Combat/WaveInterval | BossIntro | `BeginBossIntro()` — 보스 웨이브 진입 |
| BossIntro | Combat | 2초 자동 (unscaled) |
| Combat | Result | `EndRun(won)` — 보스 격파 / `PlayerDied` |
| Combat ↔ Paused | | ESC 토글 |
| Paused/Result | Title | `ReturnToTitle()` |

- **히트스톱**은 GameManager 단독 소유 — `PredictionFailed` 이벤트에서만 0.12s (일반 피격에는 없음). 상태 전환 시 강제 취소.
- 연출 타이밍 2개만 SO 아닌 `[SerializeField]` (규칙 2의 명시적 예외): `_bossIntroDuration=2f`, `_predictionFailedHitStop=0.12f`.

## 3. 이벤트 버스 (GameEvents — 유일한 정적 버스, 6종)

| 이벤트 | 페이로드 | 발행 | 구독 |
|---|---|---|---|
| `GameStateChanged` | (prev, next) | GameManager | WaveManager, PlayerController, GameScreens |
| `RunStarted` | — | GameManager.SelectMajor | AIBrain 리셋, 체력 복원, 업그레이드 리셋, 웨이브 1부터 |
| `WaveEnded` | 웨이브 번호 | WaveManager | AIBrainRunner (감쇠 + 프로파일 스냅숏) |
| `ProjectileHitPlayer` | 탄환 InstanceID | Projectile | AIBrainRunner (**데미지 여부 무관** — 무적 관통도 §7.1 "피격") |
| `PlayerDied` | — | PlayerHealth | GameManager → 패배 Result |
| `PredictionFailed` | `PredictionFailedReport` (반영 전 방향/확률, 반영 후 확률) | AIBrainRunner | GameManager (히트스톱), PredictionFailedOverlay |

도메인 리로드 방어: `[RuntimeInitializeOnLoadMethod]`로 전 필드 초기화.

## 4. 웨이브 시스템 (WaveManager)

- **7웨이브, 전멸형 종료만** (🔴 계약). `WaveConfig_1~7.asset`이 구성표, `WaveConfig_7`은 보스 웨이브.
- 진행: Combat 진입 = 웨이브 시작 신호 → 구성표를 `_pending`에 적재 → **DDA 치환(셔플 전)** → Fisher-Yates 셔플 → 아레나 가장자리 링(반폭 12 × 반높이 7, 인셋 1) 랜덤 위치에 1.5s 간격 순차 스폰, 동시 생존 상한 10 (초과분 대기).
- 전멸 판정은 폴링(`PruneDead`): `_pending` 비고 `_alive` 비면 클리어.
- 보스 웨이브: 첫 진입 시 BossIntro만 태우고, 2초 후 재진입에서 실제 스폰. 보스 격파 시 **감쇠를 적용하지 않고** 즉시 승리 Result (최종 학습 상태를 결과 화면에 그대로 보존).
- 수치 전부 SO (`WaveSystemConfig_Default`: 스폰 간격 1.5 / 상한 10 / 아레나 12×7 / 인셋 1).

### 매크로 DDA (§6.3 — 구현됨)
- 입력은 **직전 웨이브 평균 교전 거리 하나뿐** (회피 학습과 완전 분리).
- 웨이브 4부터: 평균 거리 > 6 → 챗봇을 돌진형(코딩봇)으로, < 3.5 → 원거리형(그림봇)으로 **최대 30%만 치환** (총 적 수 불변, Spawn Budget 없음).
- `DdaConfig_Default.asset`: 임계 6 / 3.5, 비율 0.3 (슬라이더 상한도 0.3 고정), 발동 웨이브 4.
- 인터벌 패널의 `COUNTER PROTOCOL` 라벨이 치환 계획을 플레이어에게 공개한다.

## 5. AIBrain — 회피 습관 온라인 학습 (게임의 핵심)

`Scripts/AIBrain/`은 **순수 C#** (UnityEngine 참조 0건 — 규칙 3 준수 확인됨). `AIBrainRunner`(MonoBehaviour)가 유일한 어댑터. `Editor/AIBrainSelfTest.cs`가 가짜 이벤트 시퀀스로 51건 자체 테스트.

### 피격 위기 이벤트 (ThreatEventTracker — 🔴 계약값)
| 항목 | 값 | 비고 |
|---|---|---|
| 트리거 TTI | 0.5s | 탄의 최근접 예상 시각 |
| 판정 확정 창 | 0.6s | 이 안에 반드시 결론 |
| 최소 좌우 변위 | 0.3u | 미달 시 학습 표본 제외 |
| 위협 최대 근접거리 | 2u | ⚠️ GDD 미명시 — "사람 확인 필요" 주석 있음 |

- 탄환당 1회만 트리거, 동시 탄막에서는 TTI 최단 1개만 추적 (활성 워치 최대 1개).
- LEFT/RIGHT 기준축은 **탄환 진행 방향** (플레이어 기준 아님).
- 판정 종료 4경로: 피격 / 탄 소멸(회피 성공) / TTI 음수 전환(최근접점 통과) / 0.6s 만료.
- **피격 시에는 방향을 학습하지 않는다** (§7.1이 성공 분기에만 학습을 명시 — 구현 판단, 주석에 근거).

### 확률 모델 (DodgePredictor — 🔴 계약 수식)
```
P(dir) = (obs_dir + 1) / (obs_L + obs_R + 2)      // 가상 카운트 (1,1) 고정 합산
웨이브 종료 시: obs_L,R ×= 0.8                      // 감쇠는 관측 카운트에만
```
- 가상 카운트 1은 `const` 하드코딩 (계약이라 SO 미노출 — 의도적). 감쇠 0.8은 SO.
- 관측 0이면 정확히 50% — "아직 모른다"가 수식에서 나옴. 감쇠가 반복되면 자연히 50%로 회귀.
- **신뢰도는 이중 게이트 불리언**: 표본 ≥ 8 **그리고** 우세 확률 ≥ 0.70 → HIGH. **MED 단계는 없다** — UI 표기(LEARNING/LOW/HIGH 3종)는 HUD가 만든 것.
- 업그레이드 훅 2개: `ScaleObservations(0.2)`(행동교정 — 관측 80% 소거), `InjectFakeSamples(8)`(논문조작 — 우세 반대 방향에 가짜 표본, 방향 자동).
- `PredictorConfigSO.OnValidate()`가 계약값 4종 이탈 시 경고 로그 ("사람 승인 필요").

### 플레이 스타일 프로파일러 (PlayStyleProfiler — §6.4)
전 항목 시간 가중 집계: 평균 교전 거리(적 1기 이상일 때만) / 직전 웨이브 평균(DDA 유일 입력) / 무빙샷 비율 / 선호 4분면 / 8방향 이동 히스토그램. 소비처는 결과 화면 + DDA + (예정) 보스 P2. 인터벌·일시정지에서는 timeScale 0이라 자동 정지.

### 역카운터 판정 (🔴 §7.5 — 순수 C# `ThreatSample.IsCounterDodge`)
```
예측탄 AND 비피격 AND 학습표본자격(변위≥0.3) AND 회피방향 ≠ 예측방향
```
"AI의 예측을 읽고 깨뜨린 순간"만 집계. 어댑터는 카운트만 한다.

## 6. 전투 (Combat)

- **`IWeapon`** (`CanFire`/`Tick`/`Fire`): 쿨다운은 무기가 소유. 구현체는 `BasicWeapon`(플레이어 전용) 하나 — 전공별 최종 무기는 D6에 구현체 교체 예정. 적 `EnemyGun`은 의도적으로 IWeapon 미구현 (FSM이 타이밍 소유 — 이중 게이트 방지).
- **투사체 (Projectile)**: 수치는 전부 `Launch(...)`로 주입. 판정 순서 = 발사자 무시 → 벽(`ProjectileBlocker`) 소멸 → 팩션 불일치는 통과(아군 오사 없음) → 무적/사망 대상은 **탄을 소모하지 않고 관통** → 데미지 → 소멸(관통탄 제외). 정적 레지스트리 `Projectile.Active`를 AIBrain이 GC 없이 순회.
- **무적 관통 특칙**: 무적 중이라도 플레이어에 닿은 탄은 `ProjectileHitPlayer`로 AIBrain에 통보 (탄환당 1회) — "안 피했는데 회피 성공" 표본 오염 방지. 스폰 텔레그래프 중인 적이 방패가 되는 것도 방지.
- **넉백은 맞는 쪽이 결정**: 적은 `KnockbackDistance` 기반 감쇠 넉백 0.15s + 피격 플래시. **플레이어는 넉백 절대 없음 (🔴 계약** — 회피 변위 표본 오염 방지).
- 팩션은 `enum Faction {Player, Enemy}` 임시 구조 (레이어 매트릭스는 ProjectSettings 승인 대기).

## 7. 플레이어

- **PlayerController**: 구 Input Manager (`GetAxisRaw` — 스무딩 없음, 의도적), 대각 정규화, 마우스 좌클릭 홀드 자동 연사, 조준은 커서 방향. Combat 외 상태에서 잔류 입력 제거. 이동 속도 = SO 6 × 업그레이드 배수.
- **PlayerHealth**: MaxHp 100 + 보너스, i-frame 0.5s + 깜빡임, `PlayerDied` 발행. `hitDirection`은 받되 **의도적으로 미사용** (넉백 금지 계약).
- **PlayerUpgrades**: 업그레이드 배수 보관소 (공격력/연사/이속/탄크기/보너스HP). 퍼센트 스택은 **가산** (+20%×3 = 1.6배).
- **PlayerSpriteView**: 몸 방향 = **조준 방향** (이동 방향 아님 — "왼쪽으로 피하면서 오른쪽을 쏘는" 기본 동작이 화면에 보여야 함).

## 8. 적

공통 `EnemyBase`: 스폰 텔레그래프 0.5s (공격·피격 모두 불가, 스케일/알파 보간), 접촉 데미지는 `OnCollisionStay2D`(플레이어 i-frame이 연타 방지), `EnemyBase.Active` 레지스트리.

| 적 | FSM | 행동 |
|---|---|---|
| **챗봇 드론** | Approach→Aim→Cooldown | 사거리 8까지 직선 추적, 정지 후 0.3s 조준, 단발, 쿨다운 2s. 거리 유지 없음 |
| **그림봇** | SeekRange→Strafe→Telegraph→Reposition | 선호 거리 6~9 유지 + 횡이동, 0.4s 부풀림 후 3발 부채꼴(30°), 발사마다 횡이동 방향 반전 |
| **코딩봇** | Approach→ChargeTelegraph→Dash→Recovery | 탄 없음 — 몸이 위협. 0.4s 움츠림 때 방향 고정 후 10u/s × 0.6s 직진 돌진 (추적 안 함), 돌진 중 접촉 데미지 12 |

- **엘리트** = 챗봇 프리팹 + `EliteModifier` 컴포넌트. 예측탄 능력 부여(조준·발사 위임), 마젠타 조준선+마커 텔레그래프 0.35s, 탄 외형 교체(마젠타+트레일). 스탯 ×1.3은 전용 SO·프리팹 스케일로. `ActiveCount`가 HUD 표시 조건.
- **예측탄**: HIGH CONFIDENCE에서만, 공격 2회당 1회 (SO, N<2 금지 — 전탄 예측이면 역으로 쉬워짐). 조준점 = `플레이어 위치 + 예측 회피 방향 × 1.5u`, 텔레그래프 중 계속 갱신 (읽고 속일 시간을 줌). 학습과 같은 축(`Vec2.Left`) 강제.
- **보스 BossLLM** (P1 구현됨): Chase(유지거리 7)→Telegraph(1s)→Cooldown(2s), 패턴 3종 순환 —
  ① 문과 `PiercingShot`: 관통탄 3발 부채꼴 12° / ② 이과 `AimedLaser`: 히트스캔 레이저 (사거리 30, 폭 0.4, 조준선 추적 후 확정) / ③ 예체능 `RotatingWave`: 12발 원형 탄막, 발사마다 +15° 회전. 색은 전부 주황 (마젠타는 P2 전용 예약).
  HP 75%/50%/25%에 미니언 3기 소환. **HP 60%에 P2 전환 트리거 — 현재는 3초 무적 + 로그뿐, P2 행동은 미구현** (아래 §14).

## 9. UI

- **GameScreens**: 상태당 패널 1개 라우팅 + Combat 전용 HUD 토글. 버튼 배선은 코드로 (UnityEvent 금지). UI는 GameManager 공개 API만 호출.
- **HUD**: `WaveLabel`(WAVE n/7) / `HpBar`(비율 스케일 + 전공 아이콘·전공색) / `AiMiniPanel` / `PredictionFailedOverlay`.
- **AiMiniPanel** (우상단): **엘리트 생존 시에만 표시.** 표본 미달 `AI MODEL: LEARNING...` → 이후 `AI READS: LEFT 72% [HIGH]`. HIGH일 때 마젠타 배경 + LOW→HIGH 전환 펄스 ("지금부터 예측탄 온다" 신호).
- **PredictionFailedOverlay**: 플래시 0.18s + `PREDICTION FAILED` + `LEFT 82% → 64%`(학습 반영 전→후 확률) + `MODEL UPDATING...`, 총 0.9s unscaled. 보상 없음 — 연출+통계만 (MVP 확정).
- **WaveInterval 패널**: 업그레이드 3택 카드 (`UpgradePanel`) + `COUNTER PROTOCOL` 라벨 (예측탄 활성화 / DDA 치환 계획 공개). 후보 0개일 때만 [다음 웨이브] 버튼 노출.
- **ResultProfile** ("AI가 학습한 나", §13): 별명 (거리밴드×회피편향×무빙샷 3축 12종 테이블) + 요약 1줄 + 통계 6종 (평균 거리 / 무빙샷 / 선호 구역 / 학습 표본 / AI 예측 적중률 / 역카운터 성공률) + 8방향 텍스트 히스토그램 + 코멘트.
- **언어**: 인게임 텍스트 현재 **전부 영문** (TMP 기본 폰트에 한글 글리프 없던 시절의 임시 조치). 한국어 원문은 SO에 준비 완료 (`UpgradeSO`, `NicknameTableSO`) — 한글 폰트는 D3에 반입됐으므로 **UI를 한국어 원문으로 교체하는 작업이 대기 중**.

## 10. 업그레이드 8종 (§8)

추첨: 인터벌마다 3택 1, 중복 없음, 조건 미달(웨이브·스택 상한·풀 제외) 제외.

| 한국어 (영문) | 효과 | 값 | 상한 | 웨이브 |
|---|---|---|---|---|
| 논문 1저자 (FIRST AUTHOR) | 공격력 | +20% | 3 | 1 |
| 벼락치기 (ALL-NIGHTER) | 연사 속도 | +15% | 3 | 1 |
| 수강신청 올클 (PERFECT SCHEDULE) | 이동 속도 | +10% | 3 | 1 |
| 국가장학금 (SCHOLARSHIP) | 최대 HP +25 & 즉시 25 회복 | +25 | 3 | 1 |
| 스펙 부풀리기 (RESUME PADDING) | 투사체 크기 | +25% | 2 | 1 |
| 전공 심화 (MAJOR MASTERY) | **미구현** (D6 최종 무기와 함께) | — | 2 | 풀 제외 |
| 행동교정 (BEHAVIOR CORRECTION) | AI 관측 카운트 ×0.2 (사실상 리셋) | 0.2 | 무제한 | 3 |
| 논문조작 (DATA FABRICATION) | 우세 반대 방향에 가짜 표본 8개 | 8 | 무제한 | 3 |

→ **실질 추첨 풀은 7종** (웨이브 1~2는 5종). AI 조작 2종은 UI가 아니라 `AIBrainRunner` 전용 API 경유 (규칙 7 준수 확인됨).

## 11. 데이터 (SO 28개 — 밸런스 수치의 유일한 위치)

- 업그레이드 8 (`Upgrade_*`) / 웨이브 8 (`WaveConfig_1~7`, `WaveSystemConfig_Default`) / 적 스탯 5 (`EnemyStats_*` — 챗봇·그림봇·코딩봇·엘리트·보스) / AI·전투 4 (`PredictorConfig`, `PredictiveShotConfig`, `DdaConfig`, `BossConfig`) / `NicknameTable_Default` / `PlayerStats_Default`.
- 하드코딩 예외는 전부 "연출 타이밍" (`[SerializeField]` + 주석 명시): 히트스톱 0.12, 보스 인트로 2s, 깜빡임·플래시·펄스 계열, FSM 텔레그래프 연출 리터럴 일부.
- `BossConfig_Default`·`NicknameTable_Default`(12종 중 11종)는 **초안 — 기획(이양빈) 검토 대기**.

## 12. 아트 & 에셋 현황

- **폰트**: 전기칩 한글 SDF 반입 완료 (OFL, D3). `Editor/FontSetupTools.cs`가 소유 — 다른 Setup 빌더 재실행 후에는 이 빌더를 마지막에 재실행할 것.
- **픽셀 아트**: Franuka 5개 팩 76파일 반입·슬라이스 완료 (D3), 4방향 스프라이트는 `DirectionalSpriteAnimator`(자체 구현, Unity Animator 미사용)로 재생. 적 5종은 속도 자동 구동, 플레이어는 조준 방향 구동.
- **아트 후보 라이브러리 `Sprites.v2`** (4,893장, **gitignore — 커밋 금지**, 라이선스 재배포 불가): D4에 **3x → 1x 원본으로 전량 교체 완료** (PPU 18/32/16, 슬라이스 ÷3). 열람 후 고른 것만 `Sprites/`·`Prefabs/`로 승격하는 흐름. `Prefabs.v2`는 아직 미생성.
- 현재 3전공 모두 Sorcerer 시트 사용 중 (Gladiator·Swashbuckler는 반입만 됨).
- **오디오: 전무** (SFX·BGM 미착수, D6 몫).

## 13. 에디터 도구 (`Scripts/Editor/` — 빌드 미포함)

- **셋업 빌더** (씬·프리팹·SO 결정론 재현): Scene / GameFlow / Hud / Upgrade / Wave / Dda / Result / Boss / Elite / CoderBot / PainterBot / Font / SpriteBinding / V2Library(+Rescale)
- **스모크·디버그**: `AIBrainSelfTest`(순수 C# 51건), `GameStateSmokeTest`, `AIBrainDebugTools`, `GameFeelDebugTools`, `Luddite/Dev/보스 웨이브로 점프`
- 주의: 테스트 배치 도구 3종(엘리트·코딩봇·그림봇)은 웨이브 스폰으로 대체됐으므로 제거 예정 TODO 상태.

## 14. 미구현 · 알려진 이슈

### 미구현 (기능 컷 아님 — 예정 순서대로)
| 항목 | 근거 위치 | 예정 |
|---|---|---|
| **보스 P2 "PATTERN: YOU"** — 거리 복제 / 무기 복제(마젠타) / 구역 장판 + 예측탄, "USER MODEL LOADED" 연출. 현재 HP 60%에서 3초 무적 + 로그뿐, 이후 P1 순환 지속 | `BossLLM.cs:11,149` | D5 몫 (이양빈 영역) |
| 보스의 예측탄 (EliteModifier 재사용) + 보스 생존 시 AI 패널 표시 | `EliteModifier.cs:23`, `AiMiniPanel.cs:16` | P2와 함께 |
| **전공별 최종 무기** (IWeapon 교체: 문과 관통 / 이과 크리 / 예체능 범위) + 전공 심화 업그레이드 효과 | `UpgradeManager.cs:101` | D6 |
| UI 한국어 원문 교체 (데이터는 SO에 준비됨, 폰트 반입됨) | `UI/*.cs`의 TODO(D3 폰트) 다수 | 아트 영역 |
| 오디오 전체 / 투사체 오브젝트 풀 / 회피 히트맵 시각화 | `PredictionFailedOverlay.cs:16`, `Projectile.cs:175`, `ResultProfile.cs:72` | D6~폴리싱 |

### 알려진 이슈 (동작하지만 주의)
- **`ReturnToTitle` 시 남은 적 미정리** — 디스폰이 `RunStarted`에만 걸려 있어 Title 화면 뒤에 적이 잔존할 수 있음 (`GameManager.cs:171` TODO 미이행).
- **`UpgradeSO._value` 죽은 값**: 행동교정·논문조작의 실제 수치는 `PredictorConfig_Default`가 소스. UpgradeSO 쪽 값은 읽히지 않아 한쪽만 바꾸면 조용히 어긋난다.
- **낡은 주석 3건**: `GameManager.cs:128,142`(이미 이행됨), `EnemyBase.cs:185`(폴링으로 대체됨), `Projectile.cs:71`("예측탄 항상 false" — 실제로는 EliteModifier가 마킹).
- MED 신뢰도 단계 없음 — 내부는 HIGH/not-HIGH 불리언, UI 3표기(LEARNING/LOW/HIGH)는 HUD 소관. GDD와 대조 시 참고.
- `_threatMissRadius`(2u)는 GDD 미명시 값 — 코드에 "사람 확인 필요" 표시.
- 보스 P1은 실플레이 검증이 생략된 채 커밋됨 (`d2aeac9`, 사유는 AI_USAGE_LOG D3 세션 3) — **재생 검증 대기 중**.

---

## 15. D5~D6 변경분 (2026-08-08 ~ 08-09) — §1~13보다 우선

### 15.1 던전 체인 (개정안 v1.1 / MAP_SPEC)

**🔴 안전판이 먼저다.** `SO/DungeonConfig_Default.asset`의 `_enabled`를 끄면 `DungeonManager`가
`Awake`에서 자기 자신을 비활성화하고 조기 반환한다 → `WaveManager`는 D4 경로(Combat 진입 = 웨이브 시작,
전멸 = 인터벌)를 그대로 탄다. 던전은 **y = −200**에 따로 지어져 있고 **기존 `Arena`(원점)는 무손상**이다.
⚠️ **토글 OFF 실주행은 아직 검증되지 않았다** (§15.6).

```
시작방 ─복도─ 전투방1 ─복도─ 전투방2 ⤴복도⤴ 전투방3 ─복도─ 전투방4 ⤵복도⤵ 전투방5 ─복도─ 전투방6 ─복도─ 보스방
   (방 8개 = 시작 1 + 전투 6 + 보스 1 / 복도 7개 / 꺾임 4회, 분기 없음)
```

| 컴포넌트 | 위치 | 역할 |
|---|---|---|
| `DungeonConfigSO` | `Scripts/Data/` | 토글 · 방 규격 · 체인 길이 · 상자 정책 |
| `Room` | `Scripts/Core/` | 구성 요소 + 진입/이탈 신호만. 판단하지 않음 |
| `Door` | `Scripts/Core/` | 잠김 = BoxCollider2D + `ProjectileBlocker` 활성. `Lock()`/`Unlock()`/`SetLocked(bool)` |
| `Chest` | `Scripts/Core/` | `GameManager.BeginWaveInterval()`만 호출 — 3택 카드·TARGET PROFILE·COUNTER PROTOCOL은 D4 패널 그대로 재사용 |
| `DungeonManager` | `Scripts/Core/` | 체인 진행 단독 소유. `RunStarted`에서 문 잠금 복구·상자 회수·시작방 복귀 |

**루프**: 방 진입(트리거) → 입구·출구 잠김 → **0.5s 후** 스폰(`_lockInDelay`, 문 닫히는 연출 시간) →
전멸 → `WaveManager.RoomCleared` → 출구 개방 + 상자 등장 → 상자 오픈 → 인터벌 패널 → 다음 방.
**감쇠·프로파일 스냅숏·DDA 판정은 여전히 `WaveEnded` 시점** — AIBrain 무변경.

**`WaveManager` 변경 (전부 가산 — 기본값이 D4 동작과 동일)**
- `SetSpawnOrigin(Vector2)` — 스폰 링 평행이동. 원점이면 기존 좌표와 완전 동일
- `SetExternalWaveControl(bool)` — Combat 진입 자동 시작 억제. **단 `previous == BossIntro`는 예외**
  (막으면 인트로가 시작을 가로챈 뒤 보스가 영영 스폰되지 않는다)
- `event Action<int> RoomCleared` — 구독자가 있으면 인터벌을 직접 열지 않는다

**방 규격 32×18** (🔴 계약 #2 개정, 사람 지시). 스폰 링 반폭 **16×9**(인셋 1 → 링 15×8).
24×14이던 이유와 바꾼 이유는 `MAP_SPEC.md` §2 참조 — 화면(26.67×15)보다 커야 추적 카메라가 작동한다.

### 15.2 카메라 (`Scripts/Core/CameraFollow.cs`)

**고정 카메라 계약은 폐기됐다.** 플레이어를 추적한다.
- **축별 조건부 클램프** — 방(+여유)이 화면보다 큰 축만 추적. 작으면 방 중심 고정
  (반대로 하면 `Mathf.Clamp(min > max)`로 좌표가 역전된다)
- **`_edgePeek` 6u 필수** — 여유가 없으면 벽에 붙은 플레이어가 화면 가장자리에 못 박혀 **문·복도가 화면 밖**으로 나간다.
  현재 이동 범위 X ±8.67 / Y ±7.50
- **복도 구간**: `Room.PlayerExited` → `DungeonManager`가 바운드를 "현재 방 ~ 다음 방"으로 확장.
  `PlayerExited`는 **카메라 전용 신호**이고 `_entered`를 되돌리지 않는다(되돌리면 재진입으로 웨이브 재시작)
- 화면 밖 위협 보정(가장자리 화살표)은 **미구현** — 추적 카메라의 동반 계약이다

### 15.3 아트 / 렌더

- **회색조 규칙 폐기** (사람 결정). `Sprites/` 53파일을 v2 컬러 원본으로 복원(`.png`만 덮어써 GUID·슬라이스 보존).
  🔴 §10.4는 유지 — 조문이 "그 외 위협은 무채색~**주황**"이라 컬러화가 위반이 아니다.
  **예약 색역**: 마젠타·핫핑크·고채도 보라(AI 전용) / 파랑·초록·노랑(전공색 전용)
- **던전 타일셋** `Sprites/Dungeon/` — `DungeonTileset`(448×96 → 16px 격자 168장) · 문 · 상자 · 횃불,
  **PPU 16**(16px = 1u), Point, Uncompressed, **FullRect**(Tiled 렌더 필수). 장식은 `Dungeon/Decor/` 13종
- **Sorting Layer 9종** (`ProjectSettings/TagManager.asset`):
  `Ground < Decor < Shadow < Units < Walls < WallTops < Projectiles < VFX < UI`
  - ⚠️ **`Default`는 `Ground`보다 아래다.** 던전 빌더를 돌린 뒤 **반드시 `Luddite/Setup/Sorting Layer 배정`을 함께 실행**
    (`FontSetupTools`를 마지막에 재실행하는 것과 같은 관계). 안 하면 액터가 바닥 밑으로 깔린다
  - ⚠️ 레이어를 새로 추가하면 **Global Light 2D의 Target Sorting Layers도 갱신**해야 한다. 안 하면 그 레이어가 빛을 못 받아 검게 나온다
- 카메라 Background `#0A0A0F`, Global Light 2D intensity **0.88**, **맵 바깥은 무렌더 어둠**(암반 배경 깔지 않음 — MAP_SPEC §5-1)
- **물리 보간** — 플레이어·적·탄 전부 `RigidbodyInterpolation2D.Interpolate`. 없으면 물리 50Hz vs 렌더 수백 fps에서 계단식 진동으로 보인다. 렌더 전용이라 🔴 AIBrain 판정에 영향 없음

### 15.4 에디터 도구 (§13에 추가)

| 도구 | 메뉴 | 비고 |
|---|---|---|
| `DungeonSetupTools` | `Luddite/Setup/던전 체인 생성 (멱등)` | `Dungeon` 루트를 통째로 재생성. **그 아래는 손편집 금지** |
| `SortingLayerSetupTools` | `Luddite/Setup/Sorting Layer 배정 (멱등)` | **던전 빌더 직후 반드시 실행** |
| `DungeonSmokeTest` | `Luddite/Dev/던전 루프 스모크` | 51건. 통행 물리 프로브 포함 |

### 15.5 D5~D6에 고친 결함 (전부 사람이 실플레이로 발견)

| 증상 | 원인 |
|---|---|
| 복도를 통과 못 함 | `ResetRoom()`이 입구 문까지 잠갔다. 락인은 *들어온 뒤*에 걸려야 한다 |
| 복도에서 카메라가 멈춤 | 바운드가 현재 "방"에 고정 → 복도가 바운드 밖 |
| 문이 어디 있는지 안 보임 | 클램프 여유 0 → 플레이어가 화면 가장자리에 고정 |
| 캐릭터가 진동 | `Rigidbody2D.interpolation = None` |
| (빌더) 웨이브 7 미시작 | 보스방이 전투방 조건에서 빠짐 |

### 15.6 🔴 다음 세션이 먼저 볼 것 (미검증·미구현)
> ⚠️ D7 세션 1이 이 중 **1(부분)·3·5를 처리**했다 — §16 참조. 2(토글 OFF 실주행)는 여전히 사람 검증 대기.

1. **엘리트·보스 P2 마젠타화 경로** — 회색조 시절엔 무채색 위 곱셈 틴트만으로 마젠타가 나왔다.
   **컬러 스프라이트 위에서는 곱셈이 안 먹는다.** 🔴 §10.4 계약 사안이고 **컬러 전환 이후 한 번도 검증되지 않았다.**
   제출 문서(#3·#4)가 마젠타 예측탄을 게임의 얼굴로 쓰고 있어 실물과 어긋나면 곤란하다
2. **토글 OFF 폴백 실주행** — `Awake`가 플레이 모드에서만 도므로 미검증. **안전판의 유일한 구멍**
3. **보스 P2 `PATTERN: YOU`** — 여전히 HP 60%에서 3초 무적 + 로그뿐 (§14)
4. **보스 P1 실플레이 검증** — D3부터 이월 중
5. 화면 밖 위협 보정(가장자리 화살표) — 추적 카메라 동반 계약인데 미구현
6. Dungeon Tileset 팩 URL·라이선스 — `CREDITS.md`에 ⚠️ 표시. **실격 리스크 직결**
7. 적 3종(Beholder·Djinn·Wizard)이 파랑·시안 계열 — 예약 색역(전공색 파랑)과 겹친다
8. UI 한국어 교체 / 오디오 전무 / 영상 촬영 — D6~D7 몫

---

## 16. D7 변경분 (2026-08-10) — 보스 P2 「PATTERN: YOU」

### 16.1 보스 P2 (§9 — 구현됨, 실플레이 검증 대기)
- **전환**: HP 60% → 무적 3초 + `GameEvents.BossPhaseTwoStarted` → HUD `BossPhaseOverlay`가
  `USER MODEL LOADED / COPY COMPLETE / PATTERN: YOU` 3줄을 1초씩 표기 (unscaled, PredictionFailed 패턴 복제)
- **P2 3요소** (`BossLLM`, 무적 종료 시 개시):
  ① **거리 복제** — `AIBrainRunner.AverageEngageDistance`를 유지 목표로 접근/후퇴 (밴드 ±0.75, 클램프 2.5~12, 이동 3.5)
  ② **무기 복제** — 플레이어 전공의 P1 패턴 고정(순환 정지): 문과=관통탄 / 이과=레이저 / 예체능=원형 탄막, **전부 마젠타**
  ③ **구역 장판** — `favoriteQuadrant` 사분면 중심에 주기 6s(초안)로 `BossZoneHazard` 런타임 생성:
  텔레그래프 2s → 지속 3s → 8/s (§9 확정값). 마젠타 (흰 원 틴트 — 곱셈 발색 문제 회피). 비투사체라 학습 무오염
- **예측탄** — `EliteModifier`를 보스 프리팹에 부착 (§9 "엘리트 시스템 그대로"). P2 텔레그래프 진입 시
  `TryBeginPredictiveAim` — HIGH 게이트·공격 2회당 1회·**온스크린 게이트(계약 #4, 이번에 신규 구현)** 전부 공유.
  부착만으로 **보스 생존 시 AI 미니 패널 표시**(§14 잔여)도 충족 (패널 조건 = `EliteModifier.ActiveCount`)
- **마젠타화 절충** (§15.6-1): 본체는 마젠타 편향 틴트 + **마젠타 오라 링**(흰 원) — 컬러 스프라이트 곱셈 한계를 실루엣·오라·공격색 분산으로 우회
- **P2 수치는 전부 `BossConfig_Default` SO** (신규 12필드 — 기존 애셋은 스크립트 기본값 자동 적용). 초안 표시: 장판 주기 6s·반경 2.2 — 기획 검토 대상

### 16.2 이번에 고친 잠재 결함 2건
| 결함 | 원인 → 수정 |
|---|---|
| 프로파일러 4분할이 던전에서 전부 남쪽 | `QuadrantIndex`가 월드 원점 기준인데 던전은 y=−200 → `AIBrainRunner.SetProfileOrigin`(방 진입마다 방 중심 주입, `DungeonManager`). 교전 거리는 상대량이라 불변, AIBrain 순수 로직 무변경 |
| 🔴 **토글 OFF 폴백 소프트락** | D5에 스폰 링 SO가 16×9로 확대 → 폴백 아레나(벽 반폭 12×7)에서 적이 **벽 밖 스폰 → 전멸 불가**. 링을 방의 속성으로 재설계: `WaveManager.SetSpawnArea(중심, 방 반폭)` 가산 + SO를 D4 값 12×7 복원. 던전 링은 15×8로 동일 |

### 16.3 세팅·검증 절차 (사람 실행 필요 — 이 세션은 MCP 미연결)
1. `Luddite/Setup/보스 P2 컴포넌트 보장 (§9 P2)` — 보스 프리팹에 Gun·EliteModifier·텔레그래프 3종 가산 + 씬에 BossPhaseOverlay
2. `Luddite/Setup/Sorting Layer 배정 (멱등)` → `Luddite/Setup/한글 폰트 세팅` (**항상 마지막**)
3. `Luddite/Dev/보스 P2 스모크` — 배선·§9 수치·이벤트 경로 자동 검사
4. 플레이: `보스 웨이브로 점프` → `보스 HP를 P2 직전(61%)으로` → 전환 연출·마젠타·거리 복제·장판·예측탄 확인
5. **토글 OFF(`DungeonConfig_Default._enabled=false`) 웨이브 1 완주** — §15.6-2 겸 이번 소프트락 정정 실검증

### 16.4 오디오 (§12 — D7 세션 2, 전무 → 충족)
- `Audio/` WAV 10개 = §12 최소 세트 8종 + BossPhase + BGM 루프(19.2s, Am-Am-F-G).
  **자체 절차 합성** (`Audio/Generator~/generate_sfx.py` — 고정 시드 결정론, Unity 무시 폴더에 보존)
- `Core/AudioDirector.cs` — 오디오 단독 소유자. GameEvents 구독(PredictionFailed 글리치 / WaveClear /
  BossPhase / 인터벌 진입 AiAnalyze / BGM 시작·정지) + 소유 컴포넌트 훅 5줄(발사·피격·격파·조준음·버튼).
  **씬에 없으면 전부 무음 no-op.** 볼륨·연사음 간격은 인스펙터 노출
- BGM: Combat 진입 시 시작, 런 내내 유지(인터벌·일시정지 포함), Title/Result 정지
- 배선: `Luddite/Setup/오디오 배선 (§12)` (멱등 — 누락 클립은 LogError로 드러남)
- 품질 교체 경로: WAV 파일만 갈아끼우면 됨 (AudioDirector는 출처 무관)
