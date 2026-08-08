using System.Collections.Generic;
using UnityEngine;
using Luddite.Data;
using Luddite.Enemies;

namespace Luddite.Core
{
    /// <summary>
    /// 7웨이브 시스템 (GDD §6.1/§6.2).
    ///
    /// <para><b>🔴 계약: 웨이브 전멸형 종료.</b> 시간제 종료는 금지 — 시간제는 "버티기"를 최적해로
    /// 만들어 회피 데이터 공급을 오염시킨다. 종료 조건은 오직 배정 적 전멸이다.</para>
    ///
    /// <para>
    /// 스폰: 배정 수량을 1.5초 간격 순차 스폰, 동시 생존 상한 10 (초과분 대기),
    /// 위치는 아레나 가장자리 벽 안쪽 1유닛 링 랜덤 (§2). 수치는 전부 SO (§6.1 / 규칙 2).
    /// </para>
    ///
    /// <para>
    /// 상태 머신과의 관계: 전멸 → <see cref="GameEvents.WaveEnded"/>(AIBrain 감쇠 §7.2) →
    /// <see cref="GameManager.BeginWaveInterval"/>. 웨이브 7(보스)은 BossIntro만 태우고
    /// 임시 승리 처리한다 — TODO(D5): 보스 스폰으로 교체.
    /// </para>
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] private WaveSystemConfigSO _systemConfig;

        [Tooltip("웨이브 순서대로 7개 (§6.2)")]
        [SerializeField] private WaveConfigSO[] _waves;

        [SerializeField] private GameManager _gameManager;

        [Header("매크로 DDA (§6.3) — 비워 두면 비활성")]
        [SerializeField] private DdaConfigSO _ddaConfig;

        [Tooltip("직전 웨이브 평균 교전 거리의 출처")]
        [SerializeField] private AIBrainRunner _brain;

        private readonly List<EnemyBase> _alive = new List<EnemyBase>(16);
        private readonly List<EnemyBase> _pending = new List<EnemyBase>(20);

        private int _waveIndex;
        private bool _waveActive;
        private bool _bossIntroStarted;
        private float _spawnTimer;
        private DdaDecision _plannedAdjustment = DdaDecision.None;

        // ── 던전 모드 훅 (개정안 v1.1). 전부 기본값이 D4까지의 동작과 같다 — 🔴 폴백 보존 ──
        private Vector2 _spawnOrigin = Vector2.zero;
        private bool _externalWaveControl;

        /// <summary>
        /// 배정 적 전멸 시 발행. <b>구독자가 있으면 인터벌을 직접 열지 않는다</b> —
        /// 던전 모드에서는 <see cref="DungeonManager"/>가 문 개방·상자를 대신 처리하고,
        /// 인터벌 패널은 상자를 열어야 뜬다 (개정안 §4). 구독자가 없으면 D4 경로 그대로.
        /// </summary>
        public event System.Action<int> RoomCleared;

        /// <summary>스폰 링의 중심. 던전 모드에서 방 중심으로 옮긴다 (기본 원점 = 기존 아레나).</summary>
        public void SetSpawnOrigin(Vector2 origin) { _spawnOrigin = origin; }

        /// <summary>
        /// true면 <c>Combat</c> 진입만으로 웨이브를 시작하지 않는다 — 방 진입이 시작 신호가 된다.
        /// <b>BossIntro 복귀는 예외</b>다 (인트로가 웨이브 시작을 한 번 가로채므로 재개가 필요).
        /// </summary>
        public void SetExternalWaveControl(bool enabled) { _externalWaveControl = enabled; }

        /// <summary>던전 모드에서 방 진입 시 호출. 방↔웨이브 1:1을 여기서 확정한다.</summary>
        public void BeginWaveNow(int waveNumber)
        {
            if (_waveActive) return;
            if (_waves == null || _waves.Length == 0) return;
            _waveIndex = Mathf.Clamp(waveNumber - 1, 0, _waves.Length - 1);
            BeginWave();
        }

        /// <summary>현재 웨이브 번호 (1-based). HUD "WAVE n/7" 표기용.</summary>
        public int CurrentWaveNumber => Mathf.Min(_waveIndex + 1, TotalWaves);

        public int TotalWaves => _waves != null ? _waves.Length : 0;

        /// <summary>살아 있는 배정 적 수. 디버그용.</summary>
        public int AliveCount => _alive.Count;

        /// <summary>스폰 대기 수. 디버그용.</summary>
        public int PendingCount => _pending.Count;

        /// <summary>다음 웨이브에 적용될 DDA 판정 (§6.3). WaveInterval의 COUNTER PROTOCOL이 읽는다.</summary>
        public DdaDecision PlannedAdjustment => _plannedAdjustment;

        /// <summary>DDA 치환 비율 (표기용, 0~1).</summary>
        public float DdaRatio => _ddaConfig != null ? _ddaConfig.ReplacementRatio : 0f;

        private void Awake()
        {
            if (_systemConfig == null) Debug.LogError("[WaveManager] WaveSystemConfigSO 미지정", this);
            if (_waves == null || _waves.Length == 0) Debug.LogError("[WaveManager] 웨이브 구성 비어 있음", this);
            if (_gameManager == null) _gameManager = FindFirstObjectByType<GameManager>();
            if (_gameManager == null) Debug.LogError("[WaveManager] GameManager 없음 — 웨이브 전환 불가", this);
        }

        private void OnEnable()
        {
            GameEvents.RunStarted += OnRunStarted;
            GameEvents.GameStateChanged += OnGameStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.RunStarted -= OnRunStarted;
            GameEvents.GameStateChanged -= OnGameStateChanged;
        }

        private void OnRunStarted()
        {
            DespawnAll();
            _waveIndex = 0;
            _waveActive = false;
            _bossIntroStarted = false;
            _plannedAdjustment = DdaDecision.None;
        }

        private void OnGameStateChanged(GameState previous, GameState next)
        {
            // Combat 진입이 웨이브 시작 신호다. 일시정지 복귀(_waveActive 유지)는 건드리지 않는다
            if (next != GameState.Combat || _waveActive) return;

            // 던전 모드에서는 방 진입이 시작 신호다 (DungeonManager.BeginWaveNow).
            // 단 BossIntro 복귀는 반드시 자동 재개해야 한다 — 인트로가 시작을 한 번 가로챘기 때문에
            // 여기서 막으면 보스가 영영 스폰되지 않는다
            if (_externalWaveControl && previous != GameState.BossIntro) return;

            BeginWave();
        }

        private void BeginWave()
        {
            if (_waves == null || _systemConfig == null || _gameManager == null) return;

            if (_waveIndex >= _waves.Length)
            {
                Debug.LogWarning("[WaveManager] 구성표를 벗어난 웨이브 요청 — 승리 처리");
                _gameManager.EndRun(won: true);
                return;
            }

            WaveConfigSO config = _waves[_waveIndex];

            // 보스 웨이브는 스폰 전에 BossIntro 연출을 한 번 태운다 (§1.1: 2초 후 Combat 복귀 → 다시 여기로)
            if (config.IsBossWave && !_bossIntroStarted)
            {
                _bossIntroStarted = true;
                _gameManager.BeginBossIntro();
                return;
            }

            _pending.Clear();
            IReadOnlyList<WaveConfigSO.SpawnEntry> entries = config.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].EnemyPrefab == null)
                {
                    Debug.LogError($"[WaveManager] 웨이브 {CurrentWaveNumber} 엔트리 {i} 프리팹 누락", this);
                    continue;
                }
                for (int n = 0; n < entries[i].Count; n++) _pending.Add(entries[i].EnemyPrefab);
            }
            ApplyDdaAdjustment();   // §6.3: 셔플 전에 치환 — 수량 합은 불변, 구성만 최대 30% 이동
            Shuffle(_pending);   // 유닛 종류가 섞여 나오도록 — 구성표의 수량은 그대로다 (§6.2)

            _waveActive = true;
            _spawnTimer = 0f;    // 첫 스폰은 즉시
            Debug.Log($"[WaveManager] 웨이브 {CurrentWaveNumber}/{TotalWaves} 시작 — 배정 {_pending.Count}기");
        }

        private void Update()
        {
            if (!_waveActive) return;

            PruneDead();
            TickSpawn(Time.deltaTime);   // timeScale 0(일시정지·인터벌)이면 자연히 멈춘다

            if (_pending.Count == 0 && _alive.Count == 0) OnWaveCleared();
        }

        private void PruneDead()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] == null || !_alive[i].IsAlive) _alive.RemoveAt(i);
            }
        }

        private void TickSpawn(float deltaTime)
        {
            if (_pending.Count == 0 || _alive.Count >= _systemConfig.MaxAlive) return;

            _spawnTimer -= deltaTime;
            if (_spawnTimer > 0f) return;

            EnemyBase prefab = _pending[0];
            _pending.RemoveAt(0);

            EnemyBase spawned = Instantiate(prefab, RandomEdgePosition(), Quaternion.identity);
            _alive.Add(spawned);
            _spawnTimer = _systemConfig.SpawnInterval;
        }

        private void OnWaveCleared()
        {
            _waveActive = false;
            int cleared = CurrentWaveNumber;
            WaveConfigSO clearedConfig = _waves[_waveIndex];
            _waveIndex++;

            // 보스 격파 = 승리 (§1.4). 런이 끝나므로 감쇠를 적용하지 않는다 —
            // 결과 화면(§13)이 보여 줄 최종 학습 상태를 웨이브 감쇠로 왜곡하지 않기 위해.
            if (clearedConfig.IsBossWave)
            {
                Debug.Log("[WaveManager] 보스 격파 — 승리");
                _gameManager.EndRun(won: true);
                return;
            }

            Debug.Log($"[WaveManager] 웨이브 {cleared} 전멸 — 감쇠 적용 후 인터벌 진입");
            GameEvents.RaiseWaveEnded(cleared);      // AIBrain 감쇠 + 프로파일 스냅숏 (§7.2/§6.4)

            // WaveEnded 구독이 동기 실행되므로 이 시점의 직전 웨이브 평균은 방금 끝난 웨이브 것이다
            _plannedAdjustment = ComputeDdaDecision();
            if (_plannedAdjustment != DdaDecision.None)
                Debug.Log($"[WaveManager] DDA 판정: {_plannedAdjustment} (직전 평균 거리 " +
                          $"{_brain.LastWaveAverageEngageDistance:F2})");

            // 던전 모드: 인터벌을 여기서 열지 않는다. 문 개방 + 상자를 거쳐 상자 오픈 시 열린다 (개정안 §4).
            // 감쇠·프로파일 스냅숏·DDA 판정은 위에서 이미 끝났으므로 AIBrain 쪽 타이밍은 무변경이다
            if (RoomCleared != null) { RoomCleared(cleared); return; }

            _gameManager.BeginWaveInterval();        // 학습 패널 + 업그레이드 (§1.1)
        }

        /// <summary>§6.3 판정. 웨이브 4부터, 직전 웨이브 표본이 있을 때만.</summary>
        private DdaDecision ComputeDdaDecision()
        {
            if (_ddaConfig == null || _brain == null) return DdaDecision.None;
            if (CurrentWaveNumber < _ddaConfig.ActiveFromWave) return DdaDecision.None;

            float lastAverage = _brain.LastWaveAverageEngageDistance;
            if (lastAverage < 0f) return DdaDecision.None;   // 표본 없음

            if (lastAverage > _ddaConfig.FarDistanceThreshold) return DdaDecision.MoreRushUnits;
            if (lastAverage < _ddaConfig.NearDistanceThreshold) return DdaDecision.MoreRangedUnits;
            return DdaDecision.None;
        }

        /// <summary>
        /// §6.3 치환 실행: 대기 목록의 챗봇을 최대 30%(내림)까지 치환한다.
        /// 총수 불변 — DDA는 구성을 기울일 뿐 구성표를 뒤엎지 않는다.
        /// </summary>
        private void ApplyDdaAdjustment()
        {
            if (_plannedAdjustment == DdaDecision.None || _ddaConfig == null) return;

            EnemyBase chatbot = _ddaConfig.ChatbotPrefab;
            EnemyBase replacement = _plannedAdjustment == DdaDecision.MoreRushUnits
                ? _ddaConfig.RushReplacement
                : _ddaConfig.RangedReplacement;
            if (chatbot == null || replacement == null)
            {
                _plannedAdjustment = DdaDecision.None;
                return;
            }

            int chatbotCount = 0;
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i] == chatbot) chatbotCount++;
            }

            int replaceCount = Mathf.FloorToInt(chatbotCount * _ddaConfig.ReplacementRatio);
            int replaced = 0;
            for (int i = 0; i < _pending.Count && replaced < replaceCount; i++)
            {
                if (_pending[i] != chatbot) continue;
                _pending[i] = replacement;
                replaced++;
            }

            if (replaced > 0)
                Debug.Log($"[WaveManager] DDA 치환: 챗봇 {replaced}/{chatbotCount}기 → {replacement.name} " +
                          $"({_plannedAdjustment})");

            _plannedAdjustment = DdaDecision.None;   // 1회성 — 다음 판정은 다음 웨이브 종료 시
        }

        /// <summary>
        /// §2: 방 가장자리, 벽 안쪽 1유닛 링 위의 랜덤 점.
        /// <see cref="_spawnOrigin"/>만큼 평행이동한다 — 던전 모드에서 현재 방 중심으로 옮겨지고,
        /// 폴백(원점)에서는 D4까지와 완전히 같은 좌표가 나온다.
        /// </summary>
        private Vector2 RandomEdgePosition()
        {
            float w = _systemConfig.RingHalfWidth;
            float h = _systemConfig.RingHalfHeight;

            float horizontal = 2f * w;
            float vertical = 2f * h;
            float t = Random.value * (horizontal + vertical) * 2f;

            Vector2 local;
            if (t < horizontal) local = new Vector2(-w + t, -h);                             // 아래변
            else
            {
                t -= horizontal;
                if (t < horizontal) local = new Vector2(-w + t, h);                          // 위변
                else
                {
                    t -= horizontal;
                    if (t < vertical) local = new Vector2(-w, -h + t);                       // 왼변
                    else { t -= vertical; local = new Vector2(w, -h + t); }                  // 오른변
                }
            }
            return local + _spawnOrigin;
        }

        private void DespawnAll()
        {
            // 추적 목록이 아니라 레지스트리 전체를 비운다 — 보스가 직접 소환한 미니언(§9)은
            // WaveManager가 스폰하지 않아 추적 목록에 없기 때문. Destroy는 프레임 말 지연이라 순회 안전.
            IReadOnlyList<EnemyBase> active = EnemyBase.Active;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i] != null) Destroy(active[i].gameObject);
            }
            _alive.Clear();
            _pending.Clear();
        }

        /// <summary>디버그 전용 — 지정 웨이브로 점프 (보스 테스트용). 정식 게임 흐름에서 호출 금지.</summary>
        public void DebugJumpToWave(int waveNumber)
        {
            DespawnAll();
            _waveIndex = Mathf.Clamp(waveNumber - 1, 0, TotalWaves - 1);
            _waveActive = false;
            _bossIntroStarted = false;
            _plannedAdjustment = DdaDecision.None;
            Debug.Log($"[WaveManager] (디버그) 웨이브 {CurrentWaveNumber}로 점프");
            if (_gameManager != null && _gameManager.State == GameState.Combat) BeginWave();
        }

        private static void Shuffle(List<EnemyBase> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
