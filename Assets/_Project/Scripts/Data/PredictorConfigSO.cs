using UnityEngine;
using Luddite.AIBrain;

namespace Luddite.Data
{
    /// <summary>
    /// AIBrain 튜닝값 (GDD §7.1~§7.3). 코드 하드코딩 금지 규칙(CLAUDE.md 규칙 2)에 따라 SO로 노출한다.
    ///
    /// <para>
    /// <b>주의</b>: 아래 4개는 🔴 계약값이다 — 트리거 TTI 0.5 / 판정 창 0.6 / 최소 변위 0.3 / 감쇠 0.8.
    /// 인스펙터에서 바꿀 수 있게 열어 둔 것은 규칙 2 때문이지만, <b>계약값 이탈은 사람 승인 사항</b>이라
    /// <see cref="OnValidate"/>가 경고를 남긴다. 경고가 보이면 의도한 변경인지 확인할 것.
    /// </para>
    ///
    /// <para>
    /// Laplace 가상 카운트 (1,1)은 여기에 없다 — 튜닝 대상이 아니라서
    /// <see cref="DodgePredictor"/>의 const로 못박았다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "PredictorConfig", menuName = "Luddite/Predictor Config")]
    public class PredictorConfigSO : ScriptableObject
    {
        private const float CONTRACT_TRIGGER_TTI = 0.5f;
        private const float CONTRACT_RESOLVE_WINDOW = 0.6f;
        private const float CONTRACT_MIN_LATERAL = 0.3f;
        private const float CONTRACT_DECAY = 0.8f;

        [Header("피격 위기 이벤트 (GDD §7.1) — 🔴 계약")]
        [Tooltip("🔴 계약: 적 탄환의 충돌 예상 시간(TTI)이 이 값 이내로 들어오면 트리거. 기본 0.5초")]
        [SerializeField] private float _triggerTimeToImpact = CONTRACT_TRIGGER_TTI;

        [Tooltip("🔴 계약: 트리거 후 이 시간 안에 판정을 확정한다. 기본 0.6초")]
        [SerializeField] private float _resolveWindow = CONTRACT_RESOLVE_WINDOW;

        [Tooltip("🔴 계약: 좌우 변위가 이 값 미달이면 학습 표본에서 제외(제자리·전후 이동). 기본 0.3유닛")]
        [SerializeField] private float _minLateralDisplacement = CONTRACT_MIN_LATERAL;

        [Header("위협 판정 보조 — GDD 미명시 (사람 확인 필요)")]
        [Tooltip("예상 근접거리가 이 값을 넘는 탄은 위협으로 보지 않는다. " +
                 "GDD에 없는 값이며, 없으면 화면 반대편으로 지나가는 탄까지 TTI 조건에 걸려 표본이 오염된다")]
        [SerializeField] private float _threatMissRadius = 2f;

        [Header("확률 모델 (GDD §7.2)")]
        [Tooltip("🔴 계약: 웨이브 종료 시 관측 카운트에만 곱하는 감쇠 계수. 기본 0.8 (튜닝 범위 0.7~0.9). " +
                 "§7.6 학습 템포의 핵심 손잡이 — 작으면 AI가 금붕어가 되고 크면 못 잊는다")]
        [Range(0.5f, 1f)]
        [SerializeField] private float _decayFactor = CONTRACT_DECAY;

        [Header("신뢰도 이중 게이트 (GDD §7.3)")]
        [Tooltip("HIGH CONFIDENCE 최소 표본 수. 감쇠 후 관측 카운트 합 기준 (가상 카운트 제외). 기본 8")]
        [SerializeField] private float _minValidSamples = 8f;

        [Tooltip("HIGH CONFIDENCE 최소 우세 확률. 기본 0.70")]
        [Range(0.5f, 1f)]
        [SerializeField] private float _minDominantProbability = 0.70f;

        [Header("업그레이드 효과 (GDD §8)")]
        [Tooltip("「행동교정」: 관측 카운트에 곱하는 값. 기본 0.2 (80% 감쇠 → 신뢰도 사실상 리셋)")]
        [SerializeField] private float _behaviourCorrectionFactor = 0.2f;

        [Tooltip("「논문조작」: 우세 방향의 반대편에 주입하는 가짜 표본 수. 기본 8. " +
                 "가짜 표본은 관측 카운트로 취급되어 표본 수에 포함된다 (§8 세칙)")]
        [SerializeField] private float _dataFabricationSamples = 8f;

        public float BehaviourCorrectionFactor => _behaviourCorrectionFactor;
        public float DataFabricationSamples => _dataFabricationSamples;

        /// <summary>HIGH CONFIDENCE 최소 표본 수. HUD가 "AI MODEL: LEARNING..." 판정(§10.1)에 읽는다.</summary>
        public float MinValidSamples => _minValidSamples;

        /// <summary>순수 C# 예측기에 넘길 값 묶음으로 변환한다.</summary>
        public PredictorSettings ToPredictorSettings() =>
            new PredictorSettings(_decayFactor, _minValidSamples, _minDominantProbability);

        /// <summary>순수 C# 탐지기에 넘길 값 묶음으로 변환한다.</summary>
        public ThreatDetectionSettings ToDetectionSettings() =>
            new ThreatDetectionSettings(_triggerTimeToImpact, _resolveWindow,
                _minLateralDisplacement, _threatMissRadius);

        private void OnValidate()
        {
            WarnIfContractChanged(_triggerTimeToImpact, CONTRACT_TRIGGER_TTI, "트리거 TTI");
            WarnIfContractChanged(_resolveWindow, CONTRACT_RESOLVE_WINDOW, "판정 창");
            WarnIfContractChanged(_minLateralDisplacement, CONTRACT_MIN_LATERAL, "최소 좌우 변위");
            WarnIfContractChanged(_decayFactor, CONTRACT_DECAY, "감쇠 계수");
        }

        private void WarnIfContractChanged(float current, float contract, string label)
        {
            if (Mathf.Approximately(current, contract)) return;
            Debug.LogWarning(
                $"[PredictorConfigSO] 🔴 계약값 변경 감지 — {label}: {contract} → {current}. " +
                "GDD §7 계약이므로 사람 승인이 필요한 변경입니다. 의도한 것이 아니면 되돌리세요.", this);
        }
    }
}
