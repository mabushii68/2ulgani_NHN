using UnityEngine;
using Luddite.AIBrain;
using Luddite.Combat;
using Luddite.Core;
using Luddite.Data;

namespace Luddite.Enemies
{
    /// <summary>
    /// 보스 — 거대 LLM (GDD §9).
    ///
    /// <para><b>P1 "전공의 종말"</b> (HP 100~60%): 3전공 패턴 순환 (문과 관통 장탄 / 이과 조준 레이저 /
    /// 예체능 회전 광역파), 각 1초 텔레그래프, HP 25% 감소마다 챗봇 3기 소환. 색은 전부 주황 (🔴 §10.4).</para>
    ///
    /// <para><b>P2 "PATTERN: YOU"</b> (HP 60~0%, 전환 무적 3초 후): 3요소 동시 — 전부 기존 데이터 재사용, 신규 시스템 없음.</para>
    /// <list type="number">
    /// <item><b>거리 복제</b>: 플레이어의 <c>avgDistanceToEnemies</c>(AIBrainRunner)를 유지하며 교전</item>
    /// <item><b>무기 복제</b>: 플레이어 전공의 P1 패턴만 <b>마젠타</b>로 사용 (패턴 순환 정지)</item>
    /// <item><b>구역 선점</b>: <c>favoriteQuadrant</c>에 주기적 장판 (<see cref="BossZoneHazard"/>)</item>
    /// </list>
    /// <para>+ 통상 공격 일부가 예측탄 — <see cref="EliteModifier"/> 재사용 (§7.4의 HIGH 게이트·2회당 1회·
    /// 온스크린 게이트가 그대로 적용). 소환은 P2에서 없음 (§9).</para>
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

        [Header("P2 — PATTERN: YOU (§9)")]
        [Tooltip("P2 장판·오라용 흰 원 스프라이트 (틴트 전제 — 빌더가 Placeholder_Circle 배선)")]
        [SerializeField] private Sprite _zoneSprite;

        [Tooltip("P2 본체 틴트 — 컬러 스프라이트 위 곱셈이라 완전 마젠타는 안 나온다. 마젠타 오라가 주 신호 (§15.6-1)")]
        [SerializeField] private Color _p2BodyTint = new Color(1f, 0.45f, 1f, 1f);

        [Tooltip("P2 공격 색 — 🔴 §10.4: 마젠타 = AI가 나를 읽고 행하는 것. P2 무기 복제가 정확히 그것이다")]
        [SerializeField] private Color _p2ProjectileColor = new Color(1f, 0.1f, 1f, 1f);

        private State _state = State.Chase;
        private Pattern _pattern = Pattern.PiercingShot;
        private Transform _target;
        private Vector2 _aimDirection = Vector2.right;
        private float _stateTimer;
        private float _ringAngle;
        private float _nextSummonFraction;
        private bool _phase2Triggered;
        private bool _phase2Active;
        private float _invulnerableRemaining;
        private float _laserFlashRemaining;
        private float _zoneTimer;
        private bool _predictiveAim;
        private Vector3 _normalScale;

        private EliteModifier _elite;
        private AIBrainRunner _brain;
        private GameManager _gameManager;
        private DungeonManager _dungeon;
        private SpriteRenderer _bodyRenderer;

        /// <summary>현재 FSM 상태·패턴. 디버그용.</summary>
        public string StateName => $"{_state}/{_pattern}" + (_phase2Active ? " [P2]" : "");

        /// <summary>P2 행동이 실제로 돌고 있는가 (전환 무적 종료 후). 스모크·디버그용.</summary>
        public bool IsPhaseTwoActive => _phase2Active;

        /// <summary>P2 전환 무적(§9) 동안 피격 불가.</summary>
        public override bool CanBeDamaged => base.CanBeDamaged && _invulnerableRemaining <= 0f;

        protected override void Awake()
        {
            base.Awake();
            if (_config == null) Debug.LogError("[BossLLM] BossConfigSO 미지정", this);
            if (_projectilePrefab == null) Debug.LogError("[BossLLM] Projectile 프리팹 미지정", this);

            _normalScale = transform.localScale;
            _nextSummonFraction = 1f - (_config != null ? _config.SummonHpInterval : 0.25f);
            _elite = GetComponent<EliteModifier>();   // P2 예측탄 (§7.4 재사용). 없으면 예측탄만 빠진다
            _bodyRenderer = FindBodyRenderer();
            HideLaser();
        }

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _target = player.transform;
            else Debug.LogError("[BossLLM] Player 태그 오브젝트 없음", this);

