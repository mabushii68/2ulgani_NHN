using UnityEngine;

namespace Luddite.Data
{
    /// <summary>업그레이드 효과 종류 (GDD §8 — 8종).</summary>
    public enum UpgradeEffect
    {
        DamagePercent,        // #1 논문 1저자
        FireRatePercent,      // #2 벼락치기
        MoveSpeedPercent,     // #3 수강신청 올클
        MaxHpFlat,            // #4 국가장학금 (+즉시 동량 회복)
        ProjectileSizePercent,// #5 스펙 부풀리기
        MajorMastery,         // #6 전공 심화 — D6 최종 무기 도입 시 편입
        BehaviourCorrection,  // #7 행동교정 (AI 상호작용)
        DataFabrication,      // #8 논문조작 (AI 상호작용)
    }

    /// <summary>
    /// 업그레이드 1종 (GDD §8 — 인스턴스 8개).
    /// 이름·툴팁은 한국어가 원문(§10.5 인간 세계 = 한국어)이고,
    /// 영문 필드는 한글 폰트 반입(D3 예정) 전까지의 표시용이다.
    /// </summary>
    [CreateAssetMenu(fileName = "Upgrade", menuName = "Luddite/Upgrade")]
    public class UpgradeSO : ScriptableObject
    {
        [SerializeField] private string _displayName = "이름";
        [SerializeField] private string _displayNameEn = "NAME";

        [TextArea]
        [SerializeField] private string _tooltip = "설명";
        [SerializeField] private string _tooltipEn = "DESCRIPTION";

        [SerializeField] private UpgradeEffect _effect;

        [Tooltip("효과 크기. 퍼센트류는 0.2 = +20%, MaxHpFlat은 25 = +25")]
        [SerializeField] private float _value;

        [Tooltip("스택 상한. 0 = 무제한 (§8: 행동교정·논문조작)")]
        [SerializeField] private int _maxStacks = 3;

        [Tooltip("이 웨이브(를 앞둔 인터벌)부터 추첨 풀 편입. §8: AI 상호작용 2종 = 3")]
        [SerializeField] private int _availableFromWave = 1;

        [Tooltip("추첨 풀 포함 여부. 전공 심화는 D6 최종 무기 도입 전까지 false")]
        [SerializeField] private bool _inPool = true;

        public string DisplayName => _displayName;
        public string DisplayNameEn => _displayNameEn;
        public string Tooltip => _tooltip;
        public string TooltipEn => _tooltipEn;
        public UpgradeEffect Effect => _effect;
        public float Value => _value;
        public int MaxStacks => _maxStacks;
        public int AvailableFromWave => _availableFromWave;
        public bool InPool => _inPool;
    }
}
