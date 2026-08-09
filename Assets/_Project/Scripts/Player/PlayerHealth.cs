using UnityEngine;
using Luddite.Combat;
using Luddite.Core;
using Luddite.Data;

namespace Luddite.Player
{
    /// <summary>
    /// 플레이어 체력·무적 (GDD §3.2, §3.3).
    ///
    /// 🔴 계약: <b>플레이어 피격 넉백은 절대 없다.</b> 넉백이 회피 방향을 왜곡하면
    /// AIBrain이 학습하는 LEFT/RIGHT 표본이 오염된다 (§7.1). <see cref="TakeDamage"/>가
    /// <c>hitDirection</c>을 받지만 이동에 반영하지 않는 것은 실수가 아니라 설계다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private PlayerStatsSO _stats;

        [Tooltip("깜빡임에 사용할 본체 스프라이트. 비워 두면 자식에서 찾는다")]
        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("무적 중 깜빡임 주기(초) — 연출값")]
        [SerializeField] private float _blinkInterval = 0.08f;

        [Tooltip("깜빡임의 어두운 쪽 알파 — 연출값")]
        [SerializeField] private float _blinkAlpha = 0.35f;

        private Color _baseColor;
        private float _hp;
        private float _invincibleRemaining;
        private PlayerUpgrades _upgrades;

        public Faction Faction => Faction.Player;

        /// <summary>생존 여부. 무적과 무관하게 체력만 본다.</summary>
        public bool IsAlive => _hp > 0f;

        /// <summary>피격 가능 상태. 사망했거나 무적(i-frame) 중이면 false.</summary>
        public bool CanBeDamaged => IsAlive && _invincibleRemaining <= 0f;

        public float Hp => _hp;

        /// <summary>최대 체력 = SO 기본값 + 업그레이드 보너스 (§8 #4 국가장학금).</summary>
        public float MaxHp => (_stats != null ? _stats.MaxHp : 0f) + (_upgrades != null ? _upgrades.BonusMaxHp : 0f);

        /// <summary>0~1 체력 비율. HUD가 읽는다 (GDD §10.1).</summary>
        public float HpRatio => MaxHp > 0f ? Mathf.Clamp01(_hp / MaxHp) : 0f;

        private void Awake()
        {
            if (_stats == null)
            {
                Debug.LogError("[PlayerHealth] PlayerStatsSO 미지정", this);
                return;
            }

            if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>();
            if (_renderer != null) _baseColor = _renderer.color;

            _upgrades = GetComponent<PlayerUpgrades>();
            _hp = _stats.MaxHp;
        }

        private void OnEnable() => GameEvents.RunStarted += ResetForNewRun;

        private void OnDisable() => GameEvents.RunStarted -= ResetForNewRun;

        /// <summary>
        /// 새 런 시작(전공 확정) 시 체력·무적을 초기 상태로 되돌린다.
        /// SO 기본값을 쓴다 — 새 런의 업그레이드 보너스는 항상 0이라, PlayerUpgrades의
        /// RunStarted 리셋과의 구독 순서에 무관하게 결과가 같다.
        /// </summary>
        private void ResetForNewRun()
        {
            if (_stats == null) return;

            _hp = _stats.MaxHp;
            _invincibleRemaining = 0f;
            RestoreColor();
        }

        private void Update()
        {
            if (_invincibleRemaining <= 0f) return;

            _invincibleRemaining -= Time.deltaTime;

            if (_invincibleRemaining > 0f) Blink();
            else RestoreColor();
        }

        private void Blink()
        {
            if (_renderer == null || _blinkInterval <= 0f) return;

            bool dim = Mathf.FloorToInt(_invincibleRemaining / _blinkInterval) % 2 == 0;
            Color c = _baseColor;
            c.a = dim ? _blinkAlpha : _baseColor.a;
            _renderer.color = c;
        }

        private void RestoreColor()
        {
            if (_renderer != null) _renderer.color = _baseColor;
        }

        /// <param name="hitDirection">
        /// 의도적으로 사용하지 않는다 — 플레이어 넉백 금지 (🔴 계약).
        /// 방향은 향후 피격 연출(플래시 방향 등)에만 쓸 수 있고, 이동에는 절대 반영하지 않는다.
        /// </param>
        public void TakeDamage(float amount, Vector2 hitDirection)
        {
            if (!CanBeDamaged || _stats == null) return;

            _hp = Mathf.Max(0f, _hp - amount);
            _invincibleRemaining = _stats.InvincibleDuration;
            AudioDirector.Play(GameSfx.PlayerHit);   // 실데미지 시에만 — 무적 관통은 CanBeDamaged가 걸렀다

            Debug.Log($"[PlayerHealth] 피격 {amount} → HP {_hp}/{MaxHp}");

            if (IsAlive) return;
            Die();
        }

        /// <summary>업그레이드 "국가장학금"(즉시 25 회복) 등이 사용할 회복 API (GDD §8).</summary>
        public void Heal(float amount)
        {
            if (!IsAlive) return;
            _hp = Mathf.Min(MaxHp, _hp + amount);
        }

        private void Die()
        {
            RestoreColor();
            Debug.Log("[PlayerHealth] 사망");

            // GameManager가 구독해 Result(패배)로 전환한다 (§1.4). 체력이 게임 플로우를 알 필요 없음
            GameEvents.RaisePlayerDied();
        }
    }
}
