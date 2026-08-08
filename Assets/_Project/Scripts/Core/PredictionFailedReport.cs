using Luddite.AIBrain;

namespace Luddite.Core
{
    /// <summary>
    /// 예측탄 회피 성공 1건의 보고 (GDD §10.3 연출 데이터).
    /// "LEFT 82% → 64%" — 학습 반영 <b>전후</b>의 같은 방향 확률을 담는다.
    /// </summary>
    public readonly struct PredictionFailedReport
    {
        /// <summary>학습 반영 직전의 우세 방향 (= AI가 예측에 쓰던 방향).</summary>
        public readonly DodgeDirection DominantBefore;

        /// <summary>학습 반영 전 그 방향의 확률.</summary>
        public readonly float ProbabilityBefore;

        /// <summary>학습 반영 후 같은 방향의 확률 — 하락 폭이 연출의 핵심.</summary>
        public readonly float ProbabilityAfter;

        public PredictionFailedReport(DodgeDirection dominantBefore,
            float probabilityBefore, float probabilityAfter)
        {
            DominantBefore = dominantBefore;
            ProbabilityBefore = probabilityBefore;
            ProbabilityAfter = probabilityAfter;
        }
    }
}
