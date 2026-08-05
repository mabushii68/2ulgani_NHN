using System.Collections.Generic;
using UnityEngine;
using Luddite.Combat;
using Luddite.Data;

namespace Luddite.Enemies
{
    /// <summary>
    /// 적 공통 기반 — 체력·피격·넉백·스폰 텔레그래프·접촉 데미지·사망.
    /// 이동과 공격 패턴은 파생 클래스의 FSM이 담당한다 (GDD §5.2).
    /// 엘리트는 별도 클래스가 아니라 이 위에 EliteModifier를 얹는다 (§5.1).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public abstract class EnemyBase : MonoBehaviour, IDamageable
    {
        /// <summary>
        /// 살아 있는 적 레지스트리. 프로파일러(평균 교전 거리, §6.4)가 매 프레임 훑는다 —
        /// <c>FindObjectsByType</c>의 프레임당 배열 할당을 피하는 투사체 레지스트리와 같은 패턴.
        /// </summary>
        private static readonly List<EnemyBase> _active = new List<EnemyBase>(16);

        public static IReadOnlyList<EnemyBase> Active => _active;

        /// <summary>도메인 리로드를 끈 상태에서 플레이 모드를 재시작하면 죽은 항목이 남는다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => _active.Clear();

        [SerializeField] protected EnemyStatsSO _stats;

        [Tooltip("본체 스프라이트. 피격 플래시·스폰 텔레그래프에 사용")]
        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("피격 플래시 지속(초) — 연출값")]
        [SerializeField] private float _hitFlashDuration = 0.06f;

        [Tooltip("넉백 후 급감속 계수. 맞고 미끄러지지 않게 하는 연출값")]
        [SerializeField] private float _knockbackDamping = 12f;

        [Tooltip("넉백이 이동 입력을 이기는 시간(초). 이 동안 FSM의 이동 명령을 무시해 넉백이 보이게 한다")]
        [SerializeField] private float _knockbackDuration = 0.15f;

        private Rigidbody2D _body;
        private CircleCollider2D _hitbox;
        private Color _baseColor;
        private Vector3 _baseScale = Vector3.one;
        private float _hp;
        private float _spawnRemaining;
        private float _flashRemaining;
        private float _knockbackRemaining;

        public Faction Faction => Faction.Enemy;

        /// <summary>스폰 텔레그래프 중인지. 이 동안 공격도 피격도 불가 (GDD §2).</summary>
        public bool IsSpawning => _spawnRemaining > 0f;

        /// <summary>생존 여부. 무적과 무관하게 체력만 본다.</summary>
        public bool IsAlive => _hp > 0f;

        /// <summary>피격 가능 상태. 사망했거나 스폰 텔레그래프 중이면 false.</summary>
        public bool CanBeDamaged => IsAlive && !IsSpawning;

        /// <summary>남은 체력. HUD·디버그용.</summary>
        public float Hp => _hp;

        /// <summary>넉백이 이동을 덮어쓰는 중인지. 이 동안 <see cref="SetMoveVelocity"/>는 무시된다.</summary>
        public bool IsKnockedBack => _knockbackRemaining > 0f;

        protected Rigidbody2D Body => _body;
        protected EnemyStatsSO Stats => _stats;

        /// <summary>
        /// FSM의 이동 명령. 넉백 중에는 무시된다 —
        /// 매 프레임 velocity를 덮어쓰면 넉백이 즉시 지워져 타격감이 사라진다.
        /// </summary>
        protected void SetMoveVelocity(Vector2 velocity)
        {
            if (IsKnockedBack) return;
            _body.linearVelocity = velocity;
        }

        protected virtual void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _hitbox = GetComponent<CircleCollider2D>();

            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _body.linearDamping = _knockbackDamping;

            if (_stats == null)
            {
                Debug.LogError($"[{GetType().Name}] EnemyStatsSO 미지정 — {name}", this);
                return;
            }

            _hitbox.radius = _stats.HitboxRadius;
            _hp = _stats.MaxHp;
            _spawnRemaining = _stats.SpawnTelegraphDuration;

