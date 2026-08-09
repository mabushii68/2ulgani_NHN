using System.Collections.Generic;
using UnityEngine;
using Luddite.AIBrain;
using Luddite.Combat;
using Luddite.Data;
using Luddite.Enemies;
using Luddite.Player;

namespace Luddite.Core
{
    /// <summary>
    /// <c>AIBrain/</c>의 순수 C# 로직과 Unity 사이를 잇는 <b>유일한</b> 어댑터 (CLAUDE.md 규칙 3).
    ///
    /// <para>이 클래스가 하는 일은 딱 세 가지다:</para>
    /// <list type="number">
    /// <item>Unity 타입 → AIBrain 타입 변환 (<c>Vector2</c> → <see cref="Vec2"/>)</item>
    /// <item>적 탄환 목록과 피격 사실을 <see cref="ThreatEventTracker"/>에 전달</item>
    /// <item>확정된 표본을 <see cref="DodgePredictor"/>에 학습시키고, 결과를 읽기 전용으로 노출</item>
    /// </list>
    ///
    /// <para>
    /// <b>판단 로직을 여기에 쓰지 말 것.</b> 확률·게이트·TTI는 전부 <c>AIBrain/</c> 안에 있고,
    /// 그래야 Unity 없이 검증할 수 있다 (실제로 51건 자체 테스트가 그 위에서 돈다).
    /// </para>
    ///
    /// <para>
    /// UI는 이 클래스를 <b>읽기만</b> 한다 (규칙 7). 모델을 바꾸는 것은 업그레이드 전용 API
    /// (<see cref="ApplyBehaviourCorrection"/>, <see cref="ApplyDataFabrication"/>)와
    /// 웨이브 훅(<see cref="OnWaveEnded"/>)뿐이다.
    /// </para>
    /// </summary>
    public class AIBrainRunner : MonoBehaviour
    {
        [SerializeField] private PredictorConfigSO _config;

        [Tooltip("추적 대상. 비워 두면 Player 태그로 찾는다")]
        [SerializeField] private Transform _player;

        [Tooltip("표본이 확정될 때마다 콘솔에 학습 상태를 남긴다. HUD(D3)가 붙기 전까지의 확인 수단")]
        [SerializeField] private bool _logSamples = true;

        private DodgePredictor _predictor;
        private ThreatEventTracker _tracker;
        private PlayStyleProfiler _profiler;
        private PlayerController _playerController;

        /// <summary>매 프레임 재사용하는 버퍼. 프레임마다 새 리스트를 만들면 탄막에서 GC를 때린다.</summary>
        private readonly List<ThreatBullet> _bulletBuffer = new List<ThreatBullet>(64);

        /// <summary>프로파일러에 넘길 적 위치 버퍼 (재사용).</summary>
        private readonly List<Vec2> _enemyPositionBuffer = new List<Vec2>(16);

        private int _pendingHitProjectileId = ThreatEventTracker.NO_BULLET;

        /// <summary>
        /// 프로파일 좌표계의 원점. §6.4의 4분할(favoriteQuadrant)은 "아레나 중심 기준"인데
        /// 던전 체인은 y=−200에 있어 월드 원점 기준으로는 전 구역이 남쪽으로 오염된다 —
        /// <see cref="DungeonManager"/>가 방 진입마다 그 방의 중심을 넣는다. 폴백 아레나는 원점(기본값).
        /// 교전 거리·이동 히스토그램은 상대량이라 영향 없다.
        /// </summary>
        private Vector2 _profileOrigin;

        // ── 읽기 전용 노출 (HUD §10.1 / WaveInterval 패널 §10.2 / 결과 화면 §13) ──

        public bool IsReady => _predictor != null && _tracker != null;

        public DodgeDirection DominantDirection => _predictor?.DominantDirection ?? DodgeDirection.Left;
        public float DominantProbability => _predictor?.DominantProbability ?? 0.5f;
        public float ValidSamples => _predictor?.ValidSamples ?? 0f;
        public bool IsHighConfidence => _predictor != null && _predictor.IsHighConfidence;

