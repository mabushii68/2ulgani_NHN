# CLAUDE.md — 러다이트 2026
> Claude Code + Unity MCP 세션 규칙. **모든 세션은 이 문서를 전제로 동작한다.**
> 게임 디자인의 유일한 진실 원천은 `GDD.md` — 디자인 질문은 GDD를 먼저 읽고, 없으면 사람에게 묻는다. 추측 구현 금지.

## 프로젝트 개요
- **러다이트 2026**: 2D 탑뷰 아레나 웨이브 슈터. 적 AI가 플레이어의 회피 습관을 온라인 학습하고, 플레이어는 그 학습을 읽고 속인다
- NHN 게임 AI 공모전 제출작. 마감 2026-08-10. 개발 10일, 2인
- 산출물: Unity WebGL → GitHub Pages (링크 클릭만으로 플레이). `.exe` 금지
- 정체성 문구: "AI는 당신의 플레이를 학습합니다. 그러니 AI에게 거짓말하세요."

## 환경 (고정)
- **Unity 6000.3.7f1** — 이 프로젝트의 실제 사용 버전. 버전 변경 금지
- **입력 코드는 구 Input Manager API만 사용** (`Input.GetAxisRaw` / `Input.GetMouseButton` — 입력 6개뿐이라 신 Input System은 과잉)
  - Player Settings의 Active Input Handling은 **`Both`(activeInputHandler: 2)** 로 둔다. 프로젝트가 URP 2D 템플릿에서 시작해 Input System 패키지가 이미 물려 있기 때문 — `Old` 전용으로 바꾸지 않아도 무해하다
  - 단, **`InputSystem_Actions.inputactions` 및 `UnityEngine.InputSystem` API는 사용하지 않는다**
- **URP 2D Renderer** / Global Light 2D 1개만, 동적 라이트 금지 / 포스트프로세싱은 **Bloom 1개만** (마젠타 발광용, HDR 색상)
- 해상도 1920×1080, WebGL 캔버스 16:9 고정
- **패키지**: 현재 `Packages/manifest.json`에 설치된 집합을 기준선으로 삼는다 (URP, ugui/TextMeshPro, 2D 툴셋, Input System, Timeline, Visual Scripting, Test Framework, Unity MCP 등 — URP 2D 템플릿 기본값). **여기에 새 패키지를 추가하는 것은 사람 승인 필수**
  - 실제로 코드가 의존하는 것은 URP + ugui/TMP + 구 Input Manager API뿐이다. 미사용 패키지 제거는 GUID·템플릿 의존성을 깨뜨릴 수 있어 임의로 하지 않으며, 빌드 사이즈 영향은 **D8 WebGL 검증에서 실측 후 판단**한다
  - 예외 (조건부): `com.unity.ml-agents` + `com.unity.sentis` — GDD §14.1 착수 조건(D8 WebGL 검증 통과 + 잔여 1.5일 이상) 충족 시, 사람 승인 하에만 추가. 그 전에 요청받으면 거절하고 착수 조건을 안내할 것. ML 정책은 보스 이동 전용이며 회피 심리전(§7) 대체 금지
- Assembly Definition **사용 안 함**
- WebGL 빌드: Compression **Gzip + Decompression Fallback** (GitHub Pages는 Brotli MIME 미지원), 목표 < 50MB
- 빌드 씬 목록은 **`Assets/_Project/Scenes/Main.unity` 단 하나** (D1 세션 2에 템플릿 `Assets/Scenes/SampleScene.unity`에서 교체)

## 폴더 구조 (준수)
> **실제 리포 구조 기준** (D1 세션 2에 현행화). 우리 작업물은 전부 `Assets/_Project/` 아래에 둔다 — `Assets/` 루트는 URP 2D 템플릿이 만든 것이므로 건드리지 않는다.

```
Assets/_Project/
├── Scenes/        # Main.unity 단 하나 (씬 추가 금지)
├── Scripts/
│   ├── Core/      # GameManager, GameState, GameEvents, WaveManager
│   ├── Player/
│   ├── Enemies/   # FSM, EliteModifier, Boss
│   ├── AIBrain/   # ⭐ 순수 C# 전용 — MonoBehaviour 금지
│   ├── Combat/    # 투사체, 판정, 히트스톱
│   ├── UI/
│   └── Data/      # SO 정의 클래스 (ScriptableObject 상속 코드)
├── SO/            # SO 인스턴스(.asset) = 밸런스 수치의 유일한 위치
├── Prefabs/       # 플레이어, 적, 투사체 프리팹
├── Sprites/       # 게임 내 스프라이트 (플레이스홀더 포함)
├── Materials/     # 머티리얼
├── Audio/         # SFX, BGM
├── Docs/          # AI_USAGE_LOG.md, TEAM_ROLES_LOG.md (세션마다 갱신), SUBMISSION.md, CREDITS.md, GDD.md
├── Art/           # 용도 미확정 — 사람 확정 전까지 사용 금지 (아트 원본 소스 후보)
├── Data/          # 용도 미확정 — SO 인스턴스는 SO/, SO 정의 코드는 Scripts/Data/. 사용 금지
├── Input/         # 미사용 (구 Input Manager API를 쓰므로 .inputactions 불필요)
└── Settings/      # 미사용 (URP 렌더러·볼륨 애셋은 Assets/Settings/ 에 있음)
```
- `Art/` · `Data/` · `Input/` · `Settings/` 4개는 프로젝트 초기 세팅 때 만들어졌으나 **현재 비어 있고 용도가 확정되지 않았다.** 새 파일을 여기에 넣지 말고, 필요하면 사람에게 용도를 물어 이 표를 먼저 갱신한다
- 리포 루트 `CREDITS.md`는 존재하지 않는다 — 실제 위치는 `Assets/_Project/Docs/CREDITS.md`

