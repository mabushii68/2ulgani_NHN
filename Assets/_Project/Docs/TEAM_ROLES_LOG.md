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

| D2 | 6 | 김정준 | **(D4 작업 선행)** 그림봇 — 거리 유지·횡이동·부채꼴 3발 FSM, 전용 수치 SO 필드 | `Scripts/Enemies/PainterBot.cs`, `Scripts/Data/EnemyStatsSO.cs`, `Scripts/Editor/PainterBotSetupTools.cs`, `Prefabs/PainterBot.prefab`, `SO/EnemyStats_PainterBot.asset`, `Scenes/Main.unity` | ⭕ | **적 FSM = 이양빈 담당 영역** — 기존 지시("담당 상관없이") 연장선에서 진행 |

| D2 | 7 | 김정준 | **(D4 작업 선행)** 코딩봇 — 돌진형 FSM(방향 고정 텔레그래프), 상태별 접촉 데미지, 삼각 플레이스홀더 절차 생성 | `Scripts/Enemies/{CoderBot,EnemyBase}.cs`, `Scripts/Data/EnemyStatsSO.cs`, `Scripts/Editor/CoderBotSetupTools.cs`, `Prefabs/CoderBot.prefab`, `SO/EnemyStats_CoderBot.asset`, `Sprites/Placeholder_Triangle.png`, `Scenes/Main.unity`, `CREDITS.md` | ⭕ | **적 FSM = 이양빈 담당 영역** — 기존 지시 연장선. 적 3종 + 엘리트 완성 |

| D2 | 8 | 김정준 | **(D4 작업 선행)** 7웨이브 시스템 — 전멸형 종료(🔴), 순차 스폰, 웨이브 감쇠 자동화, WAVE 라벨, 테스트 적 정리 | `Scripts/Core/{WaveManager,GameEvents,AIBrainRunner}.cs`, `Scripts/Data/{WaveConfigSO,WaveSystemConfigSO}.cs`, `Scripts/UI/WaveLabel.cs`, `Scripts/Editor/WaveSetupTools.cs`, `SO/WaveConfig_1~7.asset`, `SO/WaveSystemConfig_Default.asset`, `Scenes/Main.unity` | ⭕ | **웨이브·전투 구조 = 이양빈 담당 영역** — 기존 지시 연장선. 코어 루프(타이틀→전투→인터벌→보스 스텁→결과) 최초 관통 |

| D2 | 9 | 김정준 | **(D4 작업 선행)** 업그레이드 8종 — SO·추첨 규칙·배수 적용·3택 카드 UI. AI 상호작용 2종은 전용 API 경유 | `Scripts/Data/UpgradeSO.cs`, `Scripts/Player/{PlayerUpgrades,PlayerController,PlayerHealth}.cs`, `Scripts/Core/UpgradeManager.cs`, `Scripts/UI/UpgradePanel.cs`, `Scripts/Combat/BasicWeapon.cs`, `Scripts/Editor/UpgradeSetupTools.cs`, `SO/Upgrade_*.asset` ×8, `Scenes/Main.unity` | ⭕ | 카드 UI = 김정준 담당, **밸런스 수치·전투 배수 = 이양빈 담당 영역** (수치는 GDD §8 표 그대로, 결정 없음). 전공 심화는 D6 의존으로 풀 미편입 |

