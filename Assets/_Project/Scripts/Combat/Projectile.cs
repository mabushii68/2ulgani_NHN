using UnityEngine;

namespace Luddite.Combat
{
    /// <summary>
    /// 직선 투사체. 관통 없음 (GDD §3.2) — 첫 피격 대상에 데미지를 주고 소멸한다.
    /// <see cref="ProjectileBlocker"/>(벽)에 닿아도 소멸. 탄환끼리 상쇄하지 않는다.
    /// 발사자는 <see cref="Launch"/>로 수치를 주입한다 — 투사체는 수치를 스스로 갖지 않는다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class Projectile : MonoBehaviour
    {
        [Tooltip("스프라이트 원본 지름(유닛). 이 값을 기준으로 목표 지름에 맞는 스케일을 계산한다")]
        [SerializeField] private float _spriteBaseDiameter = 1f;

        private Rigidbody2D _body;
        private CircleCollider2D _hitbox;
        private Transform _ownerRoot;
        private Faction _targetFaction;
        private float _damage;
        private float _lifeRemaining;
        private bool _consumed;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _hitbox = GetComponent<CircleCollider2D>();

            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.gravityScale = 0f;
            _body.linearDamping = 0f;
            _body.freezeRotation = true;
            _hitbox.isTrigger = true;
        }

        /// <summary>발사 직후 1회 호출. 방향·속도·데미지·수명·크기·발사자·표적 진영을 주입한다.</summary>
        /// <param name="targetFaction">이 진영의 <see cref="IDamageable"/>만 때린다. 같은 편은 통과.</param>
        public void Launch(Vector2 direction, float speed, float damage, float lifetime, float diameter,
            Transform owner, Faction targetFaction)
        {
            _damage = damage;
            _lifeRemaining = lifetime;
            _ownerRoot = owner;
            _targetFaction = targetFaction;
            _consumed = false;

            // 콜라이더 반지름은 원본 기준으로 두고 스케일로 최종 크기를 맞춘다 (스프라이트와 히트박스 동시 반영)
            _hitbox.radius = _spriteBaseDiameter * 0.5f;
            float scale = diameter / Mathf.Max(_spriteBaseDiameter, Mathf.Epsilon);
            transform.localScale = new Vector3(scale, scale, 1f);

            transform.right = direction;
            _body.linearVelocity = direction * speed;
        }

        private void Update()
        {
            _lifeRemaining -= Time.deltaTime;
            if (_lifeRemaining <= 0f) Despawn();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_consumed) return;

            // 발사자 자신은 무시 (총구 오프셋만으로는 프레임에 따라 겹칠 수 있음)
            if (_ownerRoot != null && other.transform.IsChildOf(_ownerRoot)) return;

            if (other.GetComponentInParent<ProjectileBlocker>() != null)
            {
                Despawn();
                return;
            }

            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null) return;

            // 같은 편은 통과 — 탄이 소멸하지도 않는다 (아군 오사 없음)
            if (target.Faction != _targetFaction) return;

            // 무적·사망 상태는 탄을 소모하지 않고 통과시킨다.
            // 소모시키면 스폰 텔레그래프 중인 적이 방패가 되고, 무적 중인 플레이어가 탄을 지워버린다.
            if (!target.CanBeDamaged) return;

            _consumed = true;
            target.TakeDamage(_damage, _body.linearVelocity.normalized);
            Despawn();
        }

        private void Despawn()
        {
            // TODO(D9 폴리싱): 오브젝트 풀로 교체 — 웨이브 6 탄막에서 Instantiate/Destroy가 GC 스파이크를 낼 수 있다
            Destroy(gameObject);
        }
    }
}
