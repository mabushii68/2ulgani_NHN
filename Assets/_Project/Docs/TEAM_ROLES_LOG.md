# 팀원 롤 & 실작업 로그 — 러다이트 2026
> 제출물 #5 「팀원 롤 기술서」의 **원천 데이터 문서**. 제출물이 요구하는 것은 담당 '계획'이 아니라 **"각 팀원이 실제로 맡아 구현한 영역"** — 그래서 세션마다 기록한다.
> 갱신 주체: Claude Code (세션 종료 절차의 일부, `AI_USAGE_LOG.md` append와 동시 수행)
> 경로: `Assets/_Project/Docs/TEAM_ROLES_LOG.md`

## 0. 기록 규칙
1. 세션 종료 시 §2 표에 **1행 append** — 세션 요청자, 작업 영역, 주요 변경 파일
2. **상대 담당 영역 파일을 수정한 세션**은 `교차` 열에 ⭕ + 비고에 사유 (CLAUDE.md 팀 규칙)
3. 페어 작업(특히 D4 `PREDICTION FAILED` 연출)은 §3에 별도 기록 — "협업·분업 방식" 서술의 핵심 재료
4. 기존 행 수정·삭제 금지

## 1. 담당 구조 (계획 기준 — CLAUDE.md와 동기화)
| | 이양빈 (기획/프로그래밍) | 김정준 (아트/프로그래밍) |
|---|---|---|
| 영역 | 게임의 '뇌': AIBrain 전체, 전투 로직, 적 FSM, 보스, 밸런스 결정, GDD 관리 | 게임의 '얼굴': 아트 전반, 예측탄 시각 언어, HUD/UI, WebGL 파이프라인, CREDITS 관리 |
| 접점 | `PREDICTION FAILED` 연출 (로직 × 비주얼) — D4 페어 작업 | |

## 2. 세션별 실작업 기록
| 일차 | 세션 | 요청자 | 작업 영역 (기능) | 주요 변경 파일 | 교차 | 비고 |
|---|---|---|---|---|---|---|
| D1 | 1 | 공동 | 프로젝트 초기 세팅 — GDD v1.0 확정, 제출용 문서 4종 신규 작성, 세션 규칙 정비 | `GDD.md`, `AI_USAGE_LOG.md`, `CREDITS.md`, `SUBMISSION.md`, `TEAM_ROLES_LOG.md`, `CLAUDE.md`, `README.md` | — | 코드 미착수. 문서 체계 확립 세션 |
| D1 | 2 | 김정준 | 최소 전투 — 플레이어 이동·조준·홀드 연사, 투사체, `Main.unity` 아레나·고정 카메라 골격, 플레이스홀더 스프라이트 | `Scripts/Player/PlayerController.cs`, `Scripts/Combat/{IWeapon,IDamageable,Projectile,ProjectileBlocker,BasicWeapon,TargetDummy}.cs`, `Scripts/Data/PlayerStatsSO.cs`, `SO/PlayerStats_Default.asset`, `Prefabs/Projectile.prefab`, `Scenes/Main.unity`, `Sprites/Placeholder_*.png` | ⭕ | 씬 골격·아트 플레이스홀더는 담당 영역이지만 **전투 로직(투사체·데미지·넉백)은 이양빈 담당 영역**. D1 일정의 "최소 전투"를 굴리려면 분리 불가여서 함께 구현 — `IWeapon` 인터페이스로 경계를 만들어 이후 전투 로직 확장이 UI/아트와 충돌하지 않게 함 |
| D1 | 3 | 김정준 | 환경 정합성 정리 — 빌드 씬 교체(Main.unity 단독), 세션 규칙 문서를 실제 리포 기준으로 현행화(Unity 버전·입력·패키지·폴더 스펙·브랜치 전략) | `CLAUDE.md`, `CREDITS.md`, `TEAM_ROLES_LOG.md`, `ProjectSettings/EditorBuildSettings.asset` | — | 코드 변경 없음. 불일치 5건을 사람이 판단·확정 |
| D1 | 4 | 김정준 | WebGL 파이프라인 — 퍼블리싱 설정 확정(Gzip+Fallback, 1280×720), 첫 빌드 검증(17MB), `gh-pages` orphan 배포 + 일정 7일 재압축 | `ProjectSettings/ProjectSettings.asset`, `Assets/Settings/UniversalRP.asset`, `CLAUDE.md`, `GDD.md` §14·§15, `AI_USAGE_LOG.md` | ⭕ | WebGL 파이프라인은 담당 영역 정면. **`GDD.md` 수정은 이양빈 담당 영역** — 기능 컷 없이 일차 번호 재배치와 영향 주석만 추가(§15 게이트 통합, §14 스트레치 미착수 확정). 스코프 결정은 하지 않음 |

