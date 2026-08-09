using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Luddite.Core;

namespace Luddite.UI
{
    /// <summary>
    /// <see cref="GameState"/> ↔ 화면 패널 라우팅 (GDD §1.2 — 화면 7종).
    /// <see cref="GameEvents.GameStateChanged"/>를 구독해 상태당 패널 1개만 켠다.
    /// Combat은 전용 패널이 없다 — HUD(D3)가 따로 붙는다.
    ///
    /// <para>
    /// UI는 게임 상태를 <b>읽고 표시만</b> 하고, 전환 의사는 전부 <see cref="GameManager"/>의
    /// 전환 API를 호출하는 것으로 표현한다 (규칙 7과 같은 정신 — UI가 상태를 직접 소유하지 않는다).
    /// 버튼 배선은 인스펙터 UnityEvent가 아니라 코드로 한다 (규칙 4 — UnityEvent 남발 금지).
    /// </para>
    ///
    /// </summary>
    public class GameScreens : MonoBehaviour
    {
        // §1.4 승패 문구 — 인간 세계 = 한국어 원문 (§10.5, D3 반입 한글 폰트 전제).
        // 아랫줄 영어는 AI 시스템 발화 병기 (§10.5의 병기 규칙을 역방향으로 적용)
        private const string RESULT_WIN = "축하합니다. 인간의 필요성이 24시간 연장되었습니다.\n<size=60%>HUMAN NECESSITY EXTENDED BY 24 HOURS.</size>";
        private const string RESULT_LOSE = "당신의 직업은 대체되었습니다.\n<size=60%>YOUR JOB HAS BEEN REPLACED.</size>";

        [SerializeField] private GameManager _gameManager;

        [Header("상태별 패널 (상태당 1개만 활성)")]
        [SerializeField] private GameObject _titlePanel;

        [Tooltip("Combat 전용 HUD (§10.1). 상태 패널이 아니라 별도 토글")]
        [SerializeField] private GameObject _hudPanel;
        [SerializeField] private GameObject _majorSelectPanel;
        [SerializeField] private GameObject _waveIntervalPanel;
        [SerializeField] private GameObject _bossIntroPanel;
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private GameObject _pausePanel;

        [Header("버튼 (배선은 코드에서)")]
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _majorLiberalArtsButton;
        [SerializeField] private Button _majorScienceButton;
        [SerializeField] private Button _majorArtsButton;
        [SerializeField] private Button _nextWaveButton;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _pauseToTitleButton;
        [SerializeField] private Button _resultToTitleButton;

        [Header("동적 텍스트")]
        [SerializeField] private TMP_Text _resultMessage;

        private void Awake()
        {
            if (_gameManager == null)
            {
                Debug.LogError("[GameScreens] GameManager 미지정 — 화면 전환 불가", this);
                return;
            }

            Wire(_startButton, () => _gameManager.StartRun());
            Wire(_majorLiberalArtsButton, () => _gameManager.SelectMajor(Major.LiberalArts));
            Wire(_majorScienceButton, () => _gameManager.SelectMajor(Major.Science));
            Wire(_majorArtsButton, () => _gameManager.SelectMajor(Major.Arts));
            Wire(_nextWaveButton, () => _gameManager.ContinueToNextWave());
            Wire(_resumeButton, () => _gameManager.ResumeFromPause());
            Wire(_pauseToTitleButton, () => _gameManager.ReturnToTitle());
            Wire(_resultToTitleButton, () => _gameManager.ReturnToTitle());
        }

        private void OnEnable() => GameEvents.GameStateChanged += OnGameStateChanged;

        private void OnDisable() => GameEvents.GameStateChanged -= OnGameStateChanged;

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.AddListener(() => AudioDirector.Play(GameSfx.UiButton));   // 모든 버튼 공통 클릭음 (§12)
            button.onClick.AddListener(action);
        }

        private void OnGameStateChanged(GameState previous, GameState next)
        {
            SetActive(_titlePanel, next == GameState.Title);
            SetActive(_majorSelectPanel, next == GameState.MajorSelect);
            SetActive(_waveIntervalPanel, next == GameState.WaveInterval);
            SetActive(_bossIntroPanel, next == GameState.BossIntro);
            SetActive(_resultPanel, next == GameState.Result);
            SetActive(_pausePanel, next == GameState.Paused);
            SetActive(_hudPanel, next == GameState.Combat);

            if (next == GameState.Result && _resultMessage != null)
                _resultMessage.text = _gameManager.RunWon ? RESULT_WIN : RESULT_LOSE;
        }

        private static void SetActive(GameObject panel, bool active)
        {
            if (panel != null && panel.activeSelf != active) panel.SetActive(active);
        }
    }
}
