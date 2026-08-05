using System.Collections.Generic;
using UnityEngine;
using Luddite.Data;
using Luddite.Player;

namespace Luddite.Core
{
    /// <summary>
    /// 업그레이드 추첨·적용 (GDD §8).
    /// 규칙: 3택 1 / 한 추첨 안 중복 ❌ / 스택 ⭕ / 상한 도달 항목 추첨 제외 /
    /// AI 상호작용 2종(행동교정·논문조작)은 웨이브 3(을 앞둔 인터벌)부터 편입.
    ///
    /// <para>
    /// AI 모델 변경은 반드시 <see cref="AIBrainRunner"/>의 전용 API를 경유한다 (규칙 7) —
    /// UI는 이 매니저를 호출할 뿐 AIBrain을 직접 만지지 못한다.
    /// </para>
    /// </summary>
    public class UpgradeManager : MonoBehaviour
    {
        [Tooltip("전체 풀 (§8의 8종). 편입 여부·시점은 각 SO가 갖는다")]
        [SerializeField] private UpgradeSO[] _pool;

        [SerializeField] private PlayerUpgrades _playerUpgrades;
        [SerializeField] private PlayerHealth _playerHealth;
        [SerializeField] private AIBrainRunner _brain;
        [SerializeField] private WaveManager _waveManager;

        private readonly Dictionary<UpgradeSO, int> _stacks = new Dictionary<UpgradeSO, int>();
        private readonly List<UpgradeSO> _candidateScratch = new List<UpgradeSO>(8);

        private void Awake()
        {
            if (_pool == null || _pool.Length == 0) Debug.LogError("[UpgradeManager] 풀 비어 있음", this);
            if (_playerUpgrades == null || _playerHealth == null)
                Debug.LogError("[UpgradeManager] 플레이어 참조 누락", this);
            if (_brain == null) Debug.LogError("[UpgradeManager] AIBrainRunner 누락 — AI 상호작용 2종 불능", this);
        }

        private void OnEnable() => GameEvents.RunStarted += ResetStacks;

        private void OnDisable() => GameEvents.RunStarted -= ResetStacks;

        private void ResetStacks() => _stacks.Clear();

        public int StackOf(UpgradeSO upgrade) =>
            upgrade != null && _stacks.TryGetValue(upgrade, out int n) ? n : 0;

        /// <summary>
        /// 3택 후보 추첨. 편입 전·상한 도달 항목 제외, 한 추첨 안 중복 없음.
        /// 후보가 3 미만이면 있는 만큼만 반환한다 (극후반 전부 상한 도달 대비).
        /// </summary>
        public List<UpgradeSO> DrawChoices(int count)
        {
            _candidateScratch.Clear();
            int upcomingWave = _waveManager != null ? _waveManager.CurrentWaveNumber : 1;

            for (int i = 0; i < _pool.Length; i++)
            {
                UpgradeSO upgrade = _pool[i];
                if (upgrade == null || !upgrade.InPool) continue;
                if (upcomingWave < upgrade.AvailableFromWave) continue;
                if (upgrade.MaxStacks > 0 && StackOf(upgrade) >= upgrade.MaxStacks) continue;
                _candidateScratch.Add(upgrade);
            }

            List<UpgradeSO> result = new List<UpgradeSO>(count);
            while (result.Count < count && _candidateScratch.Count > 0)
            {
                int pick = Random.Range(0, _candidateScratch.Count);
                result.Add(_candidateScratch[pick]);
                _candidateScratch.RemoveAt(pick);   // 한 추첨 안 중복 금지 (§8)
            }
            return result;
        }

        public void Apply(UpgradeSO upgrade)
        {
            if (upgrade == null) return;

            _stacks[upgrade] = StackOf(upgrade) + 1;

            switch (upgrade.Effect)
            {
                case UpgradeEffect.DamagePercent:
                    _playerUpgrades.AddDamagePercent(upgrade.Value);
                    break;
                case UpgradeEffect.FireRatePercent:
                    _playerUpgrades.AddFireRatePercent(upgrade.Value);
                    break;
                case UpgradeEffect.MoveSpeedPercent:
                    _playerUpgrades.AddMoveSpeedPercent(upgrade.Value);
                    break;
                case UpgradeEffect.MaxHpFlat:
                    _playerUpgrades.AddMaxHp(upgrade.Value);
                    _playerHealth.Heal(upgrade.Value);   // §8 #4: 즉시 동량 회복
                    break;
                case UpgradeEffect.ProjectileSizePercent:
                    _playerUpgrades.AddProjectileSizePercent(upgrade.Value);
                    break;
                case UpgradeEffect.MajorMastery:
                    // TODO(D6 최종 무기): 문과 관통+1 / 이과 크리 20%(×2) / 예체능 범위+20%
                    Debug.LogWarning("[UpgradeManager] 전공 심화는 D6 최종 무기 도입 시 구현 — 현재 효과 없음");
                    break;
                case UpgradeEffect.BehaviourCorrection:
                    if (_brain != null) _brain.ApplyBehaviourCorrection();
                    break;
                case UpgradeEffect.DataFabrication:
                    if (_brain != null) _brain.ApplyDataFabrication();   // 방향 자동 = 우세의 반대 (§8 세칙)
                    break;
            }

            Debug.Log($"[UpgradeManager] 적용: {upgrade.DisplayName} (스택 {StackOf(upgrade)})");
        }
    }
}
