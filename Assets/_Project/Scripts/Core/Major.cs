namespace Luddite.Core
{
    /// <summary>
    /// 전공 3종 (GDD §4.1). 게임 플로우(MajorSelect)가 선택을 소유하므로 Core에 둔다.
    /// D1~7은 3전공 모두 공통 임시 투사체를 쓰고(색만 전공색), 최종 무기는 D6에
    /// <c>IWeapon</c> 컴포넌트 교체로 반영한다 (CLAUDE.md 규칙 6).
    /// </summary>
    public enum Major
    {
        /// <summary>문과 — 파랑 / "펜은 칼보다 강하다"</summary>
        LiberalArts,

        /// <summary>이과 — 초록 / "증명 끝. (Q.E.D.)"</summary>
        Science,

        /// <summary>예체능 — 노랑 / "영혼은 학습되지 않는다"</summary>
        Arts,
    }
}
