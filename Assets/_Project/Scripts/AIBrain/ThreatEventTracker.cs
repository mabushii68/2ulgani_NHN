using System.Collections.Generic;

namespace Luddite.AIBrain
{
    /// <summary>탐지에 필요한 탄환 정보. 어댑터가 매 틱 채워서 넘긴다.</summary>
    public readonly struct ThreatBullet
    {
        /// <summary>탄환 고유 식별자. 어댑터는 <c>GetInstanceID()</c>를 쓴다 (0은 예약값이라 쓰지 않는다).</summary>
        public readonly int Id;

        public readonly Vec2 Position;
        public readonly Vec2 Velocity;

        /// <summary>예측탄인지 (§7.4). "예측 적중" 집계에 쓰인다.</summary>
        public readonly bool IsPredictive;

        public ThreatBullet(int id, Vec2 position, Vec2 velocity, bool isPredictive)
        {
            Id = id;
            Position = position;
            Velocity = velocity;
            IsPredictive = isPredictive;
        }
    }

    /// <summary>피격 위기 이벤트 탐지 설정 (GDD §7.1). 앞의 3개는 🔴 계약값이다.</summary>
    public readonly struct ThreatDetectionSettings
    {
        /// <summary>🔴 트리거 임계 TTI(초). 기본 0.5.</summary>
        public readonly float TriggerTimeToImpact;

        /// <summary>🔴 판정 확정 창(초). 기본 0.6. 이 안에 반드시 결론이 난다.</summary>
        public readonly float ResolveWindow;

        /// <summary>🔴 학습 표본 인정 최소 좌우 변위(유닛). 기본 0.3.</summary>
        public readonly float MinLateralDisplacement;

        /// <summary>
        /// 위협으로 볼 최대 예상 근접거리(유닛).
        /// <b>GDD에 명시되지 않은 값이다</b> — 없으면 화면 반대편으로 날아가는 탄도
        /// "TTI 0.5초"에 걸려 표본이 오염되므로 추가했다. 사람 확인 필요.
        /// </summary>
        public readonly float ThreatMissRadius;

        public ThreatDetectionSettings(float triggerTimeToImpact, float resolveWindow,
            float minLateralDisplacement, float threatMissRadius)
        {
            TriggerTimeToImpact = triggerTimeToImpact;
            ResolveWindow = resolveWindow;
            MinLateralDisplacement = minLateralDisplacement;
            ThreatMissRadius = threatMissRadius;
        }

        /// <summary>GDD 기본값.</summary>
        public static ThreatDetectionSettings Default => new ThreatDetectionSettings(0.5f, 0.6f, 0.3f, 2f);
    }

    /// <summary>
    /// 피격 위기 이벤트 탐지기 (GDD §7.1) — <b>모든 학습·판정의 원시 단위를 만드는 곳</b>.
    ///
    /// <para><b>🔴 계약</b></para>
    /// <list type="bullet">
    /// <item>적 탄환의 TTI가 0.5초 이내로 진입하면 트리거. <b>탄환당 1회만</b></item>
    /// <item>트리거 후 0.6초 이내에 판정 확정</item>
    /// <item>좌우 변위 0.3유닛 미달(제자리·전후 이동만)이면 학습 표본에서 제외</item>
    /// <item>동시 탄막에서는 <b>TTI 최단 탄환 1개만</b> 처리 — 표본 오염 방지</item>
    /// </list>
    ///
    /// <para>
    /// UnityEngine 의존 없음. <see cref="Tick"/>에 가짜 위치·속도 시퀀스를 넣어 전 동작을 검증할 수 있다.
    /// </para>
    /// </summary>
    public sealed class ThreatEventTracker
    {
        /// <summary>"맞은 탄 없음"을 뜻하는 예약 ID. Unity instanceID는 0이 되지 않는다.</summary>
        public const int NO_BULLET = 0;

        private readonly ThreatDetectionSettings _settings;

