namespace Luddite.AIBrain
{
    /// <summary>
    /// 확정된 피격 위기 이벤트 1건의 결과 (GDD §7.1). 모든 학습·판정의 원시 단위.
    /// </summary>
    public readonly struct ThreatSample
    {
        /// <summary>탄에 맞았는지. true면 회피 실패.</summary>
        public readonly bool WasHit;

        /// <summary>그 탄이 예측탄이었는지. <c>WasHit &amp;&amp; WasPredictive</c>가 "예측 적중" 집계 조건.</summary>
        public readonly bool WasPredictive;

        /// <summary>
        /// 학습 표본으로 인정되는지. 좌우 변위가 0.3유닛에 못 미치면(제자리·전후 이동만) false —
        /// 🔴 계약. 이 표본을 학습에 넣으면 "안 피한 것"이 방향 편향으로 기록돼 모델이 오염된다.
        /// </summary>
        public readonly bool CountsAsLearningSample;

        /// <summary>탄환 진행 방향 기준 회피 방향. <see cref="CountsAsLearningSample"/>가 false면 의미 없음.</summary>
        public readonly DodgeDirection Direction;

        /// <summary>탄환 진행 방향 기준 좌우 변위(유닛). 양수 = 왼쪽. 디버그·튜닝용.</summary>
        public readonly float LateralDisplacement;

        /// <summary>트리거부터 판정까지 걸린 시간(초). 0.6초 창을 넘지 않는지 확인용.</summary>
        public readonly float ResolveDelay;

        public ThreatSample(bool wasHit, bool wasPredictive, bool countsAsLearningSample,
            DodgeDirection direction, float lateralDisplacement, float resolveDelay)
        {
            WasHit = wasHit;
            WasPredictive = wasPredictive;
            CountsAsLearningSample = countsAsLearningSample;
            Direction = direction;
            LateralDisplacement = lateralDisplacement;
            ResolveDelay = resolveDelay;
        }

        public override string ToString()
        {
            string outcome = WasHit ? "HIT" : "DODGE";
            string predictive = WasPredictive ? " predictive" : "";
            string counted = CountsAsLearningSample
                ? Direction.ToString().ToUpperInvariant()
                : "EXCLUDED(변위부족)";
            return $"{outcome}{predictive} {counted} lateral={LateralDisplacement:F2} t={ResolveDelay:F2}s";
        }
    }
}
