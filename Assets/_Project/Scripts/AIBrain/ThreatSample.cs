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

        /// <summary>예측탄이 노렸던 회피 방향. <see cref="WasPredictive"/>가 false면 의미 없음.</summary>
        public readonly DodgeDirection PredictedDirection;

        /// <summary>
        /// 역카운터 (🔴 §7.5 — 3조건 전부 충족 시 1회):
        /// ① HIGH 상태의 예측 (예측탄 발사 자체가 HIGH에서만 일어난다) ② 실제 예측탄
        /// ③ 예측의 <b>반대</b> 방향으로 회피 성공. 확률표와 반대로 움직인 것만으로는 집계하지 않는다 —
        /// 변위 미달(제자리 회피)도, 일반탄 회피도 아니다. "읽고 깨뜨린 순간"만.
        /// </summary>
        public bool IsCounterDodge =>
            WasPredictive && !WasHit && CountsAsLearningSample && Direction != PredictedDirection;

        public ThreatSample(bool wasHit, bool wasPredictive, bool countsAsLearningSample,
            DodgeDirection direction, float lateralDisplacement, float resolveDelay,
            DodgeDirection predictedDirection = DodgeDirection.Left)
        {
            WasHit = wasHit;
            WasPredictive = wasPredictive;
            CountsAsLearningSample = countsAsLearningSample;
            Direction = direction;
            LateralDisplacement = lateralDisplacement;
            ResolveDelay = resolveDelay;
            PredictedDirection = predictedDirection;
        }

        public override string ToString()
        {
            string outcome = WasHit ? "HIT" : "DODGE";
            string predictive = WasPredictive
                ? (IsCounterDodge ? " predictive COUNTER!" : " predictive")
                : "";
            string counted = CountsAsLearningSample
                ? Direction.ToString().ToUpperInvariant()
                : "EXCLUDED(변위부족)";
            return $"{outcome}{predictive} {counted} lateral={LateralDisplacement:F2} t={ResolveDelay:F2}s";
        }
    }
}
