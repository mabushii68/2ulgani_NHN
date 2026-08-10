using UnityEngine;

namespace Luddite.Data
{
    /// <summary>
    /// 플레이어 밸런스 수치 (GDD §3.2, §3.3).
    /// 코드 하드코딩 금지 — 플레이어 관련 수치는 이 SO가 유일한 위치다 (CLAUDE.md 규칙 2).
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "Luddite/Player Stats")]
    public class PlayerStatsSO : ScriptableObject
    {
        [Header("생존")]
        [Tooltip("최대 체력")]
        [SerializeField] private float _maxHp = 100f;

        [Tooltip("피격 후 무적 시간(초). 깜빡임 연출 구간")]
        [SerializeField] private float _invincibleDuration = 0.5f;

        [Tooltip("원형 히트박스 지름(유닛)")]
        [SerializeField] private float _hitboxDiameter = 0.8f;

        [Header("이동")]
        [Tooltip("이동 속도(유닛/초). 대각 이동도 같은 속도가 되도록 입력을 정규화한다")]
        [SerializeField] private float _moveSpeed = 6f;

        [Header("임시 투사체 — D1~D7 3전공 공통 (GDD §4.1)")]
        [Tooltip("발당 데미지")]
        [SerializeField] private float _projectileDamage = 10f;

        [Tooltip("탄속(유닛/초)")]
        [SerializeField] private float _projectileSpeed = 14f;

        [Tooltip("연사 간격(초). 좌클릭 홀드 시 이 간격으로 자동 발사한다")]
        [SerializeField] private float _fireInterval = 0.2f;

        [Tooltip("투사체 수명(초). 초과하면 소멸")]
        [SerializeField] private float _projectileLifetime = 2f;

        [Tooltip("투사체 지름(유닛)")]
        [SerializeField] private float _projectileDiameter = 0.25f;

        [Header("탄창 (D7 신규 — 사람 요청. 탄약 총량은 무한, 탄창만 소모한다)")]
        [Tooltip("탄창 1개당 발수. 전부 소모하면 자동 재장전된다")]
        [SerializeField] private int _magazineSize = 30;

        [Tooltip("자동 재장전 소요 시간(초). 이 동안 발사 불가 — 회피에만 집중하는 구간이 생긴다")]
        [SerializeField] private float _reloadDuration = 1.2f;

        public float MaxHp => _maxHp;
        public float InvincibleDuration => _invincibleDuration;
        public float HitboxDiameter => _hitboxDiameter;
        public float HitboxRadius => _hitboxDiameter * 0.5f;

        public float MoveSpeed => _moveSpeed;

        public float ProjectileDamage => _projectileDamage;
        public float ProjectileSpeed => _projectileSpeed;
        public float FireInterval => _fireInterval;
        public float ProjectileLifetime => _projectileLifetime;
        public float ProjectileDiameter => _projectileDiameter;
        public float ProjectileRadius => _projectileDiameter * 0.5f;

        public int MagazineSize => Mathf.Max(1, _magazineSize);
        public float ReloadDuration => Mathf.Max(0f, _reloadDuration);
    }
}
