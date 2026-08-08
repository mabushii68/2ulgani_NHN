using System.Collections.Generic;

namespace Luddite.AIBrain
{
    /// <summary>아레나 4분할 (GDD §2 — favoriteQuadrant 기록용).</summary>
    public enum Quadrant
    {
        NW,
        NE,
        SW,
        SE,
    }

    /// <summary>
    /// 플레이 스타일 프로파일러 (GDD §6.4). <b>순수 C#</b> — 규칙 3.
    ///
    /// <para>수집 항목과 소비처:</para>
    /// <list type="bullet">
    /// <item>평균 교전 거리 (런 전체 + 직전 웨이브) → 매크로 DDA §6.3 + 결과 화면 §13</item>
    /// <item>무빙샷 비율 / 선호 구역 / 8방향 히스토그램 → <b>결과 화면·프로필 전용</b> (§6.4: 전투 반영 ❌)</item>
    /// </list>
    ///
    /// <para>
    /// 전 항목이 <b>시간 가중</b>이다 — 프레임레이트가 흔들려도 비율이 왜곡되지 않는다.
    /// 교전 거리는 적이 1기 이상 있을 때만 집계한다 (빈 아레나를 걷는 시간은 교전이 아니다).
    /// </para>
    /// </summary>
    public sealed class PlayStyleProfiler
    {
        /// <summary>"표본 없음"을 뜻하는 값. 웨이브에 교전 표본이 전혀 없으면 이것이 반환된다.</summary>
        public const float NO_SAMPLE = -1f;

        private const float MOVE_EPSILON_SQR = 1e-4f;

        // ── 런 누적 (시간 가중) ──
        private float _engageTime;
        private float _engageDistanceSum;
        private float _fireTime;
        private float _movingFireTime;
        private float _moveTime;
        private float _totalTime;
        private readonly float[] _quadrantTime = new float[4];
        private readonly float[] _directionTime = new float[8];

        // ── 웨이브 단위 (§6.3 DDA: "직전 웨이브" 평균 교전 거리) ──
        private float _waveEngageTime;
        private float _waveEngageDistanceSum;

        /// <summary>직전 웨이브 평균 교전 거리. 표본 없으면 <see cref="NO_SAMPLE"/>.</summary>
        public float LastWaveAverageEngageDistance { get; private set; } = NO_SAMPLE;

        /// <summary>런 전체 평균 교전 거리 (적이 있던 시간 가중). 표본 없으면 0.</summary>
        public float AverageEngageDistance =>
            _engageTime > 0f ? _engageDistanceSum / _engageTime : 0f;

        /// <summary>발사 중 이동하고 있던 시간 비율 0~1. 발사가 없었다면 0.</summary>
        public float MovingShotRatio =>
            _fireTime > 0f ? _movingFireTime / _fireTime : 0f;

        /// <summary>가장 오래 머문 4분할 구역. 동률이면 NW→NE→SW→SE 순.</summary>
        public Quadrant FavoriteQuadrant
        {
            get
            {
                int best = 0;
                for (int i = 1; i < 4; i++)
                {
                    if (_quadrantTime[i] > _quadrantTime[best]) best = i;
                }
                return (Quadrant)best;
            }
        }

        /// <summary>해당 구역 체류 시간 비율 0~1.</summary>
        public float QuadrantRatio(Quadrant quadrant) =>
            _totalTime > 0f ? _quadrantTime[(int)quadrant] / _totalTime : 0f;

        /// <summary>
        /// 8방향 이동 히스토그램 비율 0~1 (이동 시간 기준).
        /// 인덱스는 동쪽(0)부터 반시계: 0=E, 1=NE, 2=N, 3=NW, 4=W, 5=SW, 6=S, 7=SE.
        /// </summary>
        public float DirectionRatio(int index) =>
            _moveTime > 0f && index >= 0 && index < 8 ? _directionTime[index] / _moveTime : 0f;

        /// <summary>매 틱 호출. 어댑터(<c>AIBrainRunner</c>)가 Unity 상태를 변환해 넘긴다.</summary>
        /// <param name="enemyPositions">현재 필드의 적 위치 전부. 없으면 빈 목록.</param>
        /// <param name="moveInput">이동 입력 (정규화 여부 무관 — 크기만 본다).</param>
        /// <param name="isFiring">이번 틱에 발사 입력이 눌려 있는지.</param>
        public void Tick(float deltaTime, Vec2 playerPosition,
            IReadOnlyList<Vec2> enemyPositions, Vec2 moveInput, bool isFiring)
        {
            if (deltaTime <= 0f) return;

            _totalTime += deltaTime;

            if (enemyPositions != null && enemyPositions.Count > 0)
            {
                float distanceSum = 0f;
                for (int i = 0; i < enemyPositions.Count; i++)
                    distanceSum += Vec2.Distance(playerPosition, enemyPositions[i]);
                float average = distanceSum / enemyPositions.Count;

                _engageTime += deltaTime;
                _engageDistanceSum += average * deltaTime;
                _waveEngageTime += deltaTime;
                _waveEngageDistanceSum += average * deltaTime;
            }

            bool moving = moveInput.SqrMagnitude > MOVE_EPSILON_SQR;

            if (isFiring)
            {
                _fireTime += deltaTime;
                if (moving) _movingFireTime += deltaTime;
            }

            if (moving)
            {
                _moveTime += deltaTime;
                _directionTime[DirectionIndex(moveInput)] += deltaTime;
            }

            _quadrantTime[QuadrantIndex(playerPosition)] += deltaTime;
        }

        /// <summary>웨이브 종료 시 호출 — 직전 웨이브 평균을 확정하고 웨이브 누적을 비운다.</summary>
        public void OnWaveEnded()
        {
            LastWaveAverageEngageDistance =
                _waveEngageTime > 0f ? _waveEngageDistanceSum / _waveEngageTime : NO_SAMPLE;
            _waveEngageTime = 0f;
            _waveEngageDistanceSum = 0f;
        }

        /// <summary>런 시작 시 초기화.</summary>
        public void Reset()
        {
            _engageTime = 0f;
            _engageDistanceSum = 0f;
            _fireTime = 0f;
            _movingFireTime = 0f;
            _moveTime = 0f;
            _totalTime = 0f;
            _waveEngageTime = 0f;
            _waveEngageDistanceSum = 0f;
            LastWaveAverageEngageDistance = NO_SAMPLE;
            for (int i = 0; i < 4; i++) _quadrantTime[i] = 0f;
            for (int i = 0; i < 8; i++) _directionTime[i] = 0f;
        }

        /// <summary>이동 방향 → 8분할 인덱스 (0=E 반시계). 45° 부채꼴의 중앙 정렬.</summary>
        private static int DirectionIndex(Vec2 move)
        {
            double angle = System.Math.Atan2(move.Y, move.X);            // -π ~ π
            double octant = angle / (System.Math.PI / 4.0);              // -4 ~ 4
            int index = (int)System.Math.Round(octant);
            return ((index % 8) + 8) % 8;
        }

        /// <summary>위치 → 4분할. 중심선 위(0)는 동쪽·북쪽에 붙인다.</summary>
        private static int QuadrantIndex(Vec2 position)
        {
            if (position.Y >= 0f) return position.X < 0f ? (int)Quadrant.NW : (int)Quadrant.NE;
            return position.X < 0f ? (int)Quadrant.SW : (int)Quadrant.SE;
        }
    }
}
