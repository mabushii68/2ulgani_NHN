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

        private readonly List<EnemyBase> _alive = new List<EnemyBase>(16);
        private readonly List<EnemyBase> _pending = new List<EnemyBase>(20);

        private int _waveIndex;
        private bool _waveActive;
        private bool _bossIntroStarted;
        private float _spawnTimer;

        /// <summary>현재 웨이브 번호 (1-based). HUD "WAVE n/7" 표기용.</summary>
        public int CurrentWaveNumber => Mathf.Min(_waveIndex + 1, TotalWaves);

        public int TotalWaves => _waves != null ? _waves.Length : 0;

        /// <summary>살아 있는 배정 적 수. 디버그용.</summary>
        public int AliveCount => _alive.Count;

        /// <summary>스폰 대기 수. 디버그용.</summary>
        public int PendingCount => _pending.Count;

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
        }

        private void OnGameStateChanged(GameState previous, GameState next)
        {
            // Combat 진입이 웨이브 시작 신호다. 일시정지 복귀(_waveActive 유지)는 건드리지 않는다
            if (next == GameState.Combat && !_waveActive) BeginWave();
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

            if (config.IsBossWave)
            {
                if (!_bossIntroStarted)
                {
                    _bossIntroStarted = true;
                    _gameManager.BeginBossIntro();   // §1.1: 2초 연출 후 Combat 복귀 → 다시 여기로
                    return;
                }

                // TODO(D5): 보스 스폰 + P1/P2 페이즈. 지금은 루프 검증을 위한 임시 승리
                Debug.Log("[WaveManager] 웨이브 7(보스)은 D5 예정 — 임시 승리 처리");
                _gameManager.EndRun(won: true);
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
            _waveIndex++;

            Debug.Log($"[WaveManager] 웨이브 {cleared} 전멸 — 감쇠 적용 후 인터벌 진입");
            GameEvents.RaiseWaveEnded(cleared);      // AIBrain 감쇠 (§7.2)
            _gameManager.BeginWaveInterval();        // 학습 패널 + 업그레이드 (§1.1)
        }

        /// <summary>§2: 아레나 가장자리, 벽 안쪽 1유닛 링 위의 랜덤 점.</summary>
        private Vector2 RandomEdgePosition()
        {
            float w = _systemConfig.RingHalfWidth;
            float h = _systemConfig.RingHalfHeight;

            float horizontal = 2f * w;
            float vertical = 2f * h;
            float t = Random.value * (horizontal + vertical) * 2f;

            if (t < horizontal) return new Vector2(-w + t, -h);                              // 아래변
            t -= horizontal;
            if (t < horizontal) return new Vector2(-w + t, h);                               // 위변
            t -= horizontal;
            if (t < vertical) return new Vector2(-w, -h + t);                                // 왼변
            t -= vertical;
            return new Vector2(w, -h + t);                                                   // 오른변
        }

        private void DespawnAll()
        {
            for (int i = 0; i < _alive.Count; i++)
            {
                if (_alive[i] != null) Destroy(_alive[i].gameObject);
            }
            _alive.Clear();
            _pending.Clear();
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
