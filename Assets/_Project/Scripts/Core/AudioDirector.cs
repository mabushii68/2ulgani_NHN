using UnityEngine;

namespace Luddite.Core
{
    /// <summary>게임 SFX 식별자 (GDD §12 최소 세트 8종 + 보스 P2 전환).</summary>
    public enum GameSfx
    {
        PlayerShoot,
        PlayerHit,
        EnemyDeath,
        AiAnalyze,          // 인터벌 패널 등장음
        PredictionShot,     // 마젠타 조준음 (§12)
        PredictionFailed,   // 전용 글리치 (§12)
        WaveClear,
        UiButton,
        BossPhase,          // PATTERN: YOU 전환 (D7 추가)
    }

    /// <summary>
    /// 오디오 단독 소유자 (GDD §12). 이벤트 버스에서 잡히는 것은 스스로 구독하고,
    /// 버스에 없는 순간(발사·피격·격파·조준·버튼)은 각 소유 컴포넌트가
    /// <see cref="Play"/>를 1줄 호출한다 — <b>씬에 없으면 전부 무음 no-op</b> (폴백 안전).
    ///
    /// <para>소스는 전부 자체 생성 레트로 신스 (§12 1순위 — `Audio/Generator~/generate_sfx.py`,
    /// 결정론 재생성 가능). 라이선스 이슈 0 — CREDITS §3 기록.</para>
    ///
    /// <para>BGM은 Combat 진입 시 시작해 런 내내 유지(인터벌 포함 — timeScale 무관),
    /// Title/Result에서 정지. AudioSource 2개(SFX 원샷 / BGM 루프)뿐 — 풀링 불필요.</para>
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        [Header("SFX 클립 (빌더 Luddite/Setup/오디오 배선이 채운다)")]
        [SerializeField] private AudioClip _playerShoot;
        [SerializeField] private AudioClip _playerHit;
        [SerializeField] private AudioClip _enemyDeath;
        [SerializeField] private AudioClip _aiAnalyze;
        [SerializeField] private AudioClip _predictionShot;
        [SerializeField] private AudioClip _predictionFailed;
        [SerializeField] private AudioClip _waveClear;
        [SerializeField] private AudioClip _uiButton;
        [SerializeField] private AudioClip _bossPhase;

        [Header("BGM")]
        [SerializeField] private AudioClip _combatLoop;

        [Header("볼륨 — 연출값")]
        [Range(0f, 1f)] [SerializeField] private float _sfxVolume = 0.8f;
        [Range(0f, 1f)] [SerializeField] private float _bgmVolume = 0.4f;

        [Tooltip("PlayerShoot 최소 재생 간격(초) — 연사 업그레이드 스택 시 소리 겹침 완화")]
        [SerializeField] private float _shootSfxMinInterval = 0.08f;

        private static AudioDirector _instance;

        /// <summary>도메인 리로드 꺼짐 대비 정적 초기화 (프로젝트 관례).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance() => _instance = null;

        private AudioSource _sfxSource;
        private AudioSource _bgmSource;
        private float _lastShootTime = -1f;

        /// <summary>어디서든 1줄로 SFX 재생. AudioDirector가 씬에 없으면 조용히 무시된다.</summary>
        public static void Play(GameSfx sfx)
        {
            if (_instance != null) _instance.PlayInternal(sfx);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[AudioDirector] 중복 인스턴스 — 이번 것을 무시", this);
                enabled = false;
                return;
            }
            _instance = this;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.clip = _combatLoop;
            _bgmSource.volume = _bgmVolume;
        }

        private void OnEnable()
        {
            GameEvents.PredictionFailed += OnPredictionFailed;
            GameEvents.WaveEnded += OnWaveEnded;
            GameEvents.BossPhaseTwoStarted += OnBossPhaseTwo;
            GameEvents.GameStateChanged += OnGameStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.PredictionFailed -= OnPredictionFailed;
            GameEvents.WaveEnded -= OnWaveEnded;
            GameEvents.BossPhaseTwoStarted -= OnBossPhaseTwo;
            GameEvents.GameStateChanged -= OnGameStateChanged;
            if (_instance == this) _instance = null;
        }

        private void OnPredictionFailed(PredictionFailedReport report) => PlayInternal(GameSfx.PredictionFailed);

        private void OnWaveEnded(int waveNumber) => PlayInternal(GameSfx.WaveClear);

        private void OnBossPhaseTwo() => PlayInternal(GameSfx.BossPhase);

        private void OnGameStateChanged(GameState previous, GameState next)
        {
            // AIAnalyze = "패널 등장음" (§12) — 인터벌(상자 오픈 포함, 같은 상태 재사용) 진입 시
            if (next == GameState.WaveInterval) PlayInternal(GameSfx.AiAnalyze);

            // BGM: 런 중 유지(인터벌·일시정지·보스 인트로 포함), 런 밖(Title/Result)에서만 정지
            bool inRun = next == GameState.Combat || next == GameState.WaveInterval
                      || next == GameState.BossIntro || next == GameState.Paused;
            if (inRun && _bgmSource != null && _bgmSource.clip != null && !_bgmSource.isPlaying)
                _bgmSource.Play();
            else if (!inRun && _bgmSource != null && _bgmSource.isPlaying)
                _bgmSource.Stop();
        }

        private void PlayInternal(GameSfx sfx)
        {
            if (_sfxSource == null) return;

            if (sfx == GameSfx.PlayerShoot)
            {
                // unscaled 시간 — 히트스톱(timeScale 0) 중 발사 잔여가 겹치는 것을 막는 게 목적이라
                if (Time.unscaledTime - _lastShootTime < _shootSfxMinInterval) return;
                _lastShootTime = Time.unscaledTime;
            }

            AudioClip clip = ClipOf(sfx);
            if (clip != null) _sfxSource.PlayOneShot(clip, _sfxVolume);
        }

        private AudioClip ClipOf(GameSfx sfx)
        {
            switch (sfx)
            {
                case GameSfx.PlayerShoot: return _playerShoot;
                case GameSfx.PlayerHit: return _playerHit;
                case GameSfx.EnemyDeath: return _enemyDeath;
                case GameSfx.AiAnalyze: return _aiAnalyze;
                case GameSfx.PredictionShot: return _predictionShot;
                case GameSfx.PredictionFailed: return _predictionFailed;
                case GameSfx.WaveClear: return _waveClear;
                case GameSfx.UiButton: return _uiButton;
                case GameSfx.BossPhase: return _bossPhase;
                default: return null;
            }
        }
    }
}