        public float ProbabilityOf(DodgeDirection direction) =>
            _predictor?.ProbabilityOf(direction) ?? 0.5f;

        /// <summary>HIGH CONFIDENCE에 필요한 최소 표본 수. HUD의 "LEARNING..." 표기 기준 (§10.1).</summary>
        public float RequiredSamples => _config != null ? _config.MinValidSamples : 8f;

        // ── 플레이 스타일 프로파일 (§6.4) — DDA·결과 화면이 읽기 전용으로 소비 ──

        /// <summary>런 전체 평균 교전 거리.</summary>
        public float AverageEngageDistance => _profiler?.AverageEngageDistance ?? 0f;

        /// <summary>직전 웨이브 평균 교전 거리 — 매크로 DDA(§6.3)의 입력. 표본 없으면 음수.</summary>
        public float LastWaveAverageEngageDistance =>
            _profiler?.LastWaveAverageEngageDistance ?? PlayStyleProfiler.NO_SAMPLE;

        /// <summary>무빙샷 비율 0~1 (결과 화면 전용 — §6.4).</summary>
        public float MovingShotRatio => _profiler?.MovingShotRatio ?? 0f;

        /// <summary>선호 4분할 구역 (결과 화면·보스 P2).</summary>
        public Quadrant FavoriteQuadrant => _profiler?.FavoriteQuadrant ?? Quadrant.NW;

        public float QuadrantRatio(Quadrant quadrant) => _profiler?.QuadrantRatio(quadrant) ?? 0f;

        /// <summary>8방향 이동 히스토그램 (0=E 반시계). 결과 화면 전용.</summary>
        public float DirectionRatio(int index) => _profiler?.DirectionRatio(index) ?? 0f;

        /// <summary>지금 위기 이벤트 판정이 진행 중인지.</summary>
        public bool HasActiveThreat => _tracker != null && _tracker.HasActiveWatch;

        /// <summary>학습에 반영된 표본 누적 수 (감쇠와 무관한 원시 카운트). 결과 화면용.</summary>
        public int LearnedSampleCount { get; private set; }

        /// <summary>확정된 예측탄 위기 이벤트 수. 예측 적중률의 분모 (§13).</summary>
        public int PredictiveAttempts { get; private set; }

        /// <summary>예측탄에 맞은 횟수 = "예측 적중" (§7.1). 적중률의 분자.</summary>
        public int PredictiveHits { get; private set; }

        /// <summary>예측 적중률 0~1. 시도가 없으면 0.</summary>
        public float PredictionAccuracy =>
            PredictiveAttempts > 0 ? (float)PredictiveHits / PredictiveAttempts : 0f;

        /// <summary>역카운터 성공 횟수 (🔴 §7.5 — "읽고 깨뜨린 순간"). 결과 화면 §13.</summary>
        public int CounterDodgeCount { get; private set; }

        /// <summary>역카운터 성공률 0~1 = 역카운터 / 예측탄 위기 이벤트 수.</summary>
        public float CounterDodgeRate =>
            PredictiveAttempts > 0 ? (float)CounterDodgeCount / PredictiveAttempts : 0f;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError("[AIBrainRunner] PredictorConfigSO 미지정 — AIBrain 비활성", this);
                return;
            }

