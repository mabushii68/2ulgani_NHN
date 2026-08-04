namespace Luddite.AIBrain
{
    /// <summary>
    /// 회피 방향. <b>🔴 계약: LEFT/RIGHT 2분류만 존재한다</b> (GDD §7.2).
    /// 8방향 회귀로 확장하는 것은 계약 변경이며 사람 승인이 필요하다 —
    /// 2분류여야 표본이 빨리 쌓여 "AI가 나를 읽었다"는 체감이 웨이브 단위로 나온다.
    /// 기준축은 <b>탄환 진행 방향</b>이다 (플레이어 기준이 아니다, §7.1).
    /// </summary>
    public enum DodgeDirection
    {
        Left,
        Right
    }
}