| D1 | 5 | 김정준 | **(D2 작업 선행)** 챗봇 드론 교전 — 적 FSM(Approach→Aim→Fire→Cooldown), 적 탄환, 스폰 텔레그래프, 플레이어 체력·무적, 진영 판정 | `Scripts/Enemies/{EnemyBase,ChatbotDrone,EnemyGun}.cs`, `Scripts/Player/PlayerHealth.cs`, `Scripts/Combat/{Faction,IDamageable,Projectile}.cs`, `Scripts/Data/EnemyStatsSO.cs`, `SO/EnemyStats_ChatbotDrone.asset`, `Prefabs/{ChatbotDrone,EnemyProjectile}.prefab`, `Scenes/Main.unity` | ⭕ | **전 범위가 이양빈 담당 영역(적 FSM·전투 로직)** — 사람이 "담당 상관없이" 진행 지시. `TargetDummy` 삭제(실제 적으로 대체). `IDamageable.IsAlive`→`CanBeDamaged` 개명은 세션 2에 직접 작성한 인터페이스라 파급 3파일로 국한 |

| D1 | 6 | 김정준 | **(D2 작업 선행)** AIBrain 프로토 — 회피 예측기(온라인 조건부 확률 모델), 피격 위기 이벤트 탐지기, 자체 검증 51건 | `Scripts/AIBrain/*` (8종 신규), `Scripts/Data/PredictorConfigSO.cs`, `SO/PredictorConfig_Default.asset`, `Scripts/Editor/AIBrainSelfTest.cs`, `CLAUDE.md` | ⭕ | **AIBrain은 이양빈 담당 영역이고 🔴 계약 3건이 걸린 최고 민감 구역** — 사람이 "담당 상관없이" 진행 지시. 계약을 코드로 강제(가상 카운트 const화, 계약값 이탈 시 OnValidate 경고). GDD 미명시 1건·GDD 문자해석 1건은 사람 확인 대상으로 분리 보고. `Scripts/Editor/`를 폴더 스펙에 정식 추가 |

| D1 | 7 | 김정준 | **(D2 완료)** AIBrainRunner 어댑터 — AIBrain↔Unity 연결, `GameEvents` 이벤트 버스 개설, 투사체 레지스트리, AIBrain 디버그·씬 배선 도구 | `Scripts/Core/{GameEvents,AIBrainRunner}.cs`, `Scripts/Combat/Projectile.cs`, `Scripts/Editor/{AIBrainDebugTools,SceneSetupTools}.cs`, `Scenes/Main.unity` | ⭕ | AIBrain·전투 로직 = 이양빈 담당 영역. 규칙 4의 단일 정적 이벤트 버스를 이 세션에서 처음 개설 — 이후 시스템 간 결합은 여기를 경유한다. §7.1 계약 경계 사례 1건(무적 중 관통) 사람 결정 대기 |

| D2 | 1 | 김정준 | 🔴 §7.1 계약 해석 확정 — 무적 관통 = 피격 (표본 오염 차단). D1 세션 7 대기 건을 사람 결정으로 해소 | `Scripts/Combat/{Projectile,IDamageable}.cs`, `GDD.md` §7.1 | ⭕ | **전투 로직·GDD = 이양빈 담당 영역.** 계약 해석은 선택지 제시 후 사람이 결정("닿으면 피격"), Claude Code가 반영 |

| D2 | 2 | 김정준 | GameState 골격 — 7상태 머신, 화면 라우팅(패널 6종), 런 초기화 이벤트, 플레이어 입력 게이트, 전환 스모크 25건 | `Scripts/Core/{GameManager,GameState,Major,GameEvents,AIBrainRunner}.cs`, `Scripts/UI/GameScreens.cs`, `Scripts/Player/{PlayerController,PlayerHealth}.cs`, `Scripts/Editor/{GameFlowSetupTools,GameStateSmokeTest}.cs`, `Scenes/Main.unity`, `Assets/TextMesh Pro/`(TMP 리소스 반입), `CREDITS.md` | ⭕ | UI·화면은 담당 영역 정면. **PlayerHealth·AIBrainRunner 연결(전투·AIBrain) = 이양빈 담당 영역** — 런 초기화 이벤트 구독 추가만. 한글 폰트 미보유로 UI 영문 표기 (D3 폰트 반입 예정) |

