using UnityEngine;
using Luddite.Combat;
using Luddite.Data;

namespace Luddite.Enemies
{
    /// <summary>
    /// 적 탄환 발사기. 발사 타이밍은 갖지 않는다 — 언제 쏠지는 FSM이 결정한다.
    /// <see cref="IWeapon"/>을 구현하지 않는 이유: 그 인터페이스는 PlayerController와 무기의
    /// 강결합을 끊기 위한 것(CLAUDE.md 규칙 6)이고, 적은 FSM이 이미 타이밍을 소유하므로
    /// 쿨다운을 무기에 또 두면 이중 게이트가 된다.
    /// </summary>
    public class EnemyGun : MonoBehaviour
    {
        [SerializeField] private EnemyStatsSO _stats;
        [SerializeField] private Projectile _projectilePrefab;

        [Tooltip("발사 위치를 조준 방향으로 밀어내는 거리(유닛). 자기 히트박스와 겹치지 않게 하는 연출값")]
        [SerializeField] private float _muzzleOffset = 0.7f;

        [Tooltip("일반탄 색. 마젠타는 AI 예측탄 전용이므로 절대 사용 금지 (GDD §10.4 🔴 계약)")]
        [SerializeField] private Color _projectileColor = new Color(1f, 0.62f, 0.28f, 1f);

        private void Awake()
        {
            if (_stats == null) Debug.LogError($"[EnemyGun] EnemyStatsSO 미지정 — {name}", this);
            if (_projectilePrefab == null) Debug.LogError($"[EnemyGun] Projectile 프리팹 미지정 — {name}", this);
        }

        /// <summary>지정 방향으로 1발. 쿨다운 판단은 호출자(FSM) 책임.</summary>
        public void Fire(Vector2 aimDirection) => Fire(aimDirection, _projectileColor, markPredictive: false);

        /// <summary>
        /// 색·예측탄 여부를 지정하는 변형 — 엘리트·보스의 예측탄(§7.4)용.
        /// 마젠타 색은 예측탄일 때만 넘길 것 (🔴 §10.4).
        /// </summary>
        /// <returns>발사된 투사체. 실패 시 null — 호출자가 트레일 등 후처리를 붙일 수 있게 반환한다.</returns>
        public Projectile Fire(Vector2 aimDirection, Color projectileColor, bool markPredictive)
        {
            if (_stats == null || _projectilePrefab == null) return null;

            Vector2 origin = (Vector2)transform.position + aimDirection * _muzzleOffset;
            Projectile shot = Instantiate(_projectilePrefab, origin, Quaternion.identity);

            SpriteRenderer spriteRenderer = shot.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) spriteRenderer.color = projectileColor;

            shot.Launch(
                direction: aimDirection,
                speed: _stats.ProjectileSpeed,
                damage: _stats.ProjectileDamage,
                lifetime: _stats.ProjectileLifetime,
                diameter: _stats.ProjectileDiameter,
                owner: transform.root,
                targetFaction: Faction.Player);

            if (markPredictive) shot.MarkAsPredictive();
            return shot;
        }
    }
}
