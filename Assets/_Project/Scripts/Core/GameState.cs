namespace Luddite.Core
{
    /// <summary>
    /// 단일 씬 <c>Main.unity</c> 안에서 전환되는 게임 상태 (GDD §1.1, 🔴 계약: 씬 분리 금지).
    /// 전환 규칙은 <see cref="GameManager"/>가 소유한다.
    /// </summary>
    public enum GameState
    {
        Title,
        MajorSelect,
        Combat,
        WaveInterval,
        BossIntro,
        Result,
        Paused,
    }
}