| D2 | 3 | 김정준 | **(D3 작업 선행)** 예측탄 + 엘리트 — EliteModifier(판단·조준·텔레그래프), 예측탄 SO, 챗봇 변형 프리팹, 마젠타 시각 요소(조준선·마커·트레일) | `Scripts/Enemies/{EliteModifier,ChatbotDrone,EnemyGun,EnemyBase}.cs`, `Scripts/Data/PredictiveShotConfigSO.cs`, `Scripts/Editor/EliteSetupTools.cs`, `Prefabs/EliteDrone.prefab`, `SO/{EnemyStats_Elite,PredictiveShotConfig_Default}.asset`, `Scenes/Main.unity` | ⭕ | **적 FSM·전투 로직 = 이양빈 담당 영역**, 예측탄 시각 언어(마젠타 텔레그래프)는 김정준 담당 정면. 점선 조준선은 실선 플레이스홀더(아트 패스 TODO) |

| D2 | 4 | 김정준 | **(D3 작업 선행)** HUD — AI 미니 패널(엘리트 생존 시만·LEARNING 표기·HIGH 펄스) + HP 바 + 전공색 아이콘 | `Scripts/UI/{AiMiniPanel,HpBar,GameScreens}.cs`, `Scripts/Editor/{HudSetupTools,GameStateSmokeTest}.cs`, `Scripts/Core/AIBrainRunner.cs`, `Scripts/Data/PredictorConfigSO.cs`, `Scripts/Enemies/EliteModifier.cs`, `Scenes/Main.unity` | — | HUD/UI = 김정준 담당 정면. AIBrain 쪽은 읽기 전용 게터 추가만 |

| D2 | 5 | 김정준 | **(D3 작업 선행)** PREDICTION FAILED 연출 + 히트스톱 — 이벤트 버스 접점, 신뢰도 하락 보고, 오버레이, GameManager 히트스톱 | `Scripts/Core/{PredictionFailedReport,GameEvents,AIBrainRunner,GameManager}.cs`, `Scripts/UI/PredictionFailedOverlay.cs`, `Scripts/Editor/{HudSetupTools,GameFeelDebugTools}.cs`, `Scenes/Main.unity` | ⭕ | **원래 D4 페어 작업으로 계획된 로직×비주얼 접점** — 사람(김정준) 지시로 단독 진행. 판정 로직(AIBrainRunner·이양빈 영역)과 연출(김정준 영역)을 GameEvents로 분리해 이후 각자 수정 가능하게 함 |

## 3. 협업·페어 작업 기록
| 일차 | 작업 | 참여 | 분업 방식 (누가 무엇을) | 결과 |
|---|---|---|---|---|
| <!-- D4 예정: PREDICTION FAILED 연출 --> | | | | |

## 4. 협업 방식 메모 (수시 기록)
> 제출물 #5의 "협업·분업 방식" 절 재료. 예: 브랜치 전략(main 단일), 세션 단위 분업, GDD를 통한 의사결정, 담당 영역 파일 소유권 규칙 등. 실제로 운영한 방식만 기록.

- (예시) main 단일 브랜치 + 세션 단위 커밋(`[D일차][타입]`)으로 충돌 최소화
- **D1 확정: main 단일 브랜치 → 담당자별 작업 브랜치 + main 병합**. 김정준 작업분은 `JungJoon` 브랜치에서 진행한다. 초기 계획(main 단일)은 2인 팀의 충돌 최소화를 노린 것이었으나, 담당 영역이 '뇌'(AIBrain·전투)와 '얼굴'(아트·UI)로 뚜렷이 갈려 브랜치 분리가 실제로는 충돌을 더 줄인다고 판단. `CLAUDE.md`도 같은 커밋에서 갱신
-

## 5. D10 최종 문서 변환 체크리스트
- [ ] §1 + §2 집계 → 팀원별 "실제 구현 영역" 절 (계획 대비 실제 차이가 있으면 실제 기준으로)
- [ ] §3 + §4 → "협업·분업 방식" 절
- [ ] 교차 작업(⭕ 행)이 담당 서술과 모순되지 않는지 검토
- [ ] PDF 변환
