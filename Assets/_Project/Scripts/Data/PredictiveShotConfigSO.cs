using UnityEngine;

namespace Luddite.Data
{
    /// <summary>
    /// 예측탄 수치 (GDD §7.4 확정 명세). 엘리트와 보스(D5)가 공유한다.
    /// 조준 공식: <c>predictedTarget = playerPosition + predictedSideDir × AimOffset</c>
    /// </summary>
    [CreateAssetMenu(fileName = "PredictiveShotConfig", menuName = "Luddite/Predictive Shot Config")]
    public class PredictiveShotConfigSO : ScriptableObject
    {
        [Tooltip("예측 지점 오프셋(유닛). §7.4의 offset = 1.5유닛 (튜닝값)")]
        [SerializeField] private float _aimOffset = 1.5f;

        [Tooltip("1단계 텔레그래프(초): 마젠타 조준선 + 지점 마커를 보여 주는 시간. §7.4 = 0.35초")]
        [SerializeField] private float _telegraphDuration = 0.35f;

        [Tooltip("공격 N회당 예측탄 1회. §7.4 = 2 — 전탄 예측이면 100% 회피 가능해져 역으로 쉬워진다")]
        [SerializeField] private int _attacksPerPredictive = 2;

        public float AimOffset => _aimOffset;
        public float TelegraphDuration => _telegraphDuration;
        public int AttacksPerPredictive => _attacksPerPredictive;

        private void OnValidate()
        {
            if (_attacksPerPredictive < 2)
                Debug.LogWarning("[PredictiveShotConfig] 공격당 예측탄 빈도가 2 미만 — §7.4는 '전탄 예측 금지'를 명시한다 (2회당 1회)", this);
            if (_telegraphDuration <= 0f)
                Debug.LogWarning("[PredictiveShotConfig] 텔레그래프 0 이하 — 예측탄은 읽을 수 있어야 심리전이 성립한다 (§7.4)", this);
        }
    }
}
