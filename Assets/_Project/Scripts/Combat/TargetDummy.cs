using UnityEngine;

namespace Luddite.Combat
{
    /// <summary>
    /// 발사·피격·넉백 검증용 임시 표적. 이동·공격·FSM 없음.
    /// TODO(D5 적 구현): GDD §5의 EnemyStatsSO + FSM 적으로 교체하고 이 클래스는 삭제한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class TargetDummy : MonoBehaviour, IDamageable
    {
        [Tooltip("체력. 챗봇 드론 기준값 30 (GDD §5.1) — 임시 표적이므로 SO로 뽑지 않는다")]
        [SerializeField] private float _maxHp = 30f;

        [Tooltip("피격 시 밀리는 거리(유닛). 적 넉백 소량 = 0.5 (GDD §3.2)")]
        [SerializeField] private float _knockbackDistance = 0.5f;

        [Tooltip("피격 플래시 지속(초)")]
        [SerializeField] private float _flashDuration = 0.06f;

        private Rigidbody2D _body;
        private SpriteRenderer _renderer;
        private Color _baseColor;
        private float _hp;
        private float _flashRemaining;

        public bool IsAlive => _hp > 0f;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _body.linearDamping = 12f;   // 넉백 직후 급감속 — 맞고 미끄러지지 않게

            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer != null) _baseColor = _renderer.color;

            _hp = _maxHp;
        }

        private void Update()
        {
            if (_flashRemaining <= 0f) return;

            _flashRemaining -= Time.deltaTime;
            if (_flashRemaining <= 0f && _renderer != null) _renderer.color = _baseColor;
        }

        public void TakeDamage(float amount, Vector2 hitDirection)
        {
            if (!IsAlive) return;

            _hp -= amount;

            // 감쇠 v(t) = v0·e^(-d·t) 에서 총 이동거리 ≈ v0/d 이므로 v0 = 목표거리 × 감쇠계수
            _body.linearVelocity = hitDirection.normalized * _knockbackDistance * _body.linearDamping;

            if (_renderer != null)
            {
                _renderer.color = Color.white;
                _flashRemaining = _flashDuration;
            }

            if (IsAlive) return;

            Debug.Log($"[TargetDummy] {name} 파괴");
            Destroy(gameObject);
        }
    }
}
