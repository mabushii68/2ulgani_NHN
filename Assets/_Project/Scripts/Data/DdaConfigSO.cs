using UnityEngine;
using Luddite.Enemies;

namespace Luddite.Data
{
    /// <summary>다음 웨이브에 적용될 매크로 DDA 판정 (§6.3).</summary>
    public enum DdaDecision
    {
        None,

        /// <summary>원거리형 플레이어(직전 웨이브 평균 거리 > 6) → 챗봇 일부를 돌진형(코딩봇)으로.</summary>
        MoreRushUnits,

        /// <summary>근접형 플레이어(< 3.5) → 챗봇 일부를 원거리형(그림봇)으로.</summary>
        MoreRangedUnits,
    }

    /// <summary>
    /// 매크로 DDA 수치 (GDD §6.3 — MVP 확정: Spawn Budget ❌, 고정 구성표 + 단순 치환만).
    /// <b>상한 30% 고정</b> — DDA가 구성표를 뒤엎지 않는다는 것이 §6.3의 핵심 제약이다.
    /// </summary>
    [CreateAssetMenu(fileName = "DdaConfig", menuName = "Luddite/DDA Config")]
    public class DdaConfigSO : ScriptableObject
    {
        [Tooltip("직전 웨이브 평균 교전 거리가 이 값을 넘으면 원거리형으로 판정. §6.3 = 6")]
        [SerializeField] private float _farDistanceThreshold = 6f;

        [Tooltip("이 값 미만이면 근접형으로 판정. §6.3 = 3.5")]
        [SerializeField] private float _nearDistanceThreshold = 3.5f;

        [Range(0f, 0.3f)]
        [Tooltip("치환 비율. §6.3: 상한 30% 고정 — 그래서 슬라이더 최대가 0.3이다")]
        [SerializeField] private float _replacementRatio = 0.3f;

        [Tooltip("적용 시작 웨이브. §6.3 = 4 (웨이브 3의 데이터로 첫 판정)")]
        [SerializeField] private int _activeFromWave = 4;

        [Header("치환 대상·결과 프리팹")]
        [Tooltip("치환의 원본 — 구성표의 챗봇 엔트리만 치환된다 (엘리트 변형은 별개 애셋이라 무관)")]
        [SerializeField] private EnemyBase _chatbotPrefab;

        [Tooltip("원거리형 판정 시 투입 (코딩봇)")]
        [SerializeField] private EnemyBase _rushReplacement;

        [Tooltip("근접형 판정 시 투입 (그림봇)")]
        [SerializeField] private EnemyBase _rangedReplacement;

        public float FarDistanceThreshold => _farDistanceThreshold;
        public float NearDistanceThreshold => _nearDistanceThreshold;
        public float ReplacementRatio => _replacementRatio;
        public int ActiveFromWave => _activeFromWave;
        public EnemyBase ChatbotPrefab => _chatbotPrefab;
        public EnemyBase RushReplacement => _rushReplacement;
        public EnemyBase RangedReplacement => _rangedReplacement;
    }
}
