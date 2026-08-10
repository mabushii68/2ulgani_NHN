using UnityEngine;
using Luddite.Core;
using Luddite.Data;
using Luddite.Player;

namespace Luddite.Combat
{
    /// <summary>
    /// D1~D7 3전공 공통 임시 무기 (GDD §4.1). 단일 직선 투사체, 관통 없음.
    /// 수치는 전부 <see cref="PlayerStatsSO"/>에서 읽는다 — 하드코딩 금지 (CLAUDE.md 규칙 2).
    /// 전공별 차이는 D8에 별도 IWeapon 구현으로 분화한다.
    /// </summary>
    public class BasicWeapon : MonoBehaviour, IWeapon
    {
        [SerializeField] private PlayerStatsSO _stats;
        [SerializeField] private Projectile _projectilePrefab;

        [Tooltip("발사 위치를 조준 방향으로 밀어내는 거리(유닛). 자기 히트박스와 겹치지 않게 하는 연출값")]
        [SerializeField] private float _muzzleOffset = 0.6f;

        [Tooltip("투사체 색. 플레이어 계열은 전공색 (GDD §10.4) — 마젠타 사용 금지")]
        [SerializeField] private Color _projectileColor = Color.white;

        private float _cooldownRemaining;

        /// <summary>업그레이드 배수 (§8). 없으면 배수 1로 동작한다 — 테스트 씬 호환.</summary>
        private PlayerUpgrades _upgrades;

        // ── 탄창 (D7 신규). 탄약 총량은 무한이고 탄창만 소모한다 — 자원 관리가 아니라
        //    "재장전 동안 회피에만 집중하는 구간"을 만드는 것이 목적이다.
        private int _ammo = -1;              // -1 = 미초기화. Awake에서 탄창 가득으로 채운다
        private float _reloadRemaining;

        public int MagazineSize => _stats != null ? _stats.MagazineSize : 0;
        public int AmmoRemaining => Mathf.Max(0, _ammo);
        public bool IsReloading => _reloadRemaining > 0f;

        public float ReloadProgress01
        {
            get
            {
                if (!IsReloading || _stats == null || _stats.ReloadDuration <= 0f) return 0f;
                return Mathf.Clamp01(1f - _reloadRemaining / _stats.ReloadDuration);
            }
        }

        public bool CanFire => _cooldownRemaining <= 0f && !IsReloading && _ammo != 0;

        private void Awake()
        {
            if (_stats == null) Debug.LogError($"[BasicWeapon] PlayerStatsSO 미지정 — {name}", this);
            if (_projectilePrefab == null) Debug.LogError($"[BasicWeapon] Projectile 프리팹 미지정 — {name}", this);
            _upgrades = GetComponentInParent<PlayerUpgrades>();
            RefillMagazine();
        }

        private void OnEnable()
        {
            // 런 재시작(RunStarted로 플레이어가 되살아나는 경로) 대비 — 재장전 중 상태로 부활하지 않게
            GameEvents.RunStarted += OnRunStarted;
        }

        private void OnDisable()
        {
            GameEvents.RunStarted -= OnRunStarted;
        }

        private void OnRunStarted() => RefillMagazine();

        private void RefillMagazine()
        {
            _ammo = _stats != null ? _stats.MagazineSize : 0;
            _reloadRemaining = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (_cooldownRemaining > 0f) _cooldownRemaining -= deltaTime;

            if (_reloadRemaining > 0f)
            {
                _reloadRemaining -= deltaTime;
                if (_reloadRemaining <= 0f) RefillMagazine();
                return;
            }

            // 탄창이 비면 입력과 무관하게 자동 재장전을 건다 (사람 요청: "전부 소모하면 자동으로 재장전")
            if (_ammo == 0 && _stats != null) _reloadRemaining = _stats.ReloadDuration;
        }

        public void Fire(Vector2 origin, Vector2 aimDirection)
        {
            if (!CanFire || _stats == null || _projectilePrefab == null) return;

            // 업그레이드 배수 (§8): 연사 +15% = 발사 빈도 배수 → 간격은 나눗셈
            float damageMultiplier = _upgrades != null ? _upgrades.DamageMultiplier : 1f;
            float fireRateMultiplier = _upgrades != null ? _upgrades.FireRateMultiplier : 1f;
            float sizeMultiplier = _upgrades != null ? _upgrades.ProjectileSizeMultiplier : 1f;

            _cooldownRemaining = _stats.FireInterval / Mathf.Max(fireRateMultiplier, 0.01f);
            if (_ammo > 0) _ammo--;                    // 0이 되면 다음 Tick이 자동 재장전을 건다
            AudioDirector.Play(GameSfx.PlayerShoot);   // 씬에 AudioDirector 없으면 무음 no-op

            Vector2 spawnPoint = origin + aimDirection * _muzzleOffset;
            Projectile shot = Instantiate(_projectilePrefab, spawnPoint, Quaternion.identity);

            SpriteRenderer spriteRenderer = shot.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) spriteRenderer.color = _projectileColor;

            shot.Launch(
                direction: aimDirection,
                speed: _stats.ProjectileSpeed,
                damage: _stats.ProjectileDamage * damageMultiplier,
                lifetime: _stats.ProjectileLifetime,
                diameter: _stats.ProjectileDiameter * sizeMultiplier,
                owner: transform.root,
                targetFaction: Faction.Enemy);
        }
    }
}