| D2 | 10 | 김정준 | **(D5 작업 선행)** 플레이 스타일 프로파일러 — §6.4 수집기 (순수 C# + 자체 테스트 18건), 적 레지스트리, 어댑터 확장 | `Scripts/AIBrain/PlayStyleProfiler.cs`, `Scripts/Core/AIBrainRunner.cs`, `Scripts/Enemies/EnemyBase.cs`, `Scripts/Player/PlayerController.cs`, `Scripts/Editor/AIBrainSelfTest.cs` | ⭕ | **AIBrain = 이양빈 담당 영역** — 기존 지시 연장선 |

| D2 | 11 | 김정준 | **(D5 작업 선행)** 매크로 DDA — 판정·30% 상한 치환·COUNTER PROTOCOL 표기 + 인터벌 레이아웃 겹침 수정(사람 발견 버그) | `Scripts/Data/DdaConfigSO.cs`, `Scripts/Core/WaveManager.cs`, `Scripts/UI/CounterProtocolLabel.cs`, `Scripts/Editor/{DdaSetupTools,GameFlowSetupTools,UpgradeSetupTools}.cs`, `SO/DdaConfig_Default.asset`, `Scenes/Main.unity` | ⭕ | **DDA·웨이브 구성 = 이양빈 담당 영역** (수치는 §6.3 표 그대로). 패널 표기·레이아웃 = 김정준 담당 |

| D3 | 1 | 김정준 | 역카운터 판정(🔴 §7.5) — 탄에 예측 방향 탑재, 순수 C# 판정 + 테스트 6건, 러너 집계 | `Scripts/AIBrain/{ThreatEventTracker,ThreatSample}.cs`, `Scripts/Combat/Projectile.cs`, `Scripts/Enemies/{EnemyGun,EliteModifier}.cs`, `Scripts/Core/AIBrainRunner.cs`, `Scripts/Editor/AIBrainSelfTest.cs` | ⭕ | **AIBrain·🔴 계약 = 이양빈 담당 영역** — 기존 지시 연장선 |

| D3 | 2 | 김정준 | 결과 화면 §13 — 별명 3축 룰 테이블(SO), AI ANALYSIS 블록, 히스토그램, ResultPanel 재배치 | `Scripts/Data/NicknameTableSO.cs`, `Scripts/UI/ResultProfile.cs`, `Scripts/Editor/{ResultSetupTools,GameFlowSetupTools}.cs`, `SO/NicknameTable_Default.asset`, `Scenes/Main.unity` | ⭕ | 화면 = 김정준 담당. **별명 11종 초안 = 이양빈(기획) 검토 대상** — SO에서 문구 수정 가능 |

| D3 | 3 | 김정준 | 보스 P1(§9) — 3전공 패턴 순환·소환·P2 전환 스텁·웨이브 7 실연결, 투사체 관통 지원 | `Scripts/Enemies/{BossLLM,EnemyBase}.cs`, `Scripts/Data/BossConfigSO.cs`, `Scripts/Combat/Projectile.cs`, `Scripts/Core/WaveManager.cs`, `Scripts/Editor/{BossSetupTools,GameFeelDebugTools}.cs`, `Prefabs/BossLLM.prefab`, `SO/{EnemyStats_Boss,BossConfig_Default,WaveConfig_7}.asset` | ⭕ | **보스 = 이양빈 담당 영역.** 패턴 데미지 초안 = 기획 검토 대상. 실플레이 검증은 사람 지시로 생략(D4 이월) |

| D3 | 4 | 김정준 | 한글 폰트 반입(§10.5) — 폰트 세팅 빌더 4메뉴, `Fonts/` 신설, 씬 텍스트 33개 폰트·머티리얼 교체, CREDITS 첫 외부 에셋 기록 | `Scripts/Editor/FontSetupTools.cs`, `Fonts/*`, `Scenes/Main.unity`, `Docs/CREDITS.md`, `CLAUDE.md` | ❌ | **아트 = 김정준 담당 영역** (첫 아트 반입). SDF 애셋은 사람이 Font Asset Creator로 직접 구움 — Claude Code는 진단·배선·문서 담당. 빌드 사이즈 16MB 이슈는 D6 WebGL 검증으로 이월 |
| D3 | 5 | 김정준 | 픽셀 아트 반입 — Franuka 5개 팩에서 76파일 선별, PPU 18·Point·Uncompressed 임포트 설정, 시트 슬라이스(실측 격자), 적·UI 회색조 변환(🔴 §10.4 방어), CREDITS 라이선스 기록 | `Sprites/{Characters,Enemies,Projectiles,Arena,UI,Icons}/*`, `Docs/CREDITS.md` | ❌ | **아트 = 김정준 담당 영역.** 프리팹 배선·애니메이션은 다음 세션. GDD §11 "픽셀아트 ❌" 갱신은 **기획 담당(이양빈) 결정 필요** — 문서만 표기하고 수정하지 않음 |
| D3 | 6 | 김정준 | 픽셀 아트 프리팹 배선 — 4방향 스프라이트 애니메이터, 적 5종 + 투사체 2종 + 씬 플레이어 배선, 총구 플레이스홀더 정리, 🔴 §10.4 색 위계 성립 | `Scripts/Core/DirectionalSpriteAnimator.cs`, `Scripts/Player/PlayerSpriteView.cs`, `Scripts/Editor/SpriteBindingSetupTools.cs`, `Scripts/Enemies/EliteModifier.cs`, `Prefabs/*`(7종), `Scenes/Main.unity`, `Sprites/Enemies/Beholder_*` | ⭕ | **아트 = 김정준 담당.** 교차: `EliteModifier.cs`(이양빈 영역)에 예측탄 스프라이트 필드 1개 추가 — 로직 무변경, 시각 필드만. **적 FSM은 의도적으로 미수정** — 공격·사망 애니메이션은 FSM 훅이 필요해 이양빈 담당으로 남김 |
| D3 | 7 | 김정준 | 아레나 타일 + UI 스킨 + 조준 표식·플레이어 탄 교체 — 바닥·벽 타일링(콜라이더 보존), 9슬라이스 UI 스킨, 전공 아이콘 컬러 복원, 조준 화살표, FireballBig 탄 | `Scripts/Editor/SpriteBindingSetupTools.cs`, `Scripts/UI/HpBar.cs`, `Scenes/Main.unity`, `Prefabs/Projectile.prefab`, `SO/PlayerStats_Default.asset`, `Sprites/{Icons,Projectiles,UI}/*`, `Docs/CREDITS.md` | ⭕ | UI·아트 = 김정준 담당 정면. 교차: **`SO/PlayerStats_Default.asset`의 `_projectileDiameter` 0.25→0.44는 밸런스 수치**(이양빈 영역) — 사람이 "탄이 너무 작다"고 명시 요청, 콜라이더를 겸하는 값이라 시각만 키울 수 없어 함께 조정. 되돌리려면 SO 한 곳 |
| D3 | 8 | 김정준 | 아트 후보 라이브러리 — 팩 18종 3x 전량(4,893파일) 카테고리 분류·임포트·격자 자동 슬라이스·프리팹, `.gitignore` 격리 | `Scripts/Editor/V2LibrarySetupTools.cs`, `Sprites.v2/*`(미커밋), `Prefabs.v2/*`(미커밋), `.gitignore`, `Docs/CREDITS.md` | ❌ | **아트 = 김정준 담당 정면.** 게임 코드·씬 무변경, 빌드 영향 0. **라이선스 판단**: 팩 전량 커밋은 "as is 재배포 금지" 위반 소지 → 로컬 열람용으로 격리하고 승격 시에만 리포 반입. **프리팹 216개는 미완**(동기 실행이 메인 스레드를 잡아 중단) — 다음 세션에서 배치 분할 후 재실행 |

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