        /// <summary>이미 트리거된 탄환. "탄환당 1회" 계약을 지키는 장치.</summary>
        private readonly HashSet<int> _triggeredBullets = new HashSet<int>();

        private readonly List<ThreatSample> _resolvedThisTick = new List<ThreatSample>();
        private readonly List<int> _pruneScratch = new List<int>();

        private bool _hasActiveWatch;
        private int _watchBulletId;
        private bool _watchIsPredictive;
        private Vec2 _watchBulletDirection;
        private Vec2 _watchPlayerPositionAtTrigger;
        private float _watchAge;

        public ThreatEventTracker(ThreatDetectionSettings settings)
        {
            _settings = settings;
        }

        /// <summary>지금 위기 이벤트를 추적 중인지. HUD가 "지금 판정 중"을 표시할 때 쓸 수 있다.</summary>
        public bool HasActiveWatch => _hasActiveWatch;

        /// <summary>추적 중인 탄환 ID. 없으면 <see cref="NO_BULLET"/>.</summary>
        public int ActiveWatchBulletId => _hasActiveWatch ? _watchBulletId : NO_BULLET;

        /// <summary>
        /// 한 틱 진행. 어댑터가 매 프레임 호출한다.
        /// </summary>
        /// <param name="deltaTime">경과 시간(초)</param>
        /// <param name="playerPosition">현재 플레이어 위치</param>
        /// <param name="bullets">현재 살아 있는 <b>적</b> 탄환 전부</param>
        /// <param name="hitBulletId">이 틱에 플레이어를 맞힌 탄환 ID. 없으면 <see cref="NO_BULLET"/></param>
        /// <returns>이 틱에 확정된 표본들. 대개 0개 또는 1개다</returns>
        public IReadOnlyList<ThreatSample> Tick(float deltaTime, Vec2 playerPosition,
            IReadOnlyList<ThreatBullet> bullets, int hitBulletId = NO_BULLET)
        {
            _resolvedThisTick.Clear();

            if (_hasActiveWatch) UpdateActiveWatch(deltaTime, playerPosition, bullets, hitBulletId);
            if (!_hasActiveWatch) TryStartWatch(playerPosition, bullets);

            PruneVanishedBullets(bullets);
            return _resolvedThisTick;
        }

        private void UpdateActiveWatch(float deltaTime, Vec2 playerPosition,
            IReadOnlyList<ThreatBullet> bullets, int hitBulletId)
        {
            _watchAge += deltaTime;

            if (hitBulletId == _watchBulletId)
            {
                Resolve(playerPosition, wasHit: true);
                return;
            }

            int index = IndexOf(bullets, _watchBulletId);
            if (index < 0)
            {
                // 탄이 사라졌다 = 최근접점을 지나 소멸했거나 벽에 막혔다 → 맞지 않았으므로 회피 성공
                Resolve(playerPosition, wasHit: false);
                return;
            }

            // 최근접점 통과 판정: TTI가 음수로 돌아서면 멀어지는 중이다
            float tti = TimeToClosestApproach(playerPosition, bullets[index]);
            if (tti < 0f)
            {
                Resolve(playerPosition, wasHit: false);
                return;
            }

            // 🔴 계약: 0.6초 안에 반드시 결론을 낸다. 맞지 않은 채 창이 끝나면 회피 성공으로 확정
            if (_watchAge >= _settings.ResolveWindow) Resolve(playerPosition, wasHit: false);
        }

        private void Resolve(Vec2 playerPosition, bool wasHit)
        {
            Vec2 displacement = playerPosition - _watchPlayerPositionAtTrigger;
            float lateral = Vec2.Dot(displacement, _watchBulletDirection.Left);

            bool enoughDisplacement = lateral >= _settings.MinLateralDisplacement ||
                                      lateral <= -_settings.MinLateralDisplacement;

            // GDD §7.1은 "피격 없이 최근접점 통과 → 회피 성공. …기록·학습"이라고
            // 학습을 성공 분기에만 명시한다. 피격은 회피 실패이므로 방향을 학습하지 않는다.
            bool counts = !wasHit && enoughDisplacement;

            DodgeDirection direction = lateral >= 0f ? DodgeDirection.Left : DodgeDirection.Right;

            _resolvedThisTick.Add(new ThreatSample(
                wasHit: wasHit,
                wasPredictive: _watchIsPredictive,
                countsAsLearningSample: counts,
                direction: direction,
                lateralDisplacement: lateral,
                resolveDelay: _watchAge));

            _hasActiveWatch = false;
            _watchBulletId = NO_BULLET;
        }

