using UnityEngine;

namespace Luddite.Combat
{
    /// <summary>
    /// 피격 가능한 대상. 투사체·접촉 데미지는 구체 타입을 몰라도 이 인터페이스만으로 동작한다.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>아직 살아 있는지. false면 데미지를 무시한다.</summary>
        bool IsAlive { get; }

        /// <param name="amount">데미지량</param>
        /// <param name="hitDirection">
        /// 가해자(탄 등)의 진행 방향(정규화). 넉백 방향의 기준 (GDD §3.2).
        /// 넉백 거리는 맞는 쪽이 결정한다 — 플레이어는 넉백 없음(🔴 계약).
        /// </param>
        void TakeDamage(float amount, Vector2 hitDirection);
    }
}