            // 엘리트(×1.3, §5.1)처럼 프리팹이 기본 스케일을 갖는 경우를 위해 캡처 —
            // 스폰 텔레그래프가 Vector3.one으로 복원하면 엘리트가 스폰 후 일반 크기가 된다
            _baseScale = transform.localScale;

            if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>();
            if (_renderer != null) _baseColor = _renderer.color;
        }

        protected virtual void OnEnable() => _active.Add(this);

        protected virtual void OnDisable() => _active.Remove(this);

        protected virtual void Update()
        {
            if (_stats == null) return;

            TickSpawnTelegraph();
            TickHitFlash();
            if (_knockbackRemaining > 0f) _knockbackRemaining -= Time.deltaTime;

            if (IsSpawning) return;
            UpdateBehaviour(Time.deltaTime);
        }

        /// <summary>FSM 갱신. 스폰 텔레그래프가 끝난 뒤에만 호출된다.</summary>
        protected abstract void UpdateBehaviour(float deltaTime);

        private void TickSpawnTelegraph()
        {
            if (_spawnRemaining <= 0f) return;

            _spawnRemaining -= Time.deltaTime;

            // 등장 연출: 작게 시작해 제 크기로 부풀고, 반투명에서 불투명으로
            float progress = _stats.SpawnTelegraphDuration <= 0f
                ? 1f
                : Mathf.Clamp01(1f - _spawnRemaining / _stats.SpawnTelegraphDuration);

            transform.localScale = _baseScale * Mathf.Lerp(0.5f, 1f, progress);
            if (_renderer != null)
            {
                Color c = _baseColor;
                c.a = Mathf.Lerp(0.25f, _baseColor.a, progress);
                _renderer.color = c;
            }

            if (_spawnRemaining > 0f) return;

            transform.localScale = _baseScale;
            if (_renderer != null) _renderer.color = _baseColor;
        }

        private void TickHitFlash()
        {
            if (_flashRemaining <= 0f) return;

            _flashRemaining -= Time.deltaTime;
            if (_flashRemaining <= 0f && _renderer != null) _renderer.color = _baseColor;
        }

        public void TakeDamage(float amount, Vector2 hitDirection)
        {
            if (!CanBeDamaged) return;   // 사망·스폰 텔레그래프 중 무시

            _hp -= amount;

            // 감쇠 v(t) = v0·e^(-d·t) 에서 총 이동거리 ≈ v0/d 이므로 v0 = 목표거리 × 감쇠계수
            _body.linearVelocity = hitDirection.normalized * _stats.KnockbackDistance * _knockbackDamping;
            _knockbackRemaining = _knockbackDuration;

            if (_renderer != null)
            {
                _renderer.color = Color.white;
                _flashRemaining = _hitFlashDuration;
            }

            if (_hp > 0f) return;
            Die();
        }

        protected virtual void Die()
        {
            // TODO(D4 웨이브): 사망을 WaveManager가 집계해야 웨이브 전멸 판정이 된다 (GDD §6.1)
            Debug.Log($"[{GetType().Name}] {_stats.DisplayName} 격파");
            Destroy(gameObject);
        }

        /// <summary>
        /// 현재 접촉 데미지량. 기본은 공통 8 (§3.2)이지만 파생이 상태에 따라 바꿀 수 있다 —
        /// 코딩봇의 돌진 접촉 12 (§5.1)가 그 사례.
        /// </summary>
        protected virtual float CurrentContactDamage => _stats != null ? _stats.ContactDamage : 0f;

        /// <summary>접촉 데미지 (GDD §3.2). 플레이어의 무적 시간이 연타를 막아 준다.</summary>
        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!CanBeDamaged || _stats == null) return;

            IDamageable target = collision.collider.GetComponentInParent<IDamageable>();
            if (target == null || target.Faction != Faction.Player || !target.CanBeDamaged) return;

            Vector2 toTarget = (Vector2)collision.transform.position - _body.position;
            target.TakeDamage(CurrentContactDamage, toTarget.normalized);
        }
    }
}
