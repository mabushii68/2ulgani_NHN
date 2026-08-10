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

| D4 | — | 김정준 | ⚠️ **세션 기록 누락.** 커밋만 존재: `1e193d1`(v2 라이브러리 3x→1x 전환 + 임포트 복구 도구) / `4278d68`(SYSTEMS.md 구현 현황 문서 신설) / `aeab4c6`(TMP 폴백 폰트 재직렬화) | `Scripts/Editor/V2LibraryRescaleTools.cs`, `Docs/SYSTEMS.md`, `Sprites.v2/*`(미커밋) | ❌ | 당시 세션에서 §2 append가 이뤄지지 않았다. §0 규칙상 사후 재구성을 하지 않으므로 커밋 기록으로만 남긴다 |
| D5 | 1 | 김정준 | 개정안 v1.1 검토 + `CLAUDE.md` 델타 병합 — 추적 카메라 계약이 산수상 무효임을 실측 반증, `CLAUDE_v1.1.md` 회귀 8건 차단, 일정·판정 기준 현행화 | `CLAUDE.md`, `Docs/GDD_AMENDMENT_v1.1.md`(편입) | ⭕ | 규칙·일정 문서는 공동 자산. **개정안은 이양빈과 공동 소유 문서**이나 내용 변경 없이 실측 반증과 구현 현황만 덧붙임. 계약 #1·#4는 승인 대기로 분리해 두었다가 다음 세션에 사람이 확정 |
| D5 | 2 | 김정준 | 회색조 폐기 → 컬러 복원 53파일, 던전 타일셋 7파일 승격(PPU 16 슬라이스 168장), 방 24×14→32×18 확대, `CameraFollow` 신설 | `Sprites/{Arena,Enemies,Projectiles,UI}/*`, `Sprites/Dungeon/*`, `Scripts/Core/CameraFollow.cs`, `SO/WaveSystemConfig_Default.asset`, `Scenes/Main.unity`, `Docs/CREDITS.md` | ⭕ | 아트 = 담당 정면. 교차: **`WaveSystemConfig`의 스폰 링 12×7→16×9는 밸런스 수치**(이양빈 영역) — 방 확대에 강제로 딸려오는 값이라 함께 조정, 되돌리려면 SO 한 곳. 🔴 §10.4 조문("무채색~주황")을 근거로 계약 위반 없이 컬러화. CREDITS 라이선스 준수 논거 정정 포함 |
| D5 | 3 | 김정준 | 던전 체인 골격 — `DungeonConfigSO`·`Door`·`Chest`·`Room`·`DungeonManager` + 결정론 빌더 + 스모크 49건 | `Scripts/Core/{Door,Chest,Room,DungeonManager}.cs`, `Scripts/Data/DungeonConfigSO.cs`, `Scripts/Core/WaveManager.cs`, `Scripts/Editor/{DungeonSetupTools,DungeonSmokeTest}.cs`, `SO/DungeonConfig_Default.asset`, `Scenes/Main.unity` | ⭕ | **`WaveManager` 수술 = 이양빈 담당 영역**(웨이브·전멸 판정). 기존 지시("담당 상관없이") 연장선. 변경은 전부 가산이라 기본값이 D4 동작과 동일 — 폴백을 코드 구조로 보장 |
| D5 | 4~6 | 김정준 | 사람 발견 결함 3건 수정 — ①전투방 입구 문이 처음부터 잠겨 진입 불가 ②복도에서 카메라 정지 ③카메라 여유 부족으로 문이 화면 밖 | `Scripts/Core/{Room,DungeonManager,CameraFollow}.cs`, `Scripts/Editor/{DungeonSetupTools,DungeonSmokeTest}.cs`, `Scenes/Main.unity` | ❌ | 던전·카메라 = 담당 정면. 스모크에 통행 물리 프로브를 추가해 같은 계열이 배선 검사를 통과해도 잡히게 함(49→51건) |
| D5 | 7 | 김정준 | 물리 보간 누락 수정 — 렌더 410fps / 물리 50Hz에서 캐릭터가 계단식으로 튀던 진동 | `Scripts/Player/PlayerController.cs`, `Scripts/Enemies/EnemyBase.cs`, `Scripts/Combat/Projectile.cs` | ⭕ | **`EnemyBase`·`Projectile` = 이양빈 담당 영역** — 로직 변경 없이 렌더 설정 1행씩. 카메라가 움직이는 상태에서는 적·탄도 같이 떨려 플레이어만 고치면 증상이 남는다. 🔴 AIBrain 영향 없음을 자체 검증 75건으로 확인 |
| D5 | 8 | 김정준 | `MAP_SPEC.md` 정렬 — 맵 바깥 어둠 복원(암반 철회)·조도 0.88·락인 후 0.5s 스폰·`SetLocked` API, 스펙 §2 좌표 표 실측 10행으로 채움 + §11 구현 현황 신설 | `Scripts/Core/{Door,DungeonManager}.cs`, `Scripts/Editor/DungeonSetupTools.cs`, `Docs/MAP_SPEC.md`, `Scenes/Main.unity` | ⭕ | **`MAP_SPEC.md`는 김정준×이양빈 공동 문서.** 스펙이 옳은 곳은 구현을, 비어 있거나 낡은 곳은 문서를 고치는 양방향 정렬. 미구현 7항목을 사유와 함께 문서에 남김 |
| D5 | 9 | 김정준 | Sorting Layer 9종 정의 + 벽 높이 착시 + 꺾인 체인(꺾임 4회)·복도 폭 3~7 변주 + 기둥 28·소품 55 배치 | `ProjectSettings/TagManager.asset`, `Scripts/Editor/{DungeonSetupTools,SortingLayerSetupTools}.cs`, `Sprites/Dungeon/Decor/*`, `Scenes/Main.unity` | ⭕ | ⚠️ **`ProjectSettings/` 변경은 CLAUDE.md 금지 목록(사람 수행)이나 사람이 직접 지시해 수행.** 🔴 기둥·장식은 **전부 비충돌** — 계약 #2·MAP_SPEC §7의 금지 사유(적 FSM 장애물 회피 없음 / 탄 차단 시 학습 표본 오염)가 실제 고장이라, 충돌 기둥은 이양빈 승인 대상으로 분리 |
| D6 | 1 | 김정준 | 제출 문서 3종 초안 + 누락된 D4·D5 세션 로그 소급 정리 | `Docs/{AI_USAGE_LOG,TEAM_ROLES_LOG}.md`, `Docs/SUBMIT_*.md`(신규 3종) | — | 제출 문서 = 공동. D4 로그는 §0 규칙(사후 재구성 금지)에 따라 커밋 기록으로만 표기 |
| D6 | 2 | 김정준 | 적 탄·엘리트 마젠타 텔레그래프가 안 보이던 문제 — `Default` 정렬 레이어 잔류 2건 + 배정 빌더의 조용한 실패 2건 | `Prefabs/{EnemyProjectile,EliteDrone}.prefab`, `Scripts/Editor/SortingLayerSetupTools.cs`, `Docs/{AI_USAGE_LOG,TEAM_ROLES_LOG}.md` | ⭕ | 정렬 레이어·마젠타 시각 언어 = 김정준 담당 정면. **적·엘리트 프리팹은 이양빈 담당 영역**이나 변경은 렌더링 배정(`m_SortingLayerID`)에 한정 — FSM·전투 수치·SO 무변경. 빌더 오타(`EliteChatbot` → 실제 `EliteDrone`)가 로그 없이 `continue`해 `b0834ea`의 수정이 두 프리팹을 건너뛴 것이 재발 원인 |
| D7 | 1 | 이양빈 (Git `VJL02` — 본인 확인 요) | 보스 P2 「PATTERN: YOU」(§9) — 거리 복제·무기 복제(마젠타)·구역 장판·예측탄(EliteModifier 재사용)·전환 연출 + 계약 #4 온스크린 게이트 + 프로파일러 4분할 원점 보정 + 🔴 토글 OFF 스폰 링 소프트락 정정 | `Scripts/Enemies/{BossLLM,BossZoneHazard,EliteModifier,EnemyBase}.cs`, `Scripts/Core/{GameEvents,AIBrainRunner,DungeonManager,WaveManager}.cs`, `Scripts/Data/{BossConfigSO,WaveSystemConfigSO}.cs`, `Scripts/UI/BossPhaseOverlay.cs`, `Scripts/Editor/{BossSetupTools,BossP2SmokeTest}.cs`, `SO/WaveSystemConfig_Default.asset` | ⭕ | 보스·전투 = 이양빈 담당 정면. 교차: HUD 오버레이·마젠타 시각 언어(김정준 영역)는 기존 PredictionFailedOverlay 패턴 복제로 한정. **`WaveSystemConfig` 링 12×7 복원은 밸런스 수치이나 D5 방 확대가 폴백 아레나를 깨뜨린 것의 원상 복구** — 던전 모드 링은 15×8로 동일 유지 (SetSpawnArea가 방 규격에서 계산) |
| D7 | 2 | 이양빈 (Git `VJL02` — 본인 확인 요) | 오디오 전량(§12 — 자체 절차 합성 SFX 9종 + BGM + AudioDirector·배선 빌더) | `Audio/*`(신규 11), `Scripts/Core/AudioDirector.cs`, `Scripts/Editor/AudioSetupTools.cs`, 훅 5파일, `Docs/CREDITS.md` | ⭕ | 오디오·CREDITS = 김정준 담당 영역이나 위임 지시("계속 쭉 진행해")로 수행. 소스는 팀 저작 절차 합성 — 라이선스 이슈 0. 품질 교체는 WAV 파일만 갈아끼우면 됨 |
| D7 | 3 | 이양빈 (Git `VJL02` — 본인 확인 요) | UI 한국어 교체(§10.5) — 카드·별명·승패·씬 텍스트 한국어 원문, AI 발화 영어 유지, ROOM/WAVE 표기 분기 | `Scripts/UI/{UpgradePanel,ResultProfile,GameScreens,WaveLabel}.cs`, `Scripts/Editor/GameFlowSetupTools.cs` | ⭕ | UI = 김정준 담당 영역이나 위임 지시로 수행. 씬 반영은 `GameState 골격` 빌더 재실행 + 폰트 빌더 마지막 재실행 필요 |
| D7 | 4 | 이양빈 (Git `VJL02` — 본인 확인 요) | 화면 밖 위협 화살표(개정안 §3 보정 ① — 추적 카메라 동반 계약 완결) | `Scripts/UI/OffscreenThreatIndicator.cs`, `Scripts/Editor/OffscreenThreatSetupTools.cs` | ⭕ | HUD = 김정준 담당 영역이나 위임 지시로 수행. 엘리트·보스 마젠타 / 일반 무채색 (§10.4) |
| D7 | 5 | 이양빈 (Git `VJL02` — 본인 확인 요) | Dungeon Tileset 출처 특정 — Franuka Dungeon Asset Pack URL·라이선스 확정 (실격 리스크 해소) | `Docs/{CREDITS,SUBMIT_4_AI활용,SYSTEMS}.md` | ⭕ | CREDITS = 김정준 담당 영역이나 위임 지시로 수행. 웹 검색 + 승격 파일 이미지 대조로 특정 |
| D7 | 6 | 이양빈 (Git `VJL02` — 본인 확인 요) | 제출 문서 PDF 변환 파이프라인 — 인쇄용 HTML 3종 + 자동 컷 절 처리 | `Docs/PDF~/build_pdf_html.py`, `Docs/PDF~/SUBMIT_*.html` ×3 | — | 제출 문서 = 공동. PDF 제작이 Ctrl+P 1회로 축소 |
| D7 | 7 | 이양빈 (Git `VJL02` — 본인 확인 요) | 과목 탄막 스프라이트 5종 절차 생성 (펜·컴퓨터·번개·공·음표 — 팀 저작물) | `Scripts/Editor/SubjectBulletSpriteTools.cs`, `Sprites/Icons/Procedural/Proc_*.png` ×5 | ⭕ | 아트 = 김정준 담당 영역이나 위임 지시로 수행. 나머지 4테마(책·돈·숫자·붓)는 기존 Franuka 아이콘 재사용 |
| D7 | 8 | 이양빈 (Git `VJL02` — 본인 확인 요) | 전공 버튼 한국어 씬 반영 — 세션 3 이월분 빌더 재실행 (코드 변경 0) | `Scenes/Main.unity` | ⭕ | UI = 김정준 담당 영역이나 위임 지시로 수행. GameState 골격 → 폰트 빌더 순서 규칙 준수 |
| D7 | 9 | 이양빈 (Git `VJL02` — 본인 확인 요) | 세부전공 선택 (첫 인터벌 대체, 9종) + 전공 버튼 간격 확대 | `Scripts/Core/{SubMajor,GameManager}.cs`, `Scripts/UI/{SubMajorPanel,UpgradePanel}.cs`, `Scripts/Editor/{SubMajorSetupTools,GameFlowSetupTools,SpriteBindingSetupTools}.cs`, `Scenes/Main.unity` | ⭕ | 게임 플로우 = 이양빈 / UI·레이아웃 = 김정준 영역 교차. 탄막 차별화는 보류(선택 저장까지) |
| D7 | 10 | 이양빈 (Git `VJL02` — 본인 확인 요) | 세부전공 탄막 9종 배선 — SO 매핑 + BasicWeapon 발사 시점 교체 | `Scripts/Data/SubMajorBulletSetSO.cs`, `Scripts/Combat/BasicWeapon.cs`, `Scripts/Editor/SubMajorSetupTools.cs`, `SO/SubMajorBulletSet.asset`, `Scenes/Main.unity`, `Docs/CREDITS.md` | ⭕ | 전투 로직 = 이양빈 / 탄막 시각 언어 = 김정준 영역 교차. 테마 탄은 무틴트(원색 아이콘 보존) |
| D7 | 11 | 이양빈 (Git `VJL02` — 본인 확인 요) | 최종 WebGL 빌드(18.33MB)·gh-pages 배포 커밋·제출 문서 현행화·누락 .meta 18개 수정 | `Builds/`(비추적), `gh-pages` 브랜치, `Docs/SUBMIT_3·4`, `SUBMISSION.md`, `README.md`, `.gitignore`, `.meta` ×18 | ⭕ | WebGL 파이프라인 = 김정준 담당 영역이나 위임 지시로 수행. 푸시 2건은 사람 터미널 |
| D7 | 12 | 이양빈 (Git `VJL02` — 본인 확인 요) | 밸런스 패치(몹 HP×2·탄속×1.5 / 보스 HP×4·탄속×2·공속×2.5) + 최종 빌드 재생성 | `SO/EnemyStats_*.asset` ×5, `gh-pages` 브랜치 | — | 밸런스 결정 = 이양빈 본인 영역. [balance] 명시 요청 |
| D7 | 13 | 김정준 (Git `김정준`) | 오디오 무음 결함 수정 — AudioListener 유실(D5 카메라 재구성) + 배선 빌더 미실행 2중 원인. 리스너 보장을 빌더에 흡수 + `.meta` 17개 미추적 회수 | `Scripts/Editor/AudioSetupTools.cs`, `Scenes/Main.unity`, `Audio/*.wav.meta` ×10, `Scripts/{Core,UI,Enemies,Editor}/*.cs.meta` ×7 | ⭕ | 오디오·씬 배선 = 김정준 담당 정면. 교차: `Main.unity`에 `AudioDirector` 오브젝트 추가 — 씬은 공용 자산이나 빌더 결정론 생성이라 손편집 충돌 없음. **`.meta` 누락은 D7 세션 1~4(이양빈) 산출물 전체에 걸쳐 있었다** — 클론 시 GUID 재생성으로 참조가 끊기므로 최종 빌드 전 필수 회수 |
| D7 | 14 | 김정준 (Git `김정준`) | 폰트 글리프 결손(`·` → □) 수정 + 전수 검사(씬 33·SO 28·코드 149, 결손 0종) + `SYSTEMS.md` §15.6 잔여 목록 현행화 | `Scripts/UI/ResultProfile.cs`, `Docs/SYSTEMS.md` | — | 폰트·UI = 김정준 담당 정면. 세션 7 검증 중 부수 발견. §15.6 번호는 교차 참조(§16.1·§16.3) 보존 위해 고정 |
| D7 | 15 | 김정준 (Git `김정준`) | 맵 타일링·소품 배치 전면 수정 / HUD 재배치 + 무기·탄약 UI(30발 자동 재장전) / 우상단 미니맵 / 상자 E키 오픈 + 팝업 — 사람 요구 4건 일괄 | `Scripts/Editor/{DungeonSetupTools,HudSetupTools,GameFlowSetupTools,DungeonSmokeTest}.cs`, `Scripts/UI/{AmmoCounter,Minimap,PanelPopIn}.cs`, `Scripts/Combat/{IWeapon,BasicWeapon}.cs`, `Scripts/Core/{Chest,DungeonManager}.cs`, `Scripts/Data/PlayerStatsSO.cs`, `SO/DungeonConfig_Default.asset`, `Sprites/Dungeon/Generated/Wall_Side.png`, `Docs/CREDITS.md` | ⭕ | 맵·아트·HUD·에셋 = 김정준 담당 정면. **교차 2건**: ① 탄창은 신규 전투 메커닉(이양빈 영역), ② 미니맵은 개정안 v1.1 §6 컷 항목 — 둘 다 GDD 밖이라 파급을 고지하고 **요청자 결정으로 편입**. `SO/DungeonConfig` 수치 변경도 명시 요청 근거. 🔴 기둥 예약 색역(파랑) 위반을 함께 해소 |
| D7 | 16 | 김정준 (Git `김정준`) | JJ ↔ main 병합 — 충돌 21건 성격별 해소(.meta 17 GUID 정합 / 씬은 빌더 재실행으로 복원 / BasicWeapon 양쪽 결합 / 문서 재번호) + 병합 무관 선행 결함 2건 복구 | `Scenes/Main.unity`, `Prefabs/BossLLM.prefab`, `Scripts/Combat/BasicWeapon.cs`, `Docs/{AI_USAGE_LOG,TEAM_ROLES_LOG,SYSTEMS}.md`, `.meta` ×17 | ⭕ | 병합은 공동 영역. **선행 결함 2건이 이양빈 산출물**이었다 — ① 씬 오디오 배선 전무(배포된 빌드가 무음) ② 보스 P2 컴포넌트 프리팹 미부착(P2 스모크 7건 실패). 둘 다 빌더 재실행으로 복구, `git diff`로 병합 무관 확인 |
| D7 | 17 | 김정준 (Git `김정준`) | 탄약 아이콘 ↔ 세부전공 동기화 + 인게임 마우스 커서 (+ 우하단 패널 배경 시도 후 사람 판단으로 철회) | `Scripts/Combat/{IWeapon,BasicWeapon}.cs`, `Scripts/UI/{AmmoCounter,GameCursor}.cs`, `Scripts/Editor/HudSetupTools.cs`, `Sprites/UI/Generated/Cursor01_2x.png`, `Docs/CREDITS.md` | ⭕ | HUD·커서·에셋 = 김정준 담당 정면. 교차: `BasicWeapon`(전투=이양빈)에 아이콘용 게터 추가 — 세부전공 교체 규칙을 한 곳으로 모아 UI와 게임플레이가 어긋나지 않게 한 것 |
| D7 | 18 | 김정준 (Git `김정준`) | 적 피격 플래시 복구 — D5 컬러 전환 이후 흰색→흰색 no-op이던 연출을 빨강 페이드로. 🔴 보스 P2 마젠타가 피격 시 벗겨지던 계약 훼손 동시 차단 | `Scripts/Enemies/{EnemyBase,BossLLM}.cs`, `Prefabs/{BossLLM,ChatbotDrone,CoderBot,PainterBot}.prefab` | ⭕ | **적·보스 = 이양빈 담당 영역** — 연출 요청이라 진행. `EnemyBase.SetBaseColor()` 신설로 런타임 틴트 소유권을 명시 |

