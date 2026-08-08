using UnityEngine;
using Luddite.Enemies;

namespace Luddite.Data
{
    /// <summary>
    /// 보스(거대 LLM) 패턴 수치 (GDD §9). 기본 생존·이동 수치는 EnemyStatsSO(보스 인스턴스)가 갖고,
    /// 여기는 패턴 전용이다. §9가 확정한 것은 구조(3패턴 순환·1초 텔레그래프·25%마다 소환 3·60% 전환)이고
    /// <b>패턴별 데미지·탄수는 미명시라 초안 밸런스</b> — 기획 검토 대상.
    /// </summary>
    [CreateAssetMenu(fileName = "BossConfig", menuName = "Luddite/Boss Config")]
    public class BossConfigSO : ScriptableObject
    {
        [Header("패턴 공통 (§9 확정)")]
        [Tooltip("패턴 텔레그래프(초). §9 = 1초")]
        [SerializeField] private float _patternTelegraph = 1f;

        [Tooltip("패턴 실행 후 다음 텔레그래프까지 간격(초)")]
        [SerializeField] private float _patternCooldown = 2f;

        [Tooltip("이 거리까지 접근 후 멈춰서 패턴을 돈다(유닛)")]
        [SerializeField] private float _holdDistance = 7f;

        [Header("문과 — 관통 장탄")]
        [SerializeField] private int _pierceShotCount = 3;
        [SerializeField] private float _pierceSpreadAngle = 12f;
        [SerializeField] private float _pierceDamage = 10f;
        [SerializeField] private float _pierceSpeed = 6f;
        [SerializeField] private float _pierceDiameter = 0.6f;
        [SerializeField] private float _pierceLifetime = 5f;

        [Header("이과 — 조준 레이저 (히트스캔)")]
        [SerializeField] private float _laserDamage = 15f;
        [Tooltip("판정 폭(유닛). 이 절반 거리 안에 플레이어가 있으면 명중")]
        [SerializeField] private float _laserWidth = 0.8f;
        [SerializeField] private float _laserRange = 30f;
        [Tooltip("발사 섬광 유지(초) — 연출값")]
        [SerializeField] private float _laserFlashDuration = 0.15f;

        [Header("예체능 — 회전 광역파 (원형 탄막)")]
        [SerializeField] private int _ringBulletCount = 12;
        [SerializeField] private float _ringDamage = 8f;
        [SerializeField] private float _ringSpeed = 5f;
        [SerializeField] private float _ringDiameter = 0.35f;
        [SerializeField] private float _ringLifetime = 4f;
        [Tooltip("발사마다 시작 각도를 이만큼 회전 — '회전' 광역파의 정체")]
        [SerializeField] private float _ringRotationStep = 15f;

        [Header("소환 (§9: P1에서 HP 25% 감소마다 챗봇 3)")]
        [SerializeField] private int _summonCount = 3;
        [Range(0.05f, 0.5f)]
        [SerializeField] private float _summonHpInterval = 0.25f;
        [SerializeField] private EnemyBase _summonPrefab;
        [Tooltip("보스 주변 소환 반경(유닛)")]
        [SerializeField] private float _summonRadius = 2.5f;

        [Header("P2 전환 (§9: HP 60%)")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float _phase2HpFraction = 0.6f;

        [Tooltip("전환 무적(초). §9 = 3초")]
        [SerializeField] private float _transitionInvulnerability = 3f;

        public float PatternTelegraph => _patternTelegraph;
        public float PatternCooldown => _patternCooldown;
        public float HoldDistance => _holdDistance;

        public int PierceShotCount => _pierceShotCount;
        public float PierceSpreadAngle => _pierceSpreadAngle;
        public float PierceDamage => _pierceDamage;
        public float PierceSpeed => _pierceSpeed;
        public float PierceDiameter => _pierceDiameter;
        public float PierceLifetime => _pierceLifetime;

        public float LaserDamage => _laserDamage;
        public float LaserWidth => _laserWidth;
        public float LaserRange => _laserRange;
        public float LaserFlashDuration => _laserFlashDuration;

        public int RingBulletCount => _ringBulletCount;
        public float RingDamage => _ringDamage;
        public float RingSpeed => _ringSpeed;
        public float RingDiameter => _ringDiameter;
        public float RingLifetime => _ringLifetime;
        public float RingRotationStep => _ringRotationStep;

        public int SummonCount => _summonCount;
        public float SummonHpInterval => _summonHpInterval;
        public EnemyBase SummonPrefab => _summonPrefab;
        public float SummonRadius => _summonRadius;

        public float Phase2HpFraction => _phase2HpFraction;
        public float TransitionInvulnerability => _transitionInvulnerability;
    }
}