## 아키텍처 규칙
1. **단일 씬 + GameState 상태 머신** (`Title, MajorSelect, Combat, WaveInterval, BossIntro, Result, Paused`). 씬 분리 금지
2. **밸런스 수치는 전부 ScriptableObject.** 코드 하드코딩 금지 (연출 타이밍만 `[SerializeField]` 허용). 매직 넘버 금지
3. **`AIBrain/`은 순수 C#** — Unity 없이 가짜 이벤트 시퀀스로 검증 가능해야 함. MonoBehaviour 어댑터(`AIBrainRunner`)가 이벤트만 전달
4. 이벤트: C# `event` + 정적 이벤트 버스 1개(`GameEvents`). UnityEvent 남발 금지, DI 프레임워크 없음
5. GameManager에 책임 집중 금지, Singleton 최소화, 기능별 소형 컴포넌트, public field 남발 금지
6. **공격 로직은 `IWeapon` 인터페이스** — PlayerController에 강결합 금지 (D8 최종 무기 교체 대비)
7. UI는 AI 데이터를 **읽기만** 한다 — UI가 AIBrain을 직접 수정 금지 (업그레이드 효과는 전용 API 경유)
8. 네임스페이스 `Luddite.{폴더명}`, 클래스/메서드 `PascalCase`, private `_camelCase`, 상수 `UPPER_SNAKE`. 주석·로그 한국어 허용

## 🔴 계약 (변경 시 반드시 경고 후 사람 승인)
아래는 GDD의 구조적 계약이다. 변경 요청을 받으면 **"이것은 계약 변경입니다"라고 명시하고 파급 범위를 설명한 뒤 승인을 기다린다:**
- 피격 위기 이벤트 정의 (TTI 0.5s 트리거 / 0.6s 판정 / 변위 0.3u 미달 제외 / TTI 최단 1개)
- LEFT/RIGHT 2분류 학습 구조 (8방향 회귀 금지)
- 확률 수식: 감쇠는 관측 카운트만 ×0.8, 가상 카운트(1,1) 고정 합산
- 마젠타 = AI 위협 색 규칙 / 고정 카메라 / 단일 씬 / 웨이브 전멸형 종료 / 플레이어 피격 넉백 없음

## 세션 워크플로우
**작업 전:**
1. `GDD.md` 해당 절 + 오늘 일차 일정을 읽는다
2. 관련 기존 파일을 먼저 읽는다 — 새 시스템이 기존과 겹치는지 확인
3. 1 세션 = 1 기능. 거대 기능 일괄 구현 금지. 요청 범위 밖 리팩터링 금지

**코드 수정 중:**
- 기존 코드 이유 없는 삭제 금지 / 기존 public API 임의 변경 금지
- 임시 구현은 `// TODO(목적):` 명시
- 수치는 SO/인스펙터 노출 (규칙 2)

**세션 종료 조건 (전부 충족 후 커밋):**
1. MCP로 Unity 콘솔 조회 → 컴파일 에러 0, 런타임 에러 0
2. 플레이 모드 스모크: 대상 기능 동작 확인
   - **키보드·마우스 입력은 MCP로 주입할 수 없다.** 입력이 걸린 기능은 코드 경로를 직접 호출하는 임시 `[MenuItem]` 스모크로 최대한 자동 검증하고, 남는 입력 경로는 **사람에게 확인 절차를 제시하고 결과를 받은 뒤** 커밋한다
3. `AIBrain` 변경 시: 순수 C# 테스트 루틴(가짜 이벤트 주입 → 확률/신뢰도 출력) 실행, 결과 로그
4. 변경 파일 목록 + 테스트 절차 + 신규 설정값 의미·초기값 요약
5. `Assets/_Project/Docs/AI_USAGE_LOG.md`에 세션 기록 append + `TEAM_ROLES_LOG.md` §2에 실작업 1행 append (요청자/영역/변경 파일/교차 여부)
- **에러가 남은 상태로 세션 종료 절대 금지**

**커밋:** `[D일차][타입] 내용` — 타입: feat/fix/balance/art/ui/docs/build/chore. 하루 최소 3커밋

**브랜치:** 여러 브랜치를 사용한다. 담당자별 작업 브랜치(예: 김정준 = `JungJoon`)에서 진행하고 `main`으로 병합한다. 현재 어느 브랜치인지 세션 시작 시 확인하고, 브랜치를 새로 만들거나 병합·푸시하는 것은 **사람이 지시할 때만** 수행한다

