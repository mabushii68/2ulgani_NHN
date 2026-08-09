using UnityEngine;

namespace Luddite.Core
{
    /// <summary>
    /// <see cref="GameState"/> 상태 머신 (GDD §1). 단일 씬 안에서 상태만 바뀐다 (🔴 계약).
    ///
    /// <para>
    /// 이 클래스는 <b>전환 규칙과 timeScale만</b> 소유한다 (규칙 5 — 책임 집중 금지):
    /// 화면 표시는 <c>Luddite.UI.GameScreens</c>가, 런 초기화는 각 시스템이
    /// <see cref="GameEvents.RunStarted"/> 구독으로 스스로 한다. 여기에 시스템 참조를
    /// 늘리기 시작하면 규칙 5가 무너진다.
    /// </para>
    ///
    /// <para>
    /// 전환 API는 상태별로 유효성을 검사하고, 유효하지 않으면 경고 후 무시한다 —
    /// 디버그 메뉴·버튼 연타로 이상 전환이 들어와도 상태 머신이 깨지지 않게.
    /// </para>
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Tooltip("BossIntro 연출 길이(초) — 연출 타이밍이므로 SerializeField 허용 (GDD §1.1: 2초)")]
        [SerializeField] private float _bossIntroDuration = 2f;

        [Tooltip("PREDICTION FAILED 히트스톱 길이(초) — §10.3: 0.08~0.15")]
        [SerializeField] private float _predictionFailedHitStop = 0.12f;

        private GameState _state = GameState.Title;
        private float _bossIntroRemaining;
        private float _hitStopRemaining;

        public GameState State => _state;

        /// <summary>MajorSelect에서 확정된 전공. 무기 차별화(D6)·전공색이 읽는다.</summary>
        public Major SelectedMajor { get; private set; } = Major.LiberalArts;

        /// <summary>
        /// 첫 WaveInterval에서 확정되는 세부전공 (D7). None = 미선택 —
        /// UpgradePanel·SubMajorPanel이 이 값으로 첫 인터벌 여부를 판정한다.
        /// 세부전공별 탄막 차별화는 후속 작업 (지금은 저장만).
        /// </summary>
        public SubMajor SelectedSubMajor { get; private set; } = SubMajor.None;

        /// <summary>이번 런의 승패 — Result 화면 메시지 분기 (§1.4). 승리 = 보스 격파.</summary>
        public bool RunWon { get; private set; }

        private void OnEnable()
        {
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.PredictionFailed += OnPredictionFailed;
        }

        private void OnDisable()
        {
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.PredictionFailed -= OnPredictionFailed;
        }

        private void Start()
        {
            // 초기 상태 통지는 Start에서 — 구독자 전원이 OnEnable에서 구독을 마친 뒤여야 한다
            ApplyState(GameState.Title, force: true);
        }

        private void Update()
        {
            // ESC 토글 (§1.1): Combat 중에만 진입, Paused에서 다시 ESC로 복귀
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_state == GameState.Combat) ApplyState(GameState.Paused);
                else if (_state == GameState.Paused) ApplyState(GameState.Combat);
            }

            // BossIntro는 timeScale 0에서 돌므로 unscaled 시간으로 잰다
            if (_state == GameState.BossIntro)
            {
                _bossIntroRemaining -= Time.unscaledDeltaTime;
                if (_bossIntroRemaining <= 0f) ApplyState(GameState.Combat);
            }

            TickHitStop();
        }

        /// <summary>
        /// 히트스톱 (§10.3). timeScale의 소유자가 GameManager이므로 여기서 관리한다 —
        /// 별도 컴포넌트가 timeScale을 만지면 상태 전환(일시정지 등)과 복원 값이 충돌한다.
        /// </summary>
        public void BeginHitStop(float duration)
        {
            if (_state != GameState.Combat) return;   // 전투 밖에서는 시간이 이미 멈춰 있다

            _hitStopRemaining = Mathf.Max(_hitStopRemaining, duration);
            Time.timeScale = 0f;
        }

        private void TickHitStop()
        {
            if (_hitStopRemaining <= 0f) return;

            _hitStopRemaining -= Time.unscaledDeltaTime;
            if (_hitStopRemaining > 0f) return;

            // 히트스톱 도중 상태가 바뀌었다면 ApplyState가 이미 timeScale을 소유했다
            if (_state == GameState.Combat) Time.timeScale = 1f;
        }

        private void OnPredictionFailed(PredictionFailedReport report)
        {
            BeginHitStop(_predictionFailedHitStop);
        }

        // ── 전환 API (UI 버튼·WaveManager(D4)·보스(D5)가 호출) ──

        /// <summary>Title → MajorSelect. 타이틀의 [시작] 버튼.</summary>
        public void StartRun()
        {
            if (!GuardTransition(GameState.Title, nameof(StartRun))) return;
            ApplyState(GameState.MajorSelect);
        }

        /// <summary>
        /// MajorSelect → Combat. 전공 확정과 동시에 런이 시작된다 —
        /// <see cref="GameEvents.RunStarted"/>로 각 시스템(AIBrain 리셋, 체력 복원)이 초기화된다.
        /// </summary>
        public void SelectMajor(Major major)
        {
            if (!GuardTransition(GameState.MajorSelect, nameof(SelectMajor))) return;

            SelectedMajor = major;
            SelectedSubMajor = SubMajor.None;   // 새 런 — 첫 인터벌에서 다시 고른다
            RunWon = false;
            GameEvents.RaiseRunStarted();
            ApplyState(GameState.Combat);
        }

        /// <summary>
        /// 첫 WaveInterval의 세부전공 카드가 호출 (SubMajorPanel). 저장만 하고
        /// 전투 진행은 UI가 <see cref="ContinueToNextWave"/>로 이어간다 — 업그레이드 카드와 같은 흐름.
        /// </summary>
        public void SelectSubMajor(SubMajor subMajor)
        {
            if (!GuardTransition(GameState.WaveInterval, nameof(SelectSubMajor))) return;

            if (subMajor == SubMajor.None || SubMajorInfo.MajorOf(subMajor) != SelectedMajor)
            {
                Debug.LogWarning($"[GameManager] 세부전공 {subMajor}은(는) 전공 {SelectedMajor} 소속이 아님 — 무시");
                return;
            }

            if (SelectedSubMajor != SubMajor.None)
            {
                Debug.LogWarning($"[GameManager] 세부전공은 런당 1회만 선택 (현재: {SelectedSubMajor}) — 무시");
                return;
            }

            SelectedSubMajor = subMajor;
        }

        /// <summary>Combat → WaveInterval. TODO(D4): WaveManager가 웨이브 전멸 판정 직후 호출.</summary>
        public void BeginWaveInterval()
        {
            if (!GuardTransition(GameState.Combat, nameof(BeginWaveInterval))) return;
            ApplyState(GameState.WaveInterval);
        }

        /// <summary>WaveInterval → Combat. 패널의 [다음 웨이브] 버튼 (업그레이드 선택은 D4).</summary>
        public void ContinueToNextWave()
        {
            if (!GuardTransition(GameState.WaveInterval, nameof(ContinueToNextWave))) return;
            ApplyState(GameState.Combat);
        }

        /// <summary>웨이브 7 진입 연출 (§1.1: 2초 후 Combat 자동 복귀). TODO(D5): 보스 웨이브 진입에서 호출.</summary>
        public void BeginBossIntro()
        {
            // 웨이브 흐름상 WaveInterval에서 곧장 진입할 수도 있어 둘 다 허용한다
            if (_state != GameState.Combat && _state != GameState.WaveInterval)
            {
                WarnInvalid(nameof(BeginBossIntro));
                return;
            }

            ApplyState(GameState.BossIntro);
        }

        /// <summary>런 종료 → Result. 승리 = 보스 격파(D5), 패배 = 플레이어 사망 (§1.4).</summary>
        public void EndRun(bool won)
        {
            if (!GuardTransition(GameState.Combat, nameof(EndRun))) return;

            RunWon = won;
            ApplyState(GameState.Result);
        }

        /// <summary>Paused → Combat. 일시정지 오버레이의 [계속] 버튼.</summary>
        public void ResumeFromPause()
        {
            if (!GuardTransition(GameState.Paused, nameof(ResumeFromPause))) return;
            ApplyState(GameState.Combat);
        }

        /// <summary>Paused/Result → Title. 남은 전투 상태 정리는 TODO(D4): WaveManager 런 정리와 함께.</summary>
        public void ReturnToTitle()
        {
            if (_state != GameState.Paused && _state != GameState.Result)
            {
                WarnInvalid(nameof(ReturnToTitle));
                return;
            }

            ApplyState(GameState.Title);
        }

        // ── 내부 ──

        private void OnPlayerDied()
        {
            // Combat 밖(연출 중 등)의 사망 통보는 상태 머신을 흔들지 않게 무시한다
            if (_state != GameState.Combat) return;

            RunWon = false;
            ApplyState(GameState.Result);
        }

        private void ApplyState(GameState next, bool force = false)
        {
            if (!force && next == _state) return;

            GameState previous = _state;
            _state = next;

            // Combat만 시간이 흐른다. WaveInterval·Paused의 완전 일시정지(§1.1)를 포함해
            // 비전투 상태 전부에 같은 규칙을 적용한다 — 연출·UI는 unscaled 시간을 쓸 것.
            // 상태 전환은 진행 중인 히트스톱을 취소하고 timeScale 소유권을 되찾는다.
            _hitStopRemaining = 0f;
            Time.timeScale = next == GameState.Combat ? 1f : 0f;

            if (next == GameState.BossIntro) _bossIntroRemaining = _bossIntroDuration;

            Debug.Log($"[GameManager] {previous} → {next}");
            GameEvents.RaiseGameStateChanged(previous, next);
        }

        private bool GuardTransition(GameState required, string apiName)
        {
            if (_state == required) return true;

            WarnInvalid(apiName);
            return false;
        }

        private void WarnInvalid(string apiName)
        {
            Debug.LogWarning($"[GameManager] {apiName}은(는) 현재 상태({_state})에서 유효하지 않음 — 무시");
        }
    }
}
