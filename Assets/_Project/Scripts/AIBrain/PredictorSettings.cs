namespace Luddite.AIBrain
{
    /// <summary>
    /// <see cref="DodgePredictor"/> 튜닝값 (GDD §7.2, §7.3).
    /// UnityEngine에 의존하지 않는 평범한 값 묶음이다 — 어댑터가 <c>PredictorConfigSO</c>에서 읽어 만든다.
    /// Laplace 가상 카운트 (1,1)은 여기 없다: 🔴 계약으로 고정이라 <see cref="DodgePredictor"/>의 const다.
    /// </summary>
    public readonly struct PredictorSettings
    {
        /// <summary>웨이브 종료 시 관측 카운트에 곱하는 계수. 기본 0.8 (튜닝 범위 0.7~0.9).</summary>
        public readonly float DecayFactor;

        /// <summary>HIGH CONFIDENCE 최소 표본 수. 기본 8. 감쇠 후 관측 카운트 합 기준.</summary>
        public readonly float MinValidSamples;

        /// <summary>HIGH CONFIDENCE 최소 우세 확률. 기본 0.70.</summary>
        public readonly float MinDominantProbability;

        public PredictorSettings(float decayFactor, float minValidSamples, float minDominantProbability)
        {
            DecayFactor = decayFactor;
            MinValidSamples = minValidSamples;
            MinDominantProbability = minDominantProbability;
        }

        /// <summary>GDD 기본값. Unity 없이 테스트할 때의 출발점.</summary>
        public static PredictorSettings Default => new PredictorSettings(0.8f, 8f, 0.70f);
    }
}
