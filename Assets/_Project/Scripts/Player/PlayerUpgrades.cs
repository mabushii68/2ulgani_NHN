using UnityEngine;
using Luddite.Core;

namespace Luddite.Player
{
    /// <summary>
    /// 업그레이드 누적 배수 보관소 (GDD §8). 효과의 <b>보관</b>만 담당하고,
    /// 추첨·적용 규칙은 <c>UpgradeManager</c>, 소비는 각 컴포넌트
    /// (BasicWeapon: 공격력·연사·크기 / PlayerController: 이속 / PlayerHealth: 최대 HP)가 한다.
    /// 퍼센트 스택은 가산이다 — +20% ×3스택 = +60% (배수 1.6).
    /// </summary>
    public class PlayerUpgrades : MonoBehaviour
    {
        public float DamageMultiplier { get; private set; } = 1f;
        public float FireRateMultiplier { get; private set; } = 1f;
        public float MoveSpeedMultiplier { get; private set; } = 1f;
        public float ProjectileSizeMultiplier { get; private set; } = 1f;
        public float BonusMaxHp { get; private set; }

        private void OnEnable() => GameEvents.RunStarted += ResetAll;

        private void OnDisable() => GameEvents.RunStarted -= ResetAll;

        public void AddDamagePercent(float fraction) => DamageMultiplier += fraction;

        public void AddFireRatePercent(float fraction) => FireRateMultiplier += fraction;

        public void AddMoveSpeedPercent(float fraction) => MoveSpeedMultiplier += fraction;

        public void AddProjectileSizePercent(float fraction) => ProjectileSizeMultiplier += fraction;

        public void AddMaxHp(float amount) => BonusMaxHp += amount;

        private void ResetAll()
        {
            DamageMultiplier = 1f;
            FireRateMultiplier = 1f;
            MoveSpeedMultiplier = 1f;
            ProjectileSizeMultiplier = 1f;
            BonusMaxHp = 0f;
        }
    }
}
