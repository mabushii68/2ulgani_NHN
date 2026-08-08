using UnityEngine;
using Luddite.Combat;
using Luddite.Data;

namespace Luddite.Enemies
{
    /// <summary>
    /// 보스 — 거대 LLM (GDD §9). 이 클래스는 <b>P1 "전공의 종말"</b>을 담당한다:
    /// 3전공 패턴 순환 (문과 관통 장탄 / 이과 조준 레이저 / 예체능 회전 광역파), 각 1초 텔레그래프,
    /// HP 25% 감소마다 챗봇 3기 소환. HP 60%에서 P2 전환 — 무적 3초 후 현재는 P1 순환을 계속한다.
    /// TODO(P2 세션): PATTERN: YOU — 거리 복제 / 무기 복제(마젠타) / 구역 선점 장판 + 예측탄.
    ///
    /// <para>패턴 색은 전부 주황 계열 — 마젠타는 P2(AI가 나를 읽은 결과)부터다 (🔴 §10.4).</para>
    /// </summary>
    public class BossLLM : EnemyBase
    {
        private enum State
        {
            Chase,
            Telegraph,
            Cooldown
        }

        private enum Pattern
        {
            PiercingShot,   // 문과
            AimedLaser,     // 이과
            RotatingWave    // 예체능
        }

        [SerializeField] private BossConfigSO _config;
        [SerializeField] private Projectile _projectilePrefab;

        [Tooltip("레이저 텔레그래프 겸 발사 섬광 (비활성 자식)")]
        [SerializeField] private LineRenderer _laserLine;

        [Tooltip("패턴 탄 색 — P1은 주황 계열 (마젠타 금지, §10.4)")]
        [SerializeField] private Color _projectileColor = new Color(1f, 0.62f, 0.28f, 1f);

        [Tooltip("텔레그래프 중 몸 부풀림 배율 — 연출값")]
        [SerializeField] private float _telegraphScalePulse = 1.12f;

        private State _state = State.Chase;
        private Pattern _pattern = Pattern.PiercingShot;
        private Transform _target;
        private Vector2 _aimDirection = Vector2.right;
        private float _stateTimer;
        private float _ringAngle;
        private float _nextSummonFraction;
        private bool _phase2Triggered;
        private float _invulnerableRemaining;
        private float _laserFlashRemaining;
        private Vector3 _normalScale;

        /// <summary>현재 FSM 상태·패턴. 디버그용.</summary>
        public string StateName => $"{_state}/{_pattern}";

        /// <summary>P2 전환 무적(§9) 동안 피격 불가.</summary>
        public override bool CanBeDamaged => base.CanBeDamaged && _invulnerableRemaining <= 0f;

        protected override void Awake()
        {
            base.Awake();
            if (_config == null) Debug.LogError("[BossLLM] BossConfigSO 미지정", this);
            if (_projectilePrefab == null) Debug.LogError("[BossLLM] Projectile 프리팹 미지정", this);

            _normalScale = transform.localScale;
            _nextSummonFraction = 1f - (_config != null ? _config.SummonHpInterval : 0.25f);
            HideLaser();
        }

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _target = player.transform;
            else Debug.LogError("[BossLLM] Player 태그 오브젝트 없음", this);
        }

        protected override void UpdateBehaviour(float deltaTime)
        {
            TickLaserFlash(deltaTime);

            if (_invulnerableRemaining > 0f)
            {
                _invulnerableRemaining -= deltaTime;
                SetMoveVelocity(Vector2.zero);
                return;
            }

            if (_target == null || _config == null)
            {
                SetMoveVelocity(Vector2.zero);
                return;
            }

            TickSummon();
            TickPhaseTransition();

            Vector2 toTarget = (Vector2)_target.position - (Vector2)transform.position;
            float distance = toTarget.magnitude;
            if (distance > Mathf.Epsilon) _aimDirection = toTarget / distance;

            switch (_state)
            {
                case State.Chase:
                    TickChase(distance);
                    break;
                case State.Telegraph:
                    TickTelegraph(deltaTime);
                    break;
                case State.Cooldown:
                    TickCooldown(deltaTime);
                    break;
            }
        }