## 금지 목록
- `ProjectSettings/` 변경 (제안만, 수행은 사람)
- `Packages/manifest.json` 변경 (사람 승인 후)
- `.meta` 직접 편집, GUID 깨는 파일 이동, 사용 중 Scene/Prefab 무단 삭제
- `SO/` 밸런스 수치 임의 변경 — 명시적 `[balance]` 요청 시만
- 빌드 설정 변경 — D4/D8 빌드 세션에서 사람 확인 하에만
- **API 키 클라이언트 하드코딩 (절대 금지)**
- 라이선스 불명 에셋 추가 / 전체 구조 임의 변경 / 요구되지 않은 대규모 리팩터링

## AI 활용 로그 (심사 제출 자료 — 누락 금지)
- **단일 문서**: `Assets/_Project/Docs/AI_USAGE_LOG.md` — 제출물 #4(AI 활용 기술 문서)의 원천 데이터. 문서 내 §0 기록 규칙을 따른다
- **세션 종료 시 Claude Code가 직접 append** (세션 종료 조건 5번). 사람 기억 재구성 금지
- 기록 단위: `§3 일자별 세션 로그`에 `세션 N — 기능명 / 목표 / 핵심 프롬프트 원문 / 결과·수정 / 커밋 해시`
- 대표 프롬프트(구조 설계·AIBrain 지시·인상적 성공/실패 사례)는 발견 즉시 같은 문서 `§2`에 복사
- AI 생성 에셋 발생 시 `§4` 기록 + CREDITS.md 갱신을 **같은 커밋**으로
- 시도 후 폐기한 AI 활용(예: ML-Agents)은 `§5`에 기록 — 삭제 금지, 최종 문서 서사 자료
- 기존 기록 수정·삭제 금지. D10에 이 문서를 편집·요약해 최종 PDF 제작

## 제출 문서 지도
| 문서 | 위치 | 갱신 시점 | 용도 |
|---|---|---|---|
| `AI_USAGE_LOG.md` | `Assets/_Project/Docs/` | **매 세션 종료** | 제출물 #4 원천 |
| `TEAM_ROLES_LOG.md` | `Assets/_Project/Docs/` | **매 세션 종료** (§2 1행) + 페어 작업 시 §3 | 제출물 #5 원천 |
| `SUBMISSION.md` | `Assets/_Project/Docs/` | D9 1차 / D10 최종 점검 | 제출 5종 누락 방어 체크리스트 |
| `CREDITS.md` | `Assets/_Project/Docs/` | **에셋 반입과 같은 커밋** | 제출물 #4 출처 절 + 실격 리스크 차단 |
| `GDD.md` | `Assets/_Project/Docs/` | 디자인 결정 변경 시 (사람) | 구현 기준의 유일한 진실 원천 |
| `README.md` | 저장소 루트 | D4 스모크 빌드 후 플레이 링크 / D9 영상·스크린샷 / D10 최종 | 심사자 첫 화면 (제출물 #1의 일부) |
- 각 문서 상단의 기록 규칙이 우선. 문서 형식 임의 변경 금지
- Claude Code는 세션 중 위 문서의 갱신 시점이 도래하면 사람에게 상기시킬 것 (예: 에셋 반입 감지 → CREDITS 갱신 요구)

## 에셋 규칙
- 외부 에셋 반입 커밋 = CREDITS.md 갱신 **같은 커밋** (분리 금지)
- 허용: 자체 제작 스프라이트 / Kenney CC0 (UI 아이콘·SFX) / jsfxr·Bfxr 자체 생성 SFX
- AI 생성 에셋: 비게임플레이 요소만, 도구명+프롬프트를 CREDITS.md와 ai-usage-log 양쪽 기록
- **실제 AI 서비스 로고·트레이드드레스·명칭 사용 절대 금지**

## 팀 & 담당 (충돌 방지 기준)
| | 이양빈 (기획/프로그래밍) | 김정준 (아트/프로그래밍) |
|---|---|---|
| 영역 | 게임의 '뇌': AIBrain 전체, 전투 로직, 적 FSM, 보스, 밸런스 결정, GDD 관리 | 게임의 '얼굴': 아트 전반, 예측탄 시각 언어, HUD/UI, WebGL 파이프라인, CREDITS 관리 |
- 접점: `PREDICTION FAILED` 연출(로직×비주얼) — D4 페어 작업
- 세션 요청자가 누구든, 상대 담당 영역 파일 수정 시 요약에 명시

## 10일 일정 앵커 (세부는 GDD·통합기획문서 참조)
D1 셋업+최소 전투(+빈 프로젝트 WebGL 빌드 1회) / D2 AIBrain 프로토 / D3 **GO/PIVOT 1차(구조)** / D4 손맛+**GO/PIVOT 2차(재미)**+스모크 빌드 / D5 적3종+웨이브+업그레이드 / D6 매크로 DDA+UX / D7 보스+결과 프로필 / D8 최종 무기+WebGL 검증 / D9 폴리싱+영상 / D10 문서 3종 PDF+제출 점검