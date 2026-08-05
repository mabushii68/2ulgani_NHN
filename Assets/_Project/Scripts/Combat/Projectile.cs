using System.Collections.Generic;
using UnityEngine;
using Luddite.Core;

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

        /// <summary>
        /// 살아 있는 투사체 레지스트리. <see cref="AIBrainRunner"/>가 매 프레임 적 탄환을 훑어야 하는데
        /// <c>FindObjectsByType</c>은 프레임마다 배열을 새로 할당한다 — 웨이브 6의 탄막에서 GC를 때린다.
        /// 서비스 싱글턴이 아니라 목록일 뿐이므로 "Singleton 최소화"(규칙 5)에 어긋나지 않는다.
        /// </summary>
        private static readonly List<Projectile> _active = new List<Projectile>(64);

        public static IReadOnlyList<Projectile> Active => _active;

        private Rigidbody2D _body;
        private CircleCollider2D _hitbox;
        private Transform _ownerRoot;
        private Faction _targetFaction;
        private float _damage;
        private float _lifeRemaining;
        private bool _consumed;
        private bool _invincibleTouchReported;

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

        private void OnEnable() => _active.Add(this);

        private void OnDisable() => _active.Remove(this);

        /// <summary>도메인 리로드를 끈 상태에서 플레이 모드를 재시작하면 죽은 항목이 남는다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => _active.Clear();

        /// <summary>현재 위치 (물리 기준).</summary>
        public Vector2 Position => _body != null ? _body.position : (Vector2)transform.position;

        /// <summary>현재 속도. AIBrain이 TTI를 계산하는 입력이다 (§7.1).</summary>
        public Vector2 Velocity => _body != null ? _body.linearVelocity : Vector2.zero;

        /// <summary>이 탄이 때리는 진영. AIBrain은 <see cref="Faction.Player"/> 표적만 위협으로 본다.</summary>
        public Faction TargetFaction => _targetFaction;

        /// <summary>
        /// 예측탄인지 (§7.4). <c>WasHit &amp;&amp; IsPredictive</c>가 "예측 적중" 집계 조건이고,
        /// 반대로 회피에 성공하면 <c>PREDICTION FAILED</c>(§10.3)로 이어진다.
        /// 예측탄 발사 자체는 D3 작업이므로 지금은 항상 false다.
        /// </summary>
        public bool IsPredictive { get; private set; }

        /// <summary>엘리트·보스의 예측탄 발사기가 <see cref="Launch"/> 직후 호출한다 (D3).</summary>
        public void MarkAsPredictive() => IsPredictive = true;

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
            _invincibleTouchReported = false;
            IsPredictive = false;

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
            if (!target.CanBeDamaged)
            {
                // 단, 무적 중이라도 플레이어 몸에 닿은 탄은 §7.1의 "피격"이다 (D2 해석 확정: 닿으면 피격).
                // 관통을 회피 성공으로 두면 안 피했는데 피한 것으로 기록되는 표본 오염이 생긴다.
                // 데미지·탄 소모 없이 AIBrain 통보만 한다. 사망 후 시체 접촉은 통보하지 않는다.
                if (_targetFaction == Faction.Player && target.IsAlive && !_invincibleTouchReported)
                {
                    _invincibleTouchReported = true; // 탄환당 1회 — 겹친 채 재접촉해도 중복 통보 없음
                    GameEvents.RaiseProjectileHitPlayer(GetInstanceID());
                }
                return;
            }

            _consumed = true;
            target.TakeDamage(_damage, _body.linearVelocity.normalized);

            // AIBrain이 §7.1 위기 이벤트를 "회피 실패"로 확정하려면 어느 탄에 맞았는지 알아야 한다.
            // 투사체가 AIBrain을 직접 알 필요는 없으므로 이벤트 버스를 경유한다 (규칙 4).
            if (_targetFaction == Faction.Player) GameEvents.RaiseProjectileHitPlayer(GetInstanceID());

            Despawn();
        }

        private void Despawn()
        {
            // TODO(D9 폴리싱): 오브젝트 풀로 교체 — 웨이브 6 탄막에서 Instantiate/Destroy가 GC 스파이크를 낼 수 있다
            Destroy(gameObject);
        }
    }
}
