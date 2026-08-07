# SYSTEMS.md — 구현 현황 문서
> **코드가 진실이고 이 문서는 스냅숏이다.** 기준 시점: **2026-08-07 (D4)**, 브랜치 `JungJoon`.
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