## 3. 협업·페어 작업 기록
| 일차 | 작업 | 참여 | 분업 방식 (누가 무엇을) | 결과 |
|---|---|---|---|---|
| <!-- D4 예정: PREDICTION FAILED 연출 --> | | | | |

## 4. 협업 방식 메모 (수시 기록)
> 제출물 #5의 "협업·분업 방식" 절 재료. 예: 브랜치 전략(main 단일), 세션 단위 분업, GDD를 통한 의사결정, 담당 영역 파일 소유권 규칙 등. 실제로 운영한 방식만 기록.

- (예시) main 단일 브랜치 + 세션 단위 커밋(`[D일차][타입]`)으로 충돌 최소화
- **D1 확정: main 단일 브랜치 → 담당자별 작업 브랜치 + main 병합**. 김정준 작업분은 `JungJoon` 브랜치에서 진행한다. 초기 계획(main 단일)은 2인 팀의 충돌 최소화를 노린 것이었으나, 담당 영역이 '뇌'(AIBrain·전투)와 '얼굴'(아트·UI)로 뚜렷이 갈려 브랜치 분리가 실제로는 충돌을 더 줄인다고 판단. `CLAUDE.md`도 같은 커밋에서 갱신
- **문서가 협업의 실제 매개였다.** 2인이 같은 시간에 붙어 있지 않아도 되도록, 의사결정을 대화가 아니라 문서에 남기는 방식으로 운영했다 — 설계는 `GDD.md`, 개편은 `GDD_AMENDMENT_v1.1.md`, 맵 세부는 `MAP_SPEC.md`, 현재 구현 상태는 `SYSTEMS.md`, 작업 규칙은 `CLAUDE.md`. 특히 **`CLAUDE.md`의 🔴 계약 절이 "혼자 바꾸면 안 되는 것"의 목록** 역할을 해서, 담당이 아닌 영역을 건드릴 때 무엇을 승인받아야 하는지가 자동으로 드러났다.
- **담당 교차를 막지 않고 '표시'했다.** 7일 일정에서 담당별로 칸막이를 세우면 기능이 안 굴러간다(예: 최소 전투를 만들려면 아트 담당이 투사체·데미지를 함께 짜야 한다). 그래서 교차 자체는 허용하되 **이 문서 §2의 `교차` 열에 ⭕로 표시하고, 상대 영역에서 무엇을 어디까지 건드렸는지 비고에 남기는 규칙**으로 운영했다. 되돌릴 때 어느 파일 한 곳만 보면 되는지가 기록에 남는다.
- **결정이 필요한 것은 구현하지 않고 분리해 올렸다.** GDD 미명시 값(`ThreatMissRadius`), 별명 11종 초안, 보스 패턴 데미지, 던전 방 안 충돌 기둥 등은 담당(이양빈)의 결정 사항이라 **선택지와 파급 범위만 정리해 대기 항목으로 남겼다.** 반대로 사람이 명시 지시한 것은 담당 영역이 아니어도 진행하고 커밋에 그 사실을 적었다.
- **사람의 역할은 '실플레이 검증'에 집중됐다.** Unity 창이 백그라운드면 게임 루프가 돌지 않고 키보드·마우스 주입도 불가하므로, 자동 검증은 `[MenuItem]` 스모크(코드 경로 직접 호출)까지만 가능하다. D5에 발견된 결함 4건(문 잠금·복도 카메라·문 가시성·물리 진동)은 **전부 사람이 실제로 플레이하다 발견**했고, 그때마다 스모크에 회귀 테스트를 추가해 같은 계열이 다음에는 자동으로 잡히게 했다.

## 5. D10 최종 문서 변환 체크리스트
- [ ] §1 + §2 집계 → 팀원별 "실제 구현 영역" 절 (계획 대비 실제 차이가 있으면 실제 기준으로)
- [ ] §3 + §4 → "협업·분업 방식" 절
- [ ] 교차 작업(⭕ 행)이 담당 서술과 모순되지 않는지 검토
- [ ] PDF 변환
