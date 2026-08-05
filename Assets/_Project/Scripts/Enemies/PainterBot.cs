using UnityEngine;

namespace Luddite.Enemies
{
    /// <summary>
    /// 그림봇 (GDD §5.1 — 이미지 생성 AI 풍자, 사각/무채색). FSM (§5.2):
    /// <c>Spawn → SeekRange(6~9) → Strafe → Telegraph(0.4s) → SpreadShot → Reposition…</c>
    /// Spawn 구간은 <see cref="EnemyBase"/>가 처리한다.
    /// 챗봇과 달리 멈춰 쏘지 않는다 — 거리를 유지하며 횡이동(Strafe)하다가 부채꼴 3발.
    /// 공격 준비(쿨다운)는 이동 중에도 차오르고, 준비가 되면 Strafe에서 Telegraph로 넘어간다.
    /// </summary>
    public class PainterBot : EnemyBase
    {
        private enum State
        {
            SeekRange,
            Strafe,
            Telegraph,
            Reposition
        }

        [SerializeField] private EnemyGun _gun;

        [Tooltip("조준 방향으로 돌릴 표식. 비워 두면 무시")]
        [SerializeField] private Transform _aimPivot;

        [Tooltip("텔레그래프 중 몸을 부풀리는 배율 — 연출값. TODO(아트): 전용 텔레그래프 표현으로 교체")]
        [SerializeField] private float _telegraphScalePulse = 1.15f;

        private State _state = State.SeekRange;
        private Transform _target;
        private Vector2 _aimDirection = Vector2.right;
        private float _stateTimer;
        private float _attackReadyTimer;
        private int _strafeSign = 1;
        private Vector3 _normalScale;

        /// <summary>현재 FSM 상태 이름. 디버그·스모크 테스트용.</summary>
        public string StateName => _state.ToString();

        protected override void Awake()
        {
            base.Awake();
            if (_gun == null) _gun = GetComponentInChildren<EnemyGun>();
            if (_gun == null) Debug.LogError("[PainterBot] EnemyGun을 찾지 못함", this);

            _normalScale = transform.localScale;
            _strafeSign = Random.value < 0.5f ? -1 : 1;
        }

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _target = player.transform;
            else Debug.LogError("[PainterBot] Player 태그 오브젝트 없음 — 추적 불가", this);
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

            // 공격 준비는 텔레그래프 중이 아니면 항상 차오른다 — §5.1 공격 간격 2.5초의 기준
            if (_state != State.Telegraph) _attackReadyTimer += deltaTime;

            switch (_state)
            {
                case State.SeekRange:
                    TickSeekRange(distance);
                    break;
                case State.Strafe:
                    TickStrafe(distance);
                    break;
                case State.Telegraph:
                    TickTelegraph(deltaTime);
                    break;
                case State.Reposition:
                    TickReposition(deltaTime, distance);
                    break;
            }
        }

        private void TickSeekRange(float distance)
        {
            if (distance > Stats.PreferredRangeMax)
            {
                SetMoveVelocity(_aimDirection * Stats.MoveSpeed);
                return;
            }

            if (distance < Stats.PreferredRangeMin)
            {
                SetMoveVelocity(-_aimDirection * Stats.MoveSpeed);
                return;
            }

            EnterState(State.Strafe);
        }

        private void TickStrafe(float distance)
        {
            // 유지 거리를 크게 벗어나면 먼저 거리부터 복구 (여유 0.5유닛의 히스테리시스)
            if (distance > Stats.PreferredRangeMax + 0.5f || distance < Stats.PreferredRangeMin - 0.5f)
            {
                EnterState(State.SeekRange);
                return;
            }

            SetMoveVelocity(Perpendicular() * (Stats.MoveSpeed * _strafeSign));

            if (_attackReadyTimer < Stats.AttackCooldown) return;

            SetMoveVelocity(Vector2.zero);
            EnterState(State.Telegraph);
        }

        private void TickTelegraph(float deltaTime)
        {
            SetMoveVelocity(Vector2.zero);
            _stateTimer += deltaTime;

            // 플레이스홀더 텔레그래프: 0.4초 동안 몸이 부풀어 오른다 (§5.2 — 읽을 수 있어야 한다)
            float progress = Stats.AimDuration > 0f ? Mathf.Clamp01(_stateTimer / Stats.AimDuration) : 1f;
            transform.localScale = _normalScale * Mathf.Lerp(1f, _telegraphScalePulse, progress);

            if (_stateTimer < Stats.AimDuration) return;

            transform.localScale = _normalScale;
            FireSpread();
            _attackReadyTimer = 0f;
            _strafeSign = -_strafeSign;   // 발사 후 반대 방향으로 재배치 — 같은 자리 반복 방지
            EnterState(State.Reposition);
        }

        /// <summary>부채꼴 발사 (§5.1: 3방향, 사이각 30°). 가운데 탄이 플레이어 정조준.</summary>
        private void FireSpread()
        {
            if (_gun == null) return;

            int count = Mathf.Max(1, Stats.SpreadShotCount);
            float centerIndex = (count - 1) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float angle = (i - centerIndex) * Stats.SpreadAngleStep;
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * _aimDirection;
                _gun.Fire(direction);
            }
        }

        private void TickReposition(float deltaTime, float distance)
        {
            _stateTimer += deltaTime;

            // 횡이동 + 거리 이탈 시 소폭 복귀 성분
            Vector2 velocity = Perpendicular() * (Stats.MoveSpeed * _strafeSign);
            if (distance > Stats.PreferredRangeMax) velocity += _aimDirection * (Stats.MoveSpeed * 0.5f);
            else if (distance < Stats.PreferredRangeMin) velocity -= _aimDirection * (Stats.MoveSpeed * 0.5f);
            SetMoveVelocity(velocity);

            if (_stateTimer < Stats.RepositionDuration) return;
            EnterState(State.SeekRange);
        }

        /// <summary>플레이어 기준 접선 방향 (반시계 90°). 부호는 <c>_strafeSign</c>이 결정.</summary>
        private Vector2 Perpendicular() => new Vector2(-_aimDirection.y, _aimDirection.x);

        private void EnterState(State next)
        {
            _state = next;
            _stateTimer = 0f;
        }
    }
}
