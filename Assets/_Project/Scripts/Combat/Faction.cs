namespace Luddite.Combat
{
    /// <summary>
    /// 진영. 투사체가 누구를 때릴지 판정하는 유일한 기준이다.
    /// 레이어 + Physics2D 충돌 매트릭스로 하는 것이 정석이지만, 레이어 추가는 ProjectSettings 변경
    /// (사람 승인 대상)이라 그때까지 이 enum으로 처리한다. TODO(레이어 정리) — ProjectileBlocker와 같은 사정.
    /// </summary>
    public enum Faction
    {
        Player,
        Enemy
    }
}