        // ── P1 소환·전환 (§9) ──

        private float HpFraction => Stats != null && Stats.MaxHp > 0f ? Hp / Stats.MaxHp : 1f;

        /// <summary>HP 25% 감소마다 챗봇 3기 — P2 진입 후에는 소환 없음 (§9).</summary>
        private void TickSummon()
        {
            if (_phase2Triggered || _config.SummonPrefab == null) return;
            if (HpFraction > _nextSummonFraction) return;

            for (int i = 0; i < _config.SummonCount; i++)
            {
                float angle = (360f / _config.SummonCount) * i + Random.Range(-20f, 20f);
                Vector2 offset = Quaternion.Euler(0f, 0f, angle) * Vector2.right * _config.SummonRadius;
                Instantiate(_config.SummonPrefab, (Vector2)transform.position + offset, Quaternion.identity);
            }

            Debug.Log($"[BossLLM] 미니언 {_config.SummonCount}기 소환 (HP {HpFraction:P0})");
            _nextSummonFraction -= _config.SummonHpInterval;
        }

        private void TickPhaseTransition()
        {
            if (_phase2Triggered || HpFraction > _config.Phase2HpFraction) return;

            _phase2Triggered = true;
            _invulnerableRemaining = _config.TransitionInvulnerability;
            _stateTimer = 0f;
            _state = State.Cooldown;
            transform.localScale = _normalScale;
            HideLaser();

            // TODO(P2 세션): "USER MODEL LOADED / COPY COMPLETE / PATTERN: YOU" 연출 + P2 3요소로 행동 교체
            Debug.Log("[BossLLM] P2 전환 — USER MODEL LOADED / COPY COMPLETE / PATTERN: YOU (연출·행동은 P2 세션)");
        }

        // ── FSM ──

        private void TickChase(float distance)
        {
            if (distance > _config.HoldDistance)
            {
                SetMoveVelocity(_aimDirection * Stats.MoveSpeed);
                return;
            }

            SetMoveVelocity(Vector2.zero);
            EnterState(State.Telegraph);
        }

        private void TickTelegraph(float deltaTime)
        {
            SetMoveVelocity(Vector2.zero);
            _stateTimer += deltaTime;

            float progress = Mathf.Clamp01(_stateTimer / Mathf.Max(_config.PatternTelegraph, 1e-4f));
            transform.localScale = _normalScale * Mathf.Lerp(1f, _telegraphScalePulse, progress);

            // 레이저는 텔레그래프 내내 얇은 조준선이 플레이어를 따라간다 — 마지막 순간 위치가 판정 방향
            if (_pattern == Pattern.AimedLaser) ShowLaserLine(_aimDirection, 0.06f);

            if (_stateTimer < _config.PatternTelegraph) return;

            transform.localScale = _normalScale;
            Execute();
            EnterState(State.Cooldown);
        }

        private void TickCooldown(float deltaTime)
        {
            SetMoveVelocity(Vector2.zero);
            _stateTimer += deltaTime;
            if (_stateTimer < _config.PatternCooldown) return;

            _pattern = (Pattern)(((int)_pattern + 1) % 3);   // 문과 → 이과 → 예체능 순환 (§9)
            EnterState(State.Chase);
        }

        private void EnterState(State next)
        {
            _state = next;
            _stateTimer = 0f;
        }

        // ── 패턴 실행 ──

        private void Execute()
        {
            switch (_pattern)
            {
                case Pattern.PiercingShot:
                    ExecutePiercingShots();
                    break;
                case Pattern.AimedLaser:
                    ExecuteLaser();
                    break;
                case Pattern.RotatingWave:
                    ExecuteRotatingWave();
                    break;
            }
        }

