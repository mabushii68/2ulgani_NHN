using UnityEngine;

namespace Luddite.Combat
{
    /// <summary>
    /// 피격 가능한 대상. 투사체·접촉 데미지는 구체 타입을 몰라도 이 인터페이스만으로 동작한다.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>소속 진영. 투사체는 자기 표적 진영만 때린다.</summary>
        Faction Faction { get; }

        /// <summary>
        /// 지금 데미지를 받을 수 있는지. 사망했거나 무적이면 false
        /// (플레이어 피격 후 i-frame, 적 스폰 텔레그래프).
        /// "살아 있는가"와는 다른 질문이다 — 살아 있어도 무적이면 false다.
        /// 판정 주체가 먼저 확인하고, 구현 쪽도 <see cref="TakeDamage"/>에서 한 번 더 방어한다.
        /// </summary>
        bool CanBeDamaged { get; }

        /// <param name="amount">데미지량</param>
        /// <param name="hitDirection">
        /// 가해자(탄 등)의 진행 방향(정규화). 넉백 방향의 기준 (GDD §3.2).
        /// 넉백 거리는 맞는 쪽이 결정한다 — 플레이어는 넉백 없음(🔴 계약).
        /// </param>
        void TakeDamage(float amount, Vector2 hitDirection);
    }
}
