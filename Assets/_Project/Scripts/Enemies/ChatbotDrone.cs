using UnityEngine;

namespace Luddite.Enemies
{
    /// <summary>
    /// 챗봇 드론 (GDD §5.1 기본 유닛). FSM (§5.2):
    /// <c>Spawn → Approach → (사거리 진입) Aim(0.3s) → Fire → Cooldown → Approach…</c>
    /// Spawn 구간은 <see cref="EnemyBase"/>가 처리하므로 여기서는 나머지 3상태만 다룬다.
    /// 거리 유지는 하지 않는다 — 그건 그림봇 담당(§5.2). 챗봇은 사거리에 들어오면 멈춰서 쏜다.
    /// </summary>
    public class ChatbotDrone : EnemyBase
    {
        private enum State
        {
            Approach,
            Aim,
            Cooldown
        }

        [SerializeField] private EnemyGun _gun;

        [Tooltip("조준 중 본체를 표적 쪽으로 돌릴 표식. 비워 두면 무시")]
        [SerializeField] private Transform _aimPivot;

        private State _state = State.Approach;
        private Transform _target;
        private Vector2 _aimDirection = Vector2.right;
        private float _stateTimer;

        /// <summary>엘리트일 때만 존재 (§5.1: 엘리트 = 챗봇 프리팹 + EliteModifier). 없으면 일반 챗봇.</summary>
        private EliteModifier _elite;
        private bool _predictiveAim;

        /// <summary>현재 FSM 상태 이름. 디버그·스모크 테스트용.</summary>
        public string StateName => _state.ToString();

        protected override void Awake()
        {
            base.Awake();
            if (_gun == null) _gun = GetComponentInChildren<EnemyGun>();
            if (_gun == null) Debug.LogError("[ChatbotDrone] EnemyGun을 찾지 못함", this);
            _elite = GetComponent<EliteModifier>();
        }

        private void Start()
        {
            // "Player"는 Unity 기본 태그라 TagManager(ProjectSettings) 변경이 필요하지 않다
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _target = player.transform;
            else Debug.LogError("[ChatbotDrone] Player 태그 오브젝트 없음 — 추적 불가", this);
        }

        protected override void UpdateBehaviour(float deltaTime)
        {
            if (_target == null)
            {
                SetMoveVelocity(Vector2.zero);
                return;
            }

            Vector2 toTarget = (Vector2)_target.position - (Vector2)transform.position;
            float distance = toTarget.magnitude;
            if (distance > Mathf.Epsilon) _aimDirection = toTarget / distance;
            if (_aimPivot != null) _aimPivot.right = _aimDirection;

            switch (_state)
            {
                case State.Approach:
                    TickApproach(distance);
                    break;
                case State.Aim:
                    TickAim(deltaTime);
                    break;
                case State.Cooldown:
                    TickCooldown(deltaTime);
                    break;
            }
        }

        private void TickApproach(float distance)
        {
            if (distance > Stats.AttackRange)
            {
                SetMoveVelocity(_aimDirection * Stats.MoveSpeed);
                return;
            }

            // 사거리 진입 → 멈추고 조준. 엘리트라면 이번 공격이 예측탄인지 여기서 결정된다 (§7.4)
            SetMoveVelocity(Vector2.zero);
            EnterState(State.Aim);
            _predictiveAim = _elite != null && _elite.TryBeginPredictiveAim();
        }

        private void TickAim(float deltaTime)
        {
            SetMoveVelocity(Vector2.zero);
            _stateTimer += deltaTime;

            // 예측 공격은 조준 시간도 §7.4 텔레그래프(0.35s)로 대체 — 마커가 플레이어를 따라간다
            if (_predictiveAim) _elite.UpdatePredictiveAim();
            float aimDuration = _predictiveAim ? _elite.TelegraphDuration : Stats.AimDuration;
            if (_stateTimer < aimDuration) return;

            // Fire는 상태가 아니라 Aim 종료 시점의 1회 동작이다
            if (_predictiveAim)
            {
                _elite.FirePredictive();
                _predictiveAim = false;
            }
            else if (_gun != null)
            {
                _gun.Fire(_aimDirection);
            }

            EnterState(State.Cooldown);
        }

        private void TickCooldown(float deltaTime)
        {
            SetMoveVelocity(Vector2.zero);
            _stateTimer += deltaTime;
            if (_stateTimer < Stats.AttackCooldown) return;

            EnterState(State.Approach);
        }

        private void EnterState(State next)
        {
            _state = next;
            _stateTimer = 0f;
        }
    }
}
