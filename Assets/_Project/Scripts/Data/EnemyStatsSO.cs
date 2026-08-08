using UnityEngine;

namespace Luddite.Data
{
    /// <summary>
    /// 적 유닛 수치 (GDD §5.1) + FSM 상태 전환 시간 (§5.2 — "모든 상태 전환 조건·시간은 SO 노출").
    /// 유닛별로 인스턴스를 하나씩 만든다: 챗봇 드론 / 그림봇 / 코딩봇 / 프리미엄 구독봇 / 거대 LLM.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyStats", menuName = "Luddite/Enemy Stats")]
    public class EnemyStatsSO : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("HUD·디버그 표기용 이름")]
        [SerializeField] private string _displayName = "챗봇 드론";

        [Header("생존")]
        [SerializeField] private float _maxHp = 30f;

        [Tooltip("스폰 후 등장 텔레그래프 시간(초). 이 동안 공격도 피격도 불가 (GDD §2)")]
        [SerializeField] private float _spawnTelegraphDuration = 0.5f;

        [Tooltip("피격 시 밀리는 거리(유닛). 적 넉백은 소량만 (GDD §3.2)")]
        [SerializeField] private float _knockbackDistance = 0.5f;

        [Header("이동")]
        [SerializeField] private float _moveSpeed = 2.5f;

        [Tooltip("원형 히트박스 지름(유닛)")]
        [SerializeField] private float _hitboxDiameter = 1f;

        [Header("공격")]
        [Tooltip("접촉 데미지. 모든 적 공통 8 (GDD §3.2)")]
        [SerializeField] private float _contactDamage = 8f;

        [Tooltip("사거리(유닛). 이 안에 들어오면 조준 시작")]
        [SerializeField] private float _attackRange = 8f;

        [Tooltip("조준 시간(초). 플레이어가 읽고 피할 수 있게 하는 텔레그래프 구간")]
        [SerializeField] private float _aimDuration = 0.3f;

        [Tooltip("발사 후 쿨다운(초). GDD §5.1의 '공격 간격'")]
        [SerializeField] private float _attackCooldown = 2f;

        [Header("그림봇 전용 (GDD §5.1/§5.2 — 다른 유닛은 무시)")]
        [Tooltip("유지 거리 하한(유닛). 이보다 가까우면 물러난다")]
        [SerializeField] private float _preferredRangeMin = 6f;

        [Tooltip("유지 거리 상한(유닛). 이보다 멀면 접근한다")]
        [SerializeField] private float _preferredRangeMax = 9f;

        [Tooltip("부채꼴 탄 수")]
        [SerializeField] private int _spreadShotCount = 3;

        [Tooltip("부채꼴 탄 사이 각도(도)")]
        [SerializeField] private float _spreadAngleStep = 30f;

        [Tooltip("발사 후 재배치 이동 시간(초)")]
        [SerializeField] private float _repositionDuration = 0.8f;

        [Header("코딩봇 전용 (GDD §5.1/§5.2 — 다른 유닛은 무시)")]
        [Tooltip("돌진 속도(유닛/초)")]
        [SerializeField] private float _dashSpeed = 10f;

        [Tooltip("돌진 지속 시간(초)")]
        [SerializeField] private float _dashDuration = 0.6f;

        [Tooltip("돌진 후 경직 시간(초)")]
        [SerializeField] private float _recoveryDuration = 0.8f;

        [Tooltip("돌진 중 접촉 데미지 (평시 접촉은 공통 8, §3.2)")]
        [SerializeField] private float _dashContactDamage = 12f;

        [Header("탄환")]
        [SerializeField] private float _projectileDamage = 8f;
        [SerializeField] private float _projectileSpeed = 6f;

        [Tooltip("탄 수명(초). 사거리 8 / 탄속 6이면 약 1.3초에 도달하므로 여유를 둔다")]
        [SerializeField] private float _projectileLifetime = 3f;

        [Tooltip("탄 지름(유닛)")]
        [SerializeField] private float _projectileDiameter = 0.3f;

        public string DisplayName => _displayName;

        public float MaxHp => _maxHp;
        public float SpawnTelegraphDuration => _spawnTelegraphDuration;
        public float KnockbackDistance => _knockbackDistance;

        public float MoveSpeed => _moveSpeed;
        public float HitboxDiameter => _hitboxDiameter;
        public float HitboxRadius => _hitboxDiameter * 0.5f;

        public float ContactDamage => _contactDamage;
        public float AttackRange => _attackRange;
        public float AimDuration => _aimDuration;
        public float AttackCooldown => _attackCooldown;

        public float PreferredRangeMin => _preferredRangeMin;
        public float PreferredRangeMax => _preferredRangeMax;
        public int SpreadShotCount => _spreadShotCount;
        public float SpreadAngleStep => _spreadAngleStep;
        public float RepositionDuration => _repositionDuration;

        public float DashSpeed => _dashSpeed;
        public float DashDuration => _dashDuration;
        public float RecoveryDuration => _recoveryDuration;
        public float DashContactDamage => _dashContactDamage;

        public float ProjectileDamage => _projectileDamage;
        public float ProjectileSpeed => _projectileSpeed;
        public float ProjectileLifetime => _projectileLifetime;
        public float ProjectileDiameter => _projectileDiameter;
        public float ProjectileRadius => _projectileDiameter * 0.5f;
    }
}
