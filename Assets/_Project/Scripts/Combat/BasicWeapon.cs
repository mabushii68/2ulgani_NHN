using UnityEngine;
using Luddite.Data;

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

        public bool CanFire => _cooldownRemaining <= 0f;

        private void Awake()
        {
            if (_stats == null) Debug.LogError($"[BasicWeapon] PlayerStatsSO 미지정 — {name}", this);
            if (_projectilePrefab == null) Debug.LogError($"[BasicWeapon] Projectile 프리팹 미지정 — {name}", this);
        }

        public void Tick(float deltaTime)
        {
            if (_cooldownRemaining > 0f) _cooldownRemaining -= deltaTime;
        }

        public void Fire(Vector2 origin, Vector2 aimDirection)
        {
            if (!CanFire || _stats == null || _projectilePrefab == null) return;

            // TODO(D5 업그레이드): 공격력 +20% / 연사 +15% 스택은 여기서 배수로 곱한다 (GDD §8)
            _cooldownRemaining = _stats.FireInterval;

            Vector2 spawnPoint = origin + aimDirection * _muzzleOffset;
            Projectile shot = Instantiate(_projectilePrefab, spawnPoint, Quaternion.identity);

            SpriteRenderer spriteRenderer = shot.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) spriteRenderer.color = _projectileColor;

            shot.Launch(
                direction: aimDirection,
                speed: _stats.ProjectileSpeed,
                damage: _stats.ProjectileDamage,
                lifetime: _stats.ProjectileLifetime,
                diameter: _stats.ProjectileDiameter,
                owner: transform.root,
                targetFaction: Faction.Enemy);
        }
    }
}
