# CREDITS — 러다이트 2026
> 외부 에셋·오픈소스·AI 생성 에셋의 출처와 라이선스 기록. **제출물 #4의 "외부 에셋/오픈소스 출처" 절의 원천.**
> 위치: 저장소 루트 (심사자가 GitHub에서 바로 확인 가능해야 함)

## 기록 규칙 (CLAUDE.md 에셋 규칙과 동일)
1. **외부 에셋 반입 커밋 = 이 문서 갱신을 같은 커밋으로.** 분리 금지
2. 허용 소스: 자체 제작 / Kenney CC0 (UI 아이콘·SFX) / jsfxr·Bfxr 자체 생성 SFX / 라이선스 확인된 CC0 BGM
3. AI 생성 에셋: **비게임플레이 요소만** (타이틀 이미지 등). 도구명 + 프롬프트를 여기와 `AI_USAGE_LOG.md §4` 양쪽에 기록
4. 라이선스 불명 에셋 반입 금지. **실제 AI 서비스의 로고·트레이드드레스·명칭 사용 금지**
5. 항목 삭제 금지 — 에셋을 프로젝트에서 제거해도 "제거됨" 표기로 이력 유지

---

## 1. 자체 제작 (팀 저작물)
| 에셋 | 종류 | 제작자 | 비고 |
|---|---|---|---|
| 플레이어/적/투사체 스프라이트 전반 | 스프라이트 | 김정준 | 플랫 벡터 도형 스타일 |
| `Sprites/Placeholder_Square.png` (32px), `Placeholder_Circle.png` (64px), `Placeholder_Triangle.png` (64px, D2 추가) | 스프라이트 (플레이스홀더) | 팀 저작 — 프로젝트 자체 코드로 절차적 생성 | D1~D2 회색 박스용 흰색 도형. 외부 에셋·AI 이미지 생성 모델 미사용(단순 for 루프 도형 렌더링)이므로 §5 AI 생성 에셋에 해당하지 않음. 최종 아트로 교체 예정 |
| <!-- 추가 시 행 추가 --> | | | |

## 2. 외부 에셋
| 에셋 | 출처 (URL) | 라이선스 | 사용 위치 | 반입 커밋 |
|---|---|---|---|---|
| **x10y12pxDenkiChipHangul (전기칩 한글)** — `Fonts/x10y12pxDenkiChipHangul.ttf` + TMP Font Asset | 제작: Lee Minseo (quiple@quiple.dev) / 원본 기반 폰트: The x8y12pxDenkiChip Project Authors — https://github.com/hicchicc/x8y12pxDenkiChip (제작자 患者長ひっく) | **SIL Open Font License 1.1** | 게임 내 모든 UI 텍스트 (TMP 기본 폰트). 12px 한글·일본어 픽셀 폰트, Adobe-KR-9 보충 0 한글 2,780자 + 일본 한자 640자 지원 | <!-- D3 반입 커밋 해시 --> |
| <!-- 예: Kenney UI Pack --> | <!-- kenney.nl/assets/... --> | CC0 1.0 | | |

**OFL 1.1 준수 메모 (전기칩 한글)**: 상업적 이용·임베드·수정·재배포 허용, 출처 표기는 의무 아님, **폰트 단독 유료 판매만 금지**(게임에 포함한 배포는 허용) — WebGL 빌드에 폰트 데이터를 동봉하는 우리 방식은 허용 범위 안이다. OFL 1.1 제2항이 요구하는 **라이선스 사본·저작권 고지 동봉**은 `Assets/_Project/Fonts/x10y12pxDenkiChipHangul - OFL.txt` 로 충족했다 (D3 반입, TMP가 `LiberationSans - OFL.txt`를 동봉한 것과 같은 형태). 폰트를 수정하지 않았으므로 제3항(Reserved Font Name)·제4항은 해당 없음.

## 3. 자체 생성 SFX
| 사운드 | 도구 | 생성 방법 | 사용 위치 |
|---|---|---|---|
| <!-- 예: PredictionFailed 글리치음 --> | jsfxr / Bfxr | 파라미터 자체 튜닝 | |

## 4. BGM
| 트랙 | 출처 | 라이선스 | 비고 |
|---|---|---|---|
| | | | |

## 5. AI 생성 에셋 (비게임플레이 요소만)
| 에셋 | 도구 | 프롬프트 (원문) | 사용 위치 | AI_USAGE_LOG 반영 |
|---|---|---|---|---|
| | | | | |

## 6. 오픈소스 / 패키지
| 이름 | 버전 | 라이선스 | 용도 |
|---|---|---|---|
| Unity 6 | 6000.3.7f1 | Unity 약관 | 엔진 |
| Universal RP (URP) | | Unity Companion License | 2D 렌더러 |
| TextMeshPro | | Unity Companion License | UI 텍스트 |
| TMP Essential Resources (`Assets/TextMesh Pro/`) | ugui 패키지 동봉 | 에셋 Unity Companion / LiberationSans 폰트 SIL OFL 1.1 | UI 텍스트 렌더링 기본 리소스 — D2 GameState 골격 UI에 반입. 한글 글리프 없음 → D3에 OFL 한글 폰트 반입 예정 |
| <!-- ML-Agents/Sentis는 §14.1 착수 시에만 추가 --> | | | |

## 7. 개발 도구 (에셋 아님 — 참고 기재)
- Claude Code + Unity MCP — 코드 작성·에디터 연동 (상세: `Assets/_Project/Docs/AI_USAGE_LOG.md`)
