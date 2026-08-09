using UnityEngine;

namespace Luddite.Data
{
    /// <summary>
    /// 웨이브 공통 규칙 (GDD §6.1 확정 + §2 스폰 위치). 개별 웨이브 구성은 <see cref="WaveConfigSO"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "WaveSystemConfig", menuName = "Luddite/Wave System Config")]
    public class WaveSystemConfigSO : ScriptableObject
    {
        [Tooltip("순차 스폰 간격(초). §6.1 = 1.5초")]
        [SerializeField] private float _spawnInterval = 1.5f;

        [Tooltip("동시 생존 상한. §6.1 = 10 (초과분 대기)")]
        [SerializeField] private int _maxAlive = 10;

        [Header("스폰 위치 (§2: 아레나 24×14, 가장자리 벽 안쪽 1유닛 링 랜덤)")]
        [SerializeField] private float _arenaHalfWidth = 12f;
        [SerializeField] private float _arenaHalfHeight = 7f;

        [Tooltip("벽에서 안쪽으로 띄우는 거리(유닛). §2 = 1")]
        [SerializeField] private float _spawnInset = 1f;

        public float SpawnInterval => _spawnInterval;
        public int MaxAlive => _maxAlive;

        public float RingHalfWidth => _arenaHalfWidth - _spawnInset;
        public float RingHalfHeight => _arenaHalfHeight - _spawnInset;

        /// <summary>벽 안쪽 여유(§2 = 1). 던전 모드가 방 규격에서 링을 계산할 때도 같은 값을 쓴다.</summary>
        public float SpawnInset => _spawnInset;
    }
}