        /// <summary>문과: 관통 장탄 부채꼴 (§9). 벽까지 뚫고 가는 큰 탄 — 옆으로 피해야 한다.</summary>
        private void ExecutePiercingShots()
        {
            int count = Mathf.Max(1, _config.PierceShotCount);
            float center = (count - 1) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float angle = (i - center) * _config.PierceSpreadAngle;
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * _aimDirection;
                Projectile shot = SpawnProjectile(direction, _config.PierceSpeed, _config.PierceDamage,
                    _config.PierceDiameter, _config.PierceLifetime);
                if (shot != null) shot.MarkAsPiercing();
            }
        }

        /// <summary>이과: 조준 레이저 — 히트스캔 (§9). 텔레그래프 종료 순간의 방향으로 판정.</summary>
        private void ExecuteLaser()
        {
            Vector2 origin = transform.position;
            Vector2 direction = _aimDirection;

            ShowLaserLine(direction, 0.5f);
            _laserFlashRemaining = _config.LaserFlashDuration;

            if (_target == null) return;
            IDamageable damageable = _target.GetComponent<IDamageable>();
            if (damageable == null || !damageable.CanBeDamaged) return;

            Vector2 toPlayer = (Vector2)_target.position - origin;
            float along = Vector2.Dot(toPlayer, direction);
            if (along < 0f || along > _config.LaserRange) return;

            float lateral = Mathf.Abs(toPlayer.x * direction.y - toPlayer.y * direction.x);   // 수직 거리
            if (lateral > _config.LaserWidth * 0.5f) return;

            damageable.TakeDamage(_config.LaserDamage, direction);
        }

        /// <summary>예체능: 회전 광역파 — 원형 탄막, 발사마다 시작각 회전 (§9).</summary>
        private void ExecuteRotatingWave()
        {
            int count = Mathf.Max(1, _config.RingBulletCount);
            float step = 360f / count;
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = Quaternion.Euler(0f, 0f, _ringAngle + step * i) * Vector2.right;
                SpawnProjectile(direction, _config.RingSpeed, _config.RingDamage,
                    _config.RingDiameter, _config.RingLifetime);
            }
            _ringAngle += _config.RingRotationStep;
        }

        private Projectile SpawnProjectile(Vector2 direction, float speed, float damage,
            float diameter, float lifetime)
        {
            if (_projectilePrefab == null) return null;

            Vector2 origin = (Vector2)transform.position + direction * 1.4f;   // 큰 몸통 밖으로
            Projectile shot = Instantiate(_projectilePrefab, origin, Quaternion.identity);

            SpriteRenderer spriteRenderer = shot.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) spriteRenderer.color = _projectileColor;

            shot.Launch(direction, speed, damage, lifetime, diameter, transform.root, Faction.Player);
            return shot;
        }

        // ── 레이저 시각 ──

        private void ShowLaserLine(Vector2 direction, float width)
        {
            if (_laserLine == null) return;

            if (!_laserLine.gameObject.activeSelf) _laserLine.gameObject.SetActive(true);
            _laserLine.positionCount = 2;
            _laserLine.startWidth = width;
            _laserLine.endWidth = width;
            _laserLine.SetPosition(0, transform.position);
            _laserLine.SetPosition(1, (Vector2)transform.position + direction * _config.LaserRange);
        }

        private void TickLaserFlash(float deltaTime)
        {
            if (_laserFlashRemaining <= 0f)
            {
                // 발사 섬광이 아닌 조준선은 Telegraph 상태가 매 프레임 다시 켠다
                if (_state != State.Telegraph || _pattern != Pattern.AimedLaser) HideLaser();
                return;
            }

            _laserFlashRemaining -= deltaTime;
            if (_laserFlashRemaining <= 0f) HideLaser();
        }

        private void HideLaser()
        {
            if (_laserLine != null && _laserLine.gameObject.activeSelf)
                _laserLine.gameObject.SetActive(false);
        }
    }
}
