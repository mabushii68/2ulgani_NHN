using UnityEngine;
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

        public bool CanFire => _cooldownRemaining <= 0f;

        private void Awake()
        {
            if (_stats == null) Debug.LogError($"[BasicWeapon] PlayerStatsSO 미지정 — {name}", this);
            if (_projectilePrefab == null) Debug.LogError($"[BasicWeapon] Projectile 프리팹 미지정 — {name}", this);
            _upgrades = GetComponentInParent<PlayerUpgrades>();
        }

        public void Tick(float deltaTime)
        {
            if (_cooldownRemaining > 0f) _cooldownRemaining -= deltaTime;
        }

        public void Fire(Vector2 origin, Vector2 aimDirection)
        {
            if (!CanFire || _stats == null || _projectilePrefab == null) return;

            // 업그레이드 배수 (§8): 연사 +15% = 발사 빈도 배수 → 간격은 나눗셈
            float damageMultiplier = _upgrades != null ? _upgrades.DamageMultiplier : 1f;
            float fireRateMultiplier = _upgrades != null ? _upgrades.FireRateMultiplier : 1f;
            float sizeMultiplier = _upgrades != null ? _upgrades.ProjectileSizeMultiplier : 1f;

            _cooldownRemaining = _stats.FireInterval / Mathf.Max(fireRateMultiplier, 0.01f);

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
