using System.Collections.Generic;
using UnityEngine;
using Luddite.Enemies;

namespace Luddite.Data
{
    /// <summary>
    /// 웨이브 1개의 구성 (GDD §6.2 — 인스턴스 7개).
    /// 엘리트도 별도 항목이 아니라 <c>EliteDrone</c> 프리팹 엔트리로 넣는다.
    /// 웨이브 7은 <see cref="IsBossWave"/>만 켜고 엔트리를 비운다 — 보스 스폰은 D5.
    /// </summary>
    [CreateAssetMenu(fileName = "WaveConfig", menuName = "Luddite/Wave Config")]
    public class WaveConfigSO : ScriptableObject
    {
        [System.Serializable]
        public class SpawnEntry
        {
            [SerializeField] private EnemyBase _enemyPrefab;
            [SerializeField] private int _count = 1;

            public EnemyBase EnemyPrefab => _enemyPrefab;
            public int Count => _count;
        }

        [Tooltip("보스 웨이브(웨이브 7). 엔트리 대신 보스 연출·스폰 경로를 탄다 (D5)")]
        [SerializeField] private bool _isBossWave;

        [SerializeField] private SpawnEntry[] _entries = new SpawnEntry[0];

        public bool IsBossWave => _isBossWave;
        public IReadOnlyList<SpawnEntry> Entries => _entries;

        public int TotalCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _entries.Length; i++) total += _entries[i].Count;
                return total;
            }
        }
    }
}