            _predictor = new DodgePredictor(_config.ToPredictorSettings());
            _tracker = new ThreatEventTracker(_config.ToDetectionSettings());
            _profiler = new PlayStyleProfiler();
        }

        private void OnEnable()
        {
            GameEvents.ProjectileHitPlayer += OnProjectileHitPlayer;
            GameEvents.RunStarted += ResetRun;
            GameEvents.WaveEnded += HandleWaveEnded;
        }

        private void OnDisable()
        {
            GameEvents.ProjectileHitPlayer -= OnProjectileHitPlayer;
            GameEvents.RunStarted -= ResetRun;
            GameEvents.WaveEnded -= HandleWaveEnded;
        }

        private void HandleWaveEnded(int waveNumber) => OnWaveEnded();

        private void Start()
        {
            if (_player != null) return;

            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) _player = found.transform;
            else Debug.LogError("[AIBrainRunner] Player 태그 오브젝트 없음 — 위기 이벤트 탐지 불가", this);
        }

        /// <summary>무빙샷·이동 히스토그램 입력원. 지연 캐시 — _player가 인스펙터 배선일 수도 있어서.</summary>
        private PlayerController PlayerControllerRef
        {
            get
            {
                if (_playerController == null && _player != null)
                    _playerController = _player.GetComponent<PlayerController>();
                return _playerController;
            }
        }

        /// <summary>
        /// 피격은 물리 단계(<c>OnTriggerEnter2D</c>)에서 통보되므로 여기서 기록만 하고,
        /// 다음 <see cref="Update"/>에서 소비한다. 한 프레임 지연은 0.6초 판정 창에 비해 무해하다.
        /// </summary>
        private void OnProjectileHitPlayer(int projectileInstanceId)
        {
            _pendingHitProjectileId = projectileInstanceId;
        }

        private void Update()
        {
            if (!IsReady || _player == null) return;

            CollectEnemyBullets();

            IReadOnlyList<ThreatSample> samples = _tracker.Tick(
                Time.deltaTime,
                ToVec2(_player.position),
                _bulletBuffer,
                _pendingHitProjectileId);

            _pendingHitProjectileId = ThreatEventTracker.NO_BULLET;

            for (int i = 0; i < samples.Count; i++) Consume(samples[i]);

            TickProfiler();
        }

        /// <summary>현재 방(아레나) 중심을 프로파일 좌표계의 원점으로 지정. 던전 전용 훅 — 폴백은 원점 유지.</summary>
        public void SetProfileOrigin(Vector2 origin) => _profileOrigin = origin;

        /// <summary>§6.4 프로파일 수집. timeScale 0(인터벌·일시정지)에서는 deltaTime이 0이라 자연히 멈춘다.</summary>
        private void TickProfiler()
        {
            _enemyPositionBuffer.Clear();
            IReadOnlyList<EnemyBase> enemies = EnemyBase.Active;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && enemies[i].IsAlive)
                    _enemyPositionBuffer.Add(ToVec2((Vector2)enemies[i].transform.position - _profileOrigin));
            }

            PlayerController controller = PlayerControllerRef;
            Vec2 moveInput = controller != null ? ToVec2(controller.MoveInput) : Vec2.Zero;
            bool isFiring = controller != null && controller.IsFiring;

            // 플레이어·적 모두 같은 원점을 빼므로 교전 거리(상대량)는 불변, 4분할만 방 중심 기준이 된다
            _profiler.Tick(Time.deltaTime, ToVec2((Vector2)_player.position - _profileOrigin),
                _enemyPositionBuffer, moveInput, isFiring);
        }

        private void CollectEnemyBullets()
        {
            _bulletBuffer.Clear();

            IReadOnlyList<Projectile> active = Projectile.Active;
            for (int i = 0; i < active.Count; i++)
            {
                Projectile shot = active[i];
                if (shot == null) continue;

                // 플레이어를 노리는 탄만 위협이다. 내가 쏜 탄은 회피 학습의 입력이 아니다
                if (shot.TargetFaction != Faction.Player) continue;

                _bulletBuffer.Add(new ThreatBullet(
                    shot.GetInstanceID(),
                    ToVec2(shot.Position),
                    ToVec2(shot.Velocity),
                    shot.IsPredictive,
                    shot.PredictedDirection));
            }
        }

        private void Consume(ThreatSample sample)
        {
            // §10.3의 "LEFT 82% → 64%" 표시를 위해 학습 반영 전의 우세 방향·확률을 캡처
            DodgeDirection dominantBefore = _predictor.DominantDirection;
            float probabilityBefore = _predictor.DominantProbability;

            if (sample.WasPredictive)
            {
                PredictiveAttempts++;
                if (sample.WasHit) PredictiveHits++;
                if (sample.IsCounterDodge) CounterDodgeCount++;   // §7.5 — 판정은 순수 C#이 한다
            }

            if (sample.CountsAsLearningSample)
            {
                _predictor.Observe(sample.Direction);
                LearnedSampleCount++;
            }

            // PREDICTION FAILED (§10.3): 예측탄을 피했다 — 학습 반영 후 같은 방향의 확률로 하락 폭을 보고
            if (sample.WasPredictive && !sample.WasHit)
            {
                GameEvents.RaisePredictionFailed(new PredictionFailedReport(
                    dominantBefore, probabilityBefore, _predictor.ProbabilityOf(dominantBefore)));
            }

            if (_logSamples) Debug.Log($"[AIBrain] {sample} → {_predictor}");
        }

        // ── 모델을 변경하는 유일한 경로들 (규칙 7: UI가 직접 만지지 못하게) ──

        /// <summary>
        /// 웨이브 종료 시 지수 감쇠 (§7.2). 🔴 관측 카운트만 줄어든다.
        /// <see cref="GameEvents.WaveEnded"/> 구독으로 호출된다 (디버그 메뉴도 직접 호출 가능).
        /// </summary>
        public void OnWaveEnded()
        {
            if (!IsReady) return;

            _predictor.ApplyWaveDecay();
            _profiler.OnWaveEnded();   // 직전 웨이브 평균 교전 거리 확정 (§6.3 DDA 입력)
            if (_logSamples) Debug.Log($"[AIBrain] 웨이브 감쇠 적용 → {_predictor} / " +
                                       $"직전 웨이브 평균 교전 거리 {_profiler.LastWaveAverageEngageDistance:F2}");
        }

        /// <summary>업그레이드 「행동교정」 (§8 #7): 관측 카운트 ×0.2 → 신뢰도 사실상 리셋.</summary>
        public void ApplyBehaviourCorrection()
        {
            if (!IsReady) return;

            _predictor.ScaleObservations(_config.BehaviourCorrectionFactor);
            Debug.Log($"[AIBrain] 행동교정 → {_predictor}");
        }

        /// <summary>업그레이드 「논문조작」 (§8 #8): 우세의 반대 방향에 가짜 표본 주입.</summary>
        /// <returns>주입된 방향 (툴팁·연출용)</returns>
        public DodgeDirection ApplyDataFabrication()
        {
            if (!IsReady) return DodgeDirection.Left;

            DodgeDirection injected = _predictor.InjectFakeSamples(_config.DataFabricationSamples);
            Debug.Log($"[AIBrain] 논문조작 → {injected} 방향에 가짜 표본 주입 / {_predictor}");
            return injected;
        }

        /// <summary>런 재시작 시 초기화. <see cref="GameEvents.RunStarted"/>(전공 확정 시점)가 호출한다.</summary>
        public void ResetRun()
        {
            if (!IsReady) return;

            _predictor.Reset();
            _tracker.Reset();
            _profiler.Reset();
            LearnedSampleCount = 0;
            PredictiveAttempts = 0;
            PredictiveHits = 0;
            CounterDodgeCount = 0;
        }

        /// <summary>디버그용 한 줄 상태. HUD가 붙기 전까지 이걸로 확인한다.</summary>
        public string DescribeState() =>
            IsReady
                ? $"{_predictor} | 학습표본={LearnedSampleCount} 예측적중={PredictiveHits}/{PredictiveAttempts} " +
                  $"역카운터={CounterDodgeCount} 추적중={HasActiveThreat} | 프로파일: 평균거리={AverageEngageDistance:F1} " +
                  $"(직전 웨이브 {LastWaveAverageEngageDistance:F1}) 무빙샷={MovingShotRatio:P0} 구역={FavoriteQuadrant}"
                : "AIBrain 미초기화 (PredictorConfigSO 확인)";

        private static Vec2 ToVec2(Vector2 v) => new Vec2(v.x, v.y);

        private static Vec2 ToVec2(Vector3 v) => new Vec2(v.x, v.y);
    }
}
