using UnityEngine;

namespace Luddite.Enemies
{
    /// <summary>
    /// 코딩봇 (GDD §5.1 — 코딩 어시스턴트 풍자, 삼각/무채색). FSM (§5.2):
    /// <c>Spawn → Approach → (거리&lt;5) ChargeTelegraph(0.4s, 방향 고정) → Dash(10u/s, 0.6s) → Recovery(0.8s)…</c>
    /// 탄을 쏘지 않는다 — 위협은 몸이다. 돌진 중 접촉 데미지 12 (평시 공통 8).
    /// <b>방향 고정</b>이 핵심 계약: 텔레그래프 시작 순간의 방향으로만 돌진하므로 플레이어가 읽고 피할 수 있고,
    /// 그 회피가 AIBrain의 표본이 되지는 않는다 (§7.1의 원시 단위는 탄환) — 대신 압박으로 이동을 강제한다.
    /// </summary>
    public class CoderBot : EnemyBase
    {
        private enum State
        {
            Approach,
            ChargeTelegraph,
            Dash,
            Recovery
        }

        [Tooltip("조준·돌진 방향으로 돌릴 본체(삼각형이 진행 방향을 가리키게). 비워 두면 무시")]
        [SerializeField] private Transform _aimPivot;

        [Tooltip("텔레그래프 중 뒤로 움츠리는 거리(유닛) — 연출값. TODO(아트): 전용 텔레그래프 표현으로 교체")]
        [SerializeField] private float _telegraphCrouch = 0.25f;

        private State _state = State.Approach;
        private Transform _target;
        private Vector2 _aimDirection = Vector2.right;
        private Vector2 _dashDirection = Vector2.right;
        private float _stateTimer;
        private float _attackReadyTimer;

        /// <summary>현재 FSM 상태 이름. 디버그·스모크 테스트용.</summary>
        public string StateName => _state.ToString();

        /// <summary>돌진 중 접촉 데미지 12, 평시 공통 8 (§5.1).</summary>
        protected override float CurrentContactDamage =>
            _state == State.Dash ? Stats.DashContactDamage : base.CurrentContactDamage;

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _target = player.transform;
            else Debug.LogError("[CoderBot] Player 태그 오브젝트 없음 — 추적 불가", this);
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

            // 삼각형이 진행 방향을 가리킨다. 단, 방향 고정 구간(텔레그래프·돌진)에서는 돌진 방향 유지
            if (_aimPivot != null)
                _aimPivot.right = _state == State.ChargeTelegraph || _state == State.Dash
                    ? _dashDirection
                    : _aimDirection;

            if (_state != State.ChargeTelegraph && _state != State.Dash) _attackReadyTimer += deltaTime;

            switch (_state)
            {
                case State.Approach:
                    TickApproach(distance);
                    break;
                case State.ChargeTelegraph:
                    TickChargeTelegraph(deltaTime);
                    break;
                case State.Dash:
                    TickDash(deltaTime);
                    break;
                case State.Recovery:
                    TickRecovery(deltaTime);
                    break;
            }
        }

        private void TickApproach(float distance)
        {
            if (distance >= Stats.AttackRange || _attackReadyTimer < Stats.AttackCooldown)
            {
                SetMoveVelocity(_aimDirection * Stats.MoveSpeed);
                return;
            }

            // 🔒 방향 고정 (§5.2): 텔레그래프 시작 순간의 플레이어 방향으로만 돌진한다
            _dashDirection = _aimDirection;
            SetMoveVelocity(Vector2.zero);
            EnterState(State.ChargeTelegraph);
        }

        private void TickChargeTelegraph(float deltaTime)
        {
            _stateTimer += deltaTime;

            // 플레이스홀더 텔레그래프: 돌진 반대쪽으로 살짝 움츠린다 (§5.1 "돌진 전 0.4초 텔레그래프")
            float progress = Stats.AimDuration > 0f ? Mathf.Clamp01(_stateTimer / Stats.AimDuration) : 1f;
            SetMoveVelocity(-_dashDirection * (_telegraphCrouch / Mathf.Max(Stats.AimDuration, 1e-4f))
                * (1f - progress));

            if (_stateTimer < Stats.AimDuration) return;

            _attackReadyTimer = 0f;
            EnterState(State.Dash);
        }

        private void TickDash(float deltaTime)
        {
            _stateTimer += deltaTime;
            SetMoveVelocity(_dashDirection * Stats.DashSpeed);

            if (_stateTimer < Stats.DashDuration) return;

            SetMoveVelocity(Vector2.zero);
            EnterState(State.Recovery);
        }

        private void TickRecovery(float deltaTime)
        {
            SetMoveVelocity(Vector2.zero);
            _stateTimer += deltaTime;
            if (_stateTimer < Stats.RecoveryDuration) return;

            EnterState(State.Approach);
        }

        private void EnterState(State next)
        {
            _state = next;
            _stateTimer = 0f;
        }
    }
}
