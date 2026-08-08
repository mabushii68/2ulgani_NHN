namespace Luddite.AIBrain
{
    /// <summary>
    /// 회피 예측기 — 온라인 조건부 확률 모델 (GDD §7.2, §7.3). 게임 정체성의 핵심.
    ///
    /// <para><b>🔴 계약</b></para>
    /// <list type="bullet">
    /// <item>LEFT/RIGHT 2분류만 (<see cref="DodgeDirection"/>)</item>
    /// <item>Laplace smoothing 가상 카운트 (1,1) <b>고정</b> — 감쇠 대상이 아니고 튜닝 대상도 아니다.
    ///       그래서 <see cref="VIRTUAL_COUNT_PER_SIDE"/>는 const이며 SO로 노출하지 않는다</item>
    /// <item>지수 감쇠는 <b>관측 카운트에만</b> 적용 (<see cref="ApplyWaveDecay"/>)</item>
    /// </list>
    ///
    /// <para>
    /// 감쇠로 관측 표본이 사라지면 확률이 자연히 50%로 회귀한다 —
    /// "AI가 신뢰를 잃는" 동작이 수식에서 공짜로 나오는 것이 이 설계의 핵심이다.
    /// </para>
    ///
    /// UnityEngine 의존 없음. 가짜 관측 시퀀스만으로 전 동작을 검증할 수 있다 (CLAUDE.md 규칙 3).
    /// </summary>
    public sealed class DodgePredictor
    {
        /// <summary>🔴 계약: Laplace 가상 카운트 (1,1) 고정. 튜닝 금지이므로 const.</summary>
        private const float VIRTUAL_COUNT_PER_SIDE = 1f;

        private readonly PredictorSettings _settings;

        /// <summary>감쇠 대상인 실제 관측 카운트. 정수가 아닌 이유는 감쇠로 소수가 되기 때문.</summary>
        private float _leftObservations;
        private float _rightObservations;

        public DodgePredictor(PredictorSettings settings)
        {
            _settings = settings;
        }

        public float LeftObservations => _leftObservations;
        public float RightObservations => _rightObservations;

        /// <summary>
        /// 신뢰도 게이트가 보는 표본 수 — 감쇠 후 <b>관측</b> 카운트의 합.
        /// 가상 카운트는 포함하지 않는다 (§7.3). 포함하면 표본 0에서도 2가 되어 게이트가 무의미해진다.
        /// </summary>
        public float ValidSamples => _leftObservations + _rightObservations;

        /// <summary>우세 방향. 정확히 50:50이면 <see cref="DodgeDirection.Left"/>를 반환하지만,
        /// 그 경우 확률이 0.5라 신뢰도 게이트를 통과하지 못하므로 예측탄으로 이어지지 않는다.</summary>
        public DodgeDirection DominantDirection =>
            ProbabilityOf(DodgeDirection.Left) >= ProbabilityOf(DodgeDirection.Right)
                ? DodgeDirection.Left
                : DodgeDirection.Right;

        public float DominantProbability => ProbabilityOf(DominantDirection);

        /// <summary>
        /// 신뢰도 이중 게이트 (§7.3). 둘 다 만족해야 HIGH — 하나라도 깨지면 즉시 일반탄으로 복귀한다.
        /// 표본 수 게이트만 있으면 50:50에서도 예측탄이 나가고, 확률 게이트만 있으면
        /// 표본 1개(확률 0.67)에서 성급하게 확신한다. 두 조건이 서로를 막는다.
        /// </summary>
        public bool IsHighConfidence =>
            ValidSamples >= _settings.MinValidSamples &&
            DominantProbability >= _settings.MinDominantProbability;

        /// <summary>
        /// P(direction) = (관측 + 1) / (관측합 + 2) — Laplace smoothing (§7.2).
        /// 표본이 없으면 정확히 0.5가 되어 "아직 아무것도 모른다"를 수식이 스스로 표현한다.
        /// </summary>
        public float ProbabilityOf(DodgeDirection direction)
        {
            float observed = direction == DodgeDirection.Left ? _leftObservations : _rightObservations;
            float numerator = observed + VIRTUAL_COUNT_PER_SIDE;
            float denominator = ValidSamples + VIRTUAL_COUNT_PER_SIDE * 2f;
            return numerator / denominator;
        }

        /// <summary>회피 이벤트 1건 학습 (§7.2).</summary>
        /// <param name="weight">
        /// 표본 가중치. 실제 회피는 1. 가짜 표본 주입(<see cref="InjectFakeSamples"/>)이 이 경로를 쓴다.
        /// </param>
        public void Observe(DodgeDirection direction, float weight = 1f)
        {
            if (weight <= 0f) return;

            if (direction == DodgeDirection.Left) _leftObservations += weight;
            else _rightObservations += weight;
        }

        /// <summary>
        /// 웨이브 종료 시 지수 감쇠 (§7.2). <b>관측 카운트만</b> 줄인다 — 🔴 계약.
        /// 이것이 §7.6 학습 템포의 핵심 손잡이다: 값이 작으면 AI가 금붕어가 되고, 크면 못 잊는다.
        /// </summary>
        public void ApplyWaveDecay()
        {
            _leftObservations *= _settings.DecayFactor;
            _rightObservations *= _settings.DecayFactor;
        }

        /// <summary>
        /// 업그레이드 「행동교정」 (GDD §8 #7): 관측 카운트 즉시 ×0.2 → 신뢰도 사실상 리셋.
        /// 감쇠와 같은 연산이지만 웨이브 종료와 무관하게 플레이어가 능동적으로 발동하는 것이라 API를 분리했다.
        /// </summary>
        public void ScaleObservations(float factor)
        {
            if (factor < 0f) factor = 0f;
            _leftObservations *= factor;
            _rightObservations *= factor;
        }

        /// <summary>
        /// 업그레이드 「논문조작」 (GDD §8 #8): 우세 회피의 <b>반대</b> 방향에 가짜 표본을 주입한다.
        /// <para>
        /// 세칙: 가짜 표본은 <b>관측 카운트로 취급</b>되어 <see cref="ValidSamples"/>에 포함된다.
        /// 포함하지 않으면 가짜 방향으로는 신뢰도가 영영 오르지 않아 업그레이드가 무의미해진다.
        /// 방향은 자동(우세의 반대)이며 선택 UI는 없다.
        /// </para>
        /// </summary>
        /// <returns>주입한 방향</returns>
        public DodgeDirection InjectFakeSamples(float count)
        {
            DodgeDirection opposite = DominantDirection == DodgeDirection.Left
                ? DodgeDirection.Right
                : DodgeDirection.Left;

            Observe(opposite, count);
            return opposite;
        }

        /// <summary>런 시작 시 초기화. 표본 0 = 확률 50:50 = LEARNING 상태.</summary>
        public void Reset()
        {
            _leftObservations = 0f;
            _rightObservations = 0f;
        }

        /// <summary>HUD·디버그용 한 줄 요약 (§10.1 미니 패널 형식에 대응).</summary>
        public override string ToString()
        {
            string badge = IsHighConfidence ? "HIGH" : "LOW";
            return $"DODGE {DominantDirection.ToString().ToUpperInvariant()} " +
                   $"{DominantProbability * 100f:F0}% ({badge}) " +
                   $"samples={ValidSamples:F2} [L={_leftObservations:F2} R={_rightObservations:F2}]";
        }
    }
}