        private void TryStartWatch(Vec2 playerPosition, IReadOnlyList<ThreatBullet> bullets)
        {
            int bestIndex = -1;
            float bestTti = float.MaxValue;

            for (int i = 0; i < bullets.Count; i++)
            {
                ThreatBullet bullet = bullets[i];
                if (_triggeredBullets.Contains(bullet.Id)) continue;   // 탄환당 1회

                float tti = TimeToClosestApproach(playerPosition, bullet);
                if (tti < 0f || tti > _settings.TriggerTimeToImpact) continue;
                if (MissDistance(playerPosition, bullet, tti) > _settings.ThreatMissRadius) continue;

                if (tti >= bestTti) continue;   // 🔴 TTI 최단 1개만
                bestTti = tti;
                bestIndex = i;
            }

            if (bestIndex < 0) return;

            ThreatBullet chosen = bullets[bestIndex];
            _hasActiveWatch = true;
            _watchBulletId = chosen.Id;
            _watchIsPredictive = chosen.IsPredictive;
            _watchBulletDirection = chosen.Velocity.Normalized;
            _watchPlayerPositionAtTrigger = playerPosition;
            _watchAge = 0f;

            _triggeredBullets.Add(chosen.Id);
        }

        /// <summary>사라진 탄환의 ID를 정리한다. 12분 런에서 무한히 쌓이지 않게 하는 것이 목적.</summary>
        private void PruneVanishedBullets(IReadOnlyList<ThreatBullet> bullets)
        {
            if (_triggeredBullets.Count == 0) return;

            _pruneScratch.Clear();
            foreach (int id in _triggeredBullets)
            {
                if (id == _watchBulletId) continue;
                if (IndexOf(bullets, id) < 0) _pruneScratch.Add(id);
            }

            for (int i = 0; i < _pruneScratch.Count; i++) _triggeredBullets.Remove(_pruneScratch[i]);
        }

        /// <summary>
        /// 탄환이 플레이어에게 가장 가까워지는 시각(초). 음수면 이미 멀어지는 중.
        /// 직선 등속 가정 — 적 탄환은 전부 직선이다 (GDD §5.1).
        /// </summary>
        private static float TimeToClosestApproach(Vec2 playerPosition, ThreatBullet bullet)
        {
            float speedSqr = bullet.Velocity.SqrMagnitude;
            if (speedSqr < 1e-6f) return -1f;   // 정지한 탄은 위협이 아니다

            Vec2 toPlayer = playerPosition - bullet.Position;
            return Vec2.Dot(toPlayer, bullet.Velocity) / speedSqr;
        }

        /// <summary>최근접 시점의 예상 거리(유닛).</summary>
        private static float MissDistance(Vec2 playerPosition, ThreatBullet bullet, float tti)
        {
            Vec2 closestPoint = bullet.Position + bullet.Velocity * tti;
            return Vec2.Distance(closestPoint, playerPosition);
        }

        private static int IndexOf(IReadOnlyList<ThreatBullet> bullets, int id)
        {
            for (int i = 0; i < bullets.Count; i++)
            {
                if (bullets[i].Id == id) return i;
            }
            return -1;
        }

        /// <summary>런 시작 시 초기화.</summary>
        public void Reset()
        {
            _triggeredBullets.Clear();
            _resolvedThisTick.Clear();
            _hasActiveWatch = false;
            _watchBulletId = NO_BULLET;
            _watchAge = 0f;
        }
    }
}