            _brain = FindFirstObjectByType<AIBrainRunner>();
            _gameManager = FindFirstObjectByType<GameManager>();
            _dungeon = FindFirstObjectByType<DungeonManager>();
            if (_brain == null)
                Debug.LogWarning("[BossLLM] AIBrainRunner 없음 — P2 거리 복제·구역 선점이 기본값으로 동작", this);
        }

        protected override void UpdateBehaviour(float deltaTime)
        {
            TickLaserFlash(deltaTime);

            if (_invulnerableRemaining > 0f)
            {
                _invulnerableRemaining -= deltaTime;
                SetMoveVelocity(Vector2.zero);

                // 전환 무적이 끝나는 순간 P2 행동 개시 (§9: "아래 3요소 동시")
                if (_invulnerableRemaining <= 0f && _phase2Triggered && !_phase2Active)
                    BeginPhaseTwo();
                return;
            }

            if (_target == null || _config == null)
            {
                SetMoveVelocity(Vector2.zero);
                return;
            }

            TickSummon();
            TickPhaseTransition();
            if (_phase2Active) TickZone(deltaTime);

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
            _predictiveAim = false;
            if (_elite != null) _elite.CancelPredictiveAim();
            HideLaser();

            GameEvents.RaiseBossPhaseTwoStarted();   // UI 연출 (BossPhaseOverlay)은 버스 건너편에서
            Debug.Log("[BossLLM] P2 전환 — USER MODEL LOADED / COPY COMPLETE / PATTERN: YOU");
        }

        /// <summary>전환 무적 종료 시 1회: 전공 패턴 고정 + 마젠타화 + 장판 타이머 시작.</summary>
        private void BeginPhaseTwo()
        {
            _phase2Active = true;
            _pattern = PatternForMajor(_gameManager != null ? _gameManager.SelectedMajor : Major.LiberalArts);
            _projectileColor = _p2ProjectileColor;
            _zoneTimer = 0f;
            ApplyPhaseTwoVisuals();

            Debug.Log($"[BossLLM] P2 개시 — 복제 패턴 {_pattern}, 유지 거리 {DesiredEngageDistance():F1}u, " +
                      $"선호 구역 {(_brain != null ? _brain.FavoriteQuadrant.ToString() : "?")}");
        }

        /// <summary>§9 P2-② 무기 복제: 전공 → P1 패턴 매핑. 색 교체 재사용이 §9의 명시 방침.</summary>
        private static Pattern PatternForMajor(Major major)
        {
            switch (major)
            {
                case Major.Science: return Pattern.AimedLaser;
                case Major.Arts: return Pattern.RotatingWave;
                default: return Pattern.PiercingShot;   // 문과
            }
        }

        /// <summary>
        /// P2 마젠타화 (🔴 §10.4). 컬러 스프라이트 위 곱셈 틴트는 마젠타를 못 내므로(SYSTEMS §15.6-1)
        /// 본체 틴트는 보조 신호고, <b>주 신호는 마젠타 오라 링 + 마젠타 공격</b>이다 —
        /// 위협 신호를 색 하나에 걸지 않는다 (DarkOrb 실루엣 선례와 같은 원칙).
        /// </summary>
        private void ApplyPhaseTwoVisuals()
        {
            // SetBaseColor를 경유해야 한다 — 직접 색만 바꾸면 피격 플래시가 끝날 때
            // Awake 시점 색으로 되돌아가 맞을 때마다 P2 마젠타가 벗겨진다 (D7)
            if (_bodyRenderer != null) SetBaseColor(_p2BodyTint);

            if (_laserLine != null)
            {
                Color magenta = _p2ProjectileColor;
                _laserLine.startColor = magenta;
                _laserLine.endColor = new Color(magenta.r, magenta.g, magenta.b, 0.4f);
            }

            if (_zoneSprite != null)
            {
                GameObject aura = new GameObject("P2Aura");
                aura.transform.SetParent(transform, false);
                SpriteRenderer renderer = aura.AddComponent<SpriteRenderer>();
                renderer.sprite = _zoneSprite;
                renderer.color = new Color(1f, 0f, 1f, 0.28f);
                CopyBodySorting(renderer);

                // 본체보다 살짝 큰 링 느낌 — 흰 원 지름을 본체 히트박스의 1.4배로
                float spriteDiameter = Mathf.Max(_zoneSprite.bounds.size.x, 1e-3f);
                float targetDiameter = (Stats != null ? Stats.HitboxDiameter : 2f) * 1.4f;
                // 루트 스케일(대형 보스 ×2)이 곱해지므로 로컬은 루트 배율로 나눈다
                float rootScale = Mathf.Max(transform.localScale.x, 1e-3f);
                aura.transform.localScale = Vector3.one * (targetDiameter / spriteDiameter / rootScale);
            }
        }

        /// <summary>오라를 본체와 같은 레이어, 본체 바로 아래 순서에 둔다 — Default에 두면 바닥 밑에 깔린다 (§15.6).</summary>
        private void CopyBodySorting(SpriteRenderer renderer)
        {
            if (_bodyRenderer != null)
            {
                renderer.sortingLayerID = _bodyRenderer.sortingLayerID;
                renderer.sortingOrder = _bodyRenderer.sortingOrder - 1;
            }
        }

        private SpriteRenderer FindBodyRenderer()
        {
            Transform body = transform.Find("Body");
            if (body != null)
            {
                SpriteRenderer named = body.GetComponent<SpriteRenderer>();
                if (named != null) return named;
            }
            return GetComponentInChildren<SpriteRenderer>();
        }

        // ── P2 구역 선점 (§9 P2-③) ──

        /// <summary>주기마다 favoriteQuadrant에 장판. 프로필 기준(방 중심)과 같은 좌표계를 쓴다.</summary>
        private void TickZone(float deltaTime)
        {
            _zoneTimer += deltaTime;
            if (_zoneTimer < _config.ZoneInterval) return;
            _zoneTimer = 0f;

            if (_zoneSprite == null || _brain == null) return;

            Vector2 center;
            Vector2 halfExtents;
            if (_dungeon != null && _dungeon.Active && _dungeon.CurrentRoom != null)
            {
                center = _dungeon.CurrentRoom.Center;
                halfExtents = _dungeon.RoomHalfExtents;
            }
            else
            {
                center = Vector2.zero;                          // 폴백 아레나는 원점
                halfExtents = _config.FallbackArenaHalfExtents;
            }

            Vector2 sign = QuadrantSign(_brain.FavoriteQuadrant);
            Vector2 position = center + Vector2.Scale(sign, halfExtents * 0.5f);

            BossZoneHazard.Spawn(position, _zoneSprite, _config.ZoneRadius,
                _config.ZoneTelegraph, _config.ZoneActiveDuration, _config.ZoneDamagePerSecond);
            Debug.Log($"[BossLLM] 구역 장판 — {_brain.FavoriteQuadrant} ({position.x:F1}, {position.y:F1})");
        }

        private static Vector2 QuadrantSign(Quadrant quadrant)
        {
            switch (quadrant)
            {
                case Quadrant.NE: return new Vector2(1f, 1f);
                case Quadrant.SW: return new Vector2(-1f, -1f);
                case Quadrant.SE: return new Vector2(1f, -1f);
                default: return new Vector2(-1f, 1f);   // NW
            }
        }

        // ── FSM ──

        /// <summary>P2 거리 복제 (§9 P2-①): 플레이어의 런 평균 교전 거리를 유지 목표로 삼는다.</summary>
        private float DesiredEngageDistance()
        {
            if (!_phase2Active || _brain == null) return _config.HoldDistance;

            float learned = _brain.AverageEngageDistance;
            if (learned <= 0f) return _config.HoldDistance;   // 표본 없음 — P1 거리 유지
            return Mathf.Clamp(learned, _config.P2MinEngageDistance, _config.P2MaxEngageDistance);
        }

        private float CurrentMoveSpeed => _phase2Active ? _config.P2MoveSpeed : Stats.MoveSpeed;

        private void TickChase(float distance)
        {
            float desired = DesiredEngageDistance();

            if (!_phase2Active)
            {
                // P1: 유지 거리까지 접근 후 패턴 (기존 동작)
                if (distance > desired)
                {
                    SetMoveVelocity(_aimDirection * CurrentMoveSpeed);
                    return;
                }

                SetMoveVelocity(Vector2.zero);
                BeginTelegraph();
                return;
            }

            // P2: 거리 복제 — 밴드 밖이면 접근/후퇴, 사거리 안이면 공격 개시.
            // 후퇴 중에도 desired+tol 안이면 공격한다 (벽에 몰리면 후퇴가 막혀도 공격이 끊기지 않게)
            float tolerance = _config.P2DistanceTolerance;
            if (distance > desired + tolerance)
            {
                SetMoveVelocity(_aimDirection * CurrentMoveSpeed);
                return;
            }

            if (distance < desired - tolerance)
                SetMoveVelocity(-_aimDirection * CurrentMoveSpeed);
            else
                SetMoveVelocity(Vector2.zero);

            BeginTelegraph();
        }

        /// <summary>
        /// 텔레그래프 진입. P2에서는 이번 공격이 예측탄인지 먼저 묻는다 (§7.4 —
        /// HIGH 게이트·공격 2회당 1회·온스크린 게이트를 <see cref="EliteModifier"/>가 판정).
        /// </summary>
        private void BeginTelegraph()
        {
            EnterState(State.Telegraph);
            _predictiveAim = _phase2Active && _elite != null && _elite.TryBeginPredictiveAim();
        }

        private void TickTelegraph(float deltaTime)
        {
            SetMoveVelocity(Vector2.zero);   // 멈춰서 조준 — 안 지우면 직전 이동 속도로 드리프트한다
            _stateTimer += deltaTime;

            if (_predictiveAim)
            {
                // 예측 공격: §7.4 텔레그래프 (마젠타 조준선이 예측 지점을 따라간다)
                _elite.UpdatePredictiveAim();
                if (_stateTimer < _elite.TelegraphDuration) return;

                transform.localScale = _normalScale;
                _elite.FirePredictive();
                _predictiveAim = false;
                EnterState(State.Cooldown);
                return;
            }

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

            // P1: 문과 → 이과 → 예체능 순환 (§9). P2: 플레이어 전공 패턴 고정 (무기 복제)
            if (!_phase2Active) _pattern = (Pattern)(((int)_pattern + 1) % 3);
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
