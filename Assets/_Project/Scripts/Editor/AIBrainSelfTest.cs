// AIBrain 자체 검증 루틴 — 가짜 이벤트 시퀀스 주입 → 확률·신뢰도 출력.
// CLAUDE.md 세션 종료 조건 3번("AIBrain 변경 시 순수 C# 테스트 루틴 실행, 결과 로그")의 실행 수단.
// Unity 플레이 모드가 필요 없다 — AIBrain이 UnityEngine에 의존하지 않기 때문에 에디터에서 즉시 돌아간다.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Luddite.AIBrain;

namespace Luddite.EditorTools
{
    public static class AIBrainSelfTest
    {
        private const float TICK = 1f / 60f;
        private const float EPS = 0.0005f;

        private static int _passed;
        private static int _failed;
        private static StringBuilder _log;

        [MenuItem("Luddite/Dev/AIBrain Self Test")]
        public static void Run()
        {
            _passed = 0;
            _failed = 0;
            _log = new StringBuilder();

            _log.AppendLine("===== AIBrain 자체 검증 (GDD §7) =====");

            TestInitialStateIsIgnorance();
            TestLaplaceSmoothing();
            TestConfidenceGatePassesAtThreshold();
            TestConfidenceGateNeedsBothConditions();
            TestDecayCausesConfidenceLoss();
            TestDecayRegressesToFiftyFifty();
            TestVirtualCountsAreNeverDecayed();
            TestBehaviourCorrectionUpgrade();
            TestDataFabricationUpgrade();

            TestThreatTriggerAndLeftDodge();
            TestThreatRightDodge();
            TestThreatExcludesSmallDisplacement();
            TestThreatHitIsNotLearned();
            TestThreatResolveWindowIsEnforced();
            TestThreatPicksShortestTimeToImpact();
            TestThreatTriggersOncePerBullet();
            TestThreatIgnoresDistantBullet();

            TestCounterDodgeDetected();
            TestCounterRequiresOppositeDirection();
            TestCounterRequiresDodgeSuccess();
            TestCounterRequiresDisplacement();
            TestCounterRequiresPredictiveBullet();

            TestProfilerInitialState();
            TestProfilerAverageEngageDistance();
            TestProfilerIgnoresEmptyArena();
            TestProfilerMovingShotRatio();
            TestProfilerFavoriteQuadrant();
            TestProfilerDirectionHistogram();
            TestProfilerWaveSnapshot();
            TestProfilerReset();

            string summary = $"결과: {_passed} 통과 / {_failed} 실패";
            _log.AppendLine("===== " + summary + " =====");

            // Unity 콘솔은 멀티라인 로그의 첫 줄만 외부로 노출되는 경우가 있어 파일에도 남긴다.
            // Logs/는 .gitignore 대상이므로 리포에 들어가지 않는다.
            string reportPath = System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(), "Logs", "aibrain-selftest.txt");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(reportPath));
            System.IO.File.WriteAllText(reportPath, _log.ToString());

            // 첫 줄에 요약을 넣어 콘솔만 봐도 성패를 알 수 있게 한다
            string oneLine = $"[AIBrainSelfTest] {summary} — 상세: Logs/aibrain-selftest.txt";
            if (_failed > 0) Debug.LogError(oneLine + "\n" + _log);
            else Debug.Log(oneLine + "\n" + _log);
        }

        // ───────────────────────── 확률 모델 (§7.2, §7.3) ─────────────────────────

        private static void TestInitialStateIsIgnorance()
        {
            DodgePredictor p = NewPredictor();
            Approx("초기 P(LEFT) = 0.5 (아무것도 모름)", p.ProbabilityOf(DodgeDirection.Left), 0.5f);
            Approx("초기 표본 수 = 0", p.ValidSamples, 0f);
            IsFalse("초기 신뢰도는 LOW", p.IsHighConfidence);
        }

        private static void TestLaplaceSmoothing()
        {
            DodgePredictor p = NewPredictor();
            p.Observe(DodgeDirection.Left);
            // (1+1)/(1+2) = 0.6667 — 표본 1개로 100%를 주장하지 않는 것이 Laplace의 목적
            Approx("LEFT 1회 → P(LEFT) = 2/3", p.ProbabilityOf(DodgeDirection.Left), 2f / 3f);
            Approx("LEFT 1회 → P(RIGHT) = 1/3", p.ProbabilityOf(DodgeDirection.Right), 1f / 3f);
            Approx("확률 합 = 1", p.ProbabilityOf(DodgeDirection.Left) + p.ProbabilityOf(DodgeDirection.Right), 1f);
            IsFalse("표본 1개는 확률이 높아도 LOW (이중 게이트)", p.IsHighConfidence);
        }

        private static void TestConfidenceGatePassesAtThreshold()
        {
            DodgePredictor p = NewPredictor();
            for (int i = 0; i < 8; i++) p.Observe(DodgeDirection.Left);

            Approx("LEFT ×8 → 표본 8", p.ValidSamples, 8f);
            Approx("LEFT ×8 → P(LEFT) = 9/10", p.ProbabilityOf(DodgeDirection.Left), 0.9f);
            IsTrue("표본 8 + 확률 0.9 → HIGH", p.IsHighConfidence);
            IsTrue("우세 방향 = LEFT", p.DominantDirection == DodgeDirection.Left);
        }

        private static void TestConfidenceGateNeedsBothConditions()
        {
            // 경계 검증: L=6 R=2 → 표본 8(통과), P(LEFT) = 7/10 = 0.70(정확히 임계)
            DodgePredictor p = NewPredictor();
            for (int i = 0; i < 6; i++) p.Observe(DodgeDirection.Left);
            for (int i = 0; i < 2; i++) p.Observe(DodgeDirection.Right);
            Approx("L6:R2 → P(LEFT) = 0.70 (임계 정확히)", p.ProbabilityOf(DodgeDirection.Left), 0.7f);
            IsTrue("임계값 정확히 도달 시 HIGH (>= 비교)", p.IsHighConfidence);

            // 표본은 충분하지만 확률이 모자란 경우 → LOW
            DodgePredictor q = NewPredictor();
            for (int i = 0; i < 5; i++) q.Observe(DodgeDirection.Left);
            for (int i = 0; i < 5; i++) q.Observe(DodgeDirection.Right);
            Approx("L5:R5 → 표본 10", q.ValidSamples, 10f);
            Approx("L5:R5 → P = 0.5", q.DominantProbability, 0.5f);
            IsFalse("표본은 충분해도 확률 미달이면 LOW", q.IsHighConfidence);
        }

        private static void TestDecayCausesConfidenceLoss()
        {
            DodgePredictor p = NewPredictor();
            for (int i = 0; i < 8; i++) p.Observe(DodgeDirection.Left);
            IsTrue("감쇠 전 HIGH", p.IsHighConfidence);

            p.ApplyWaveDecay();   // ×0.8 → 6.4
            Approx("1회 감쇠 → 표본 6.4", p.ValidSamples, 6.4f);
            IsFalse("표본이 8 미달로 떨어지면 즉시 LOW (§7.3 게이트 붕괴)", p.IsHighConfidence);
        }

        private static void TestDecayRegressesToFiftyFifty()
        {
            DodgePredictor p = NewPredictor();
            for (int i = 0; i < 20; i++) p.Observe(DodgeDirection.Left);
            float before = p.ProbabilityOf(DodgeDirection.Left);

            for (int wave = 0; wave < 30; wave++) p.ApplyWaveDecay();
            float after = p.ProbabilityOf(DodgeDirection.Left);

            IsTrue($"감쇠 반복 → 확률이 0.5로 회귀 ({before:F3} → {after:F3})", after < before && after < 0.55f);
            IsTrue("감쇠는 관측 카운트를 0으로 밀지만 음수는 아니다", p.ValidSamples >= 0f);
        }

        private static void TestVirtualCountsAreNeverDecayed()
        {
            // 🔴 계약: 가상 카운트 (1,1)은 감쇠 대상이 아니다.
            // 관측이 0인 상태에서 아무리 감쇠해도 확률은 정확히 0.5여야 한다.
            DodgePredictor p = NewPredictor();
            for (int i = 0; i < 50; i++) p.ApplyWaveDecay();
            Approx("관측 0 + 감쇠 50회 → P(LEFT) 정확히 0.5 (가상 카운트 보존)",
                p.ProbabilityOf(DodgeDirection.Left), 0.5f);
        }

        private static void TestBehaviourCorrectionUpgrade()
        {
            // GDD §8 #7 「행동교정」: 관측 카운트 ×0.2 → 신뢰도 사실상 리셋
            DodgePredictor p = NewPredictor();
            for (int i = 0; i < 10; i++) p.Observe(DodgeDirection.Left);
            for (int i = 0; i < 2; i++) p.Observe(DodgeDirection.Right);
            IsTrue("행동교정 전 HIGH", p.IsHighConfidence);

            p.ScaleObservations(0.2f);
            Approx("행동교정 → 표본 12 → 2.4", p.ValidSamples, 2.4f);
            IsFalse("행동교정 후 LOW", p.IsHighConfidence);
        }

        private static void TestDataFabricationUpgrade()
        {
            // GDD §8 #8 「논문조작」: 우세의 반대 방향에 가짜 표본 8개.
            // 세칙 — 가짜 표본은 관측 카운트로 취급되어 표본 수에 포함된다.
            DodgePredictor p = NewPredictor();
            for (int i = 0; i < 10; i++) p.Observe(DodgeDirection.Left);
            IsTrue("조작 전 HIGH (LEFT 우세)", p.IsHighConfidence);

            DodgeDirection injected = p.InjectFakeSamples(8f);
            IsTrue("주입 방향은 우세의 반대 = RIGHT", injected == DodgeDirection.Right);
            Approx("가짜 표본이 표본 수에 포함 (10 + 8 = 18)", p.ValidSamples, 18f);
            Approx("P(LEFT) = 11/20 = 0.55", p.ProbabilityOf(DodgeDirection.Left), 0.55f);
            IsFalse("조작으로 확률이 무너져 LOW", p.IsHighConfidence);
        }

        // ───────────────────────── 피격 위기 이벤트 (§7.1) ─────────────────────────

        private static void TestThreatTriggerAndLeftDodge()
        {
            // 탄이 아래(-Y)에서 위(+Y)로 접근. 탄 진행 방향 (0,1)의 왼쪽은 -X.
            ThreatSample? s = SimulateApproachingBullet(playerLateralSpeed: 6f, forceHit: false);
            IsTrue("트리거·판정이 확정됨", s.HasValue);
            if (!s.HasValue) return;

            IsFalse("맞지 않음 → 회피 성공", s.Value.WasHit);
            IsTrue("학습 표본으로 인정", s.Value.CountsAsLearningSample);
            IsTrue($"-X 이동 → LEFT ({s.Value.Direction})", s.Value.Direction == DodgeDirection.Left);
            IsTrue($"판정이 0.6초 창 안에 났다 ({s.Value.ResolveDelay:F3}s)", s.Value.ResolveDelay <= 0.6f + EPS);
        }

        private static void TestThreatRightDodge()
        {
            ThreatSample? s = SimulateApproachingBullet(playerLateralSpeed: -6f, forceHit: false);
            IsTrue("반대로 이동해도 판정 확정", s.HasValue);
            if (!s.HasValue) return;
            IsTrue($"+X 이동 → RIGHT ({s.Value.Direction})", s.Value.Direction == DodgeDirection.Right);
        }

        private static void TestThreatExcludesSmallDisplacement()
        {
            // 🔴 계약: 좌우 변위 0.3유닛 미달이면 표본 제외.
            // 0.3 u/s로 약 0.5초 이동 = 0.15유닛 → 제외되어야 한다.
            ThreatSample? s = SimulateApproachingBullet(playerLateralSpeed: 0.3f, forceHit: false);
            IsTrue("판정 자체는 확정됨", s.HasValue);
            if (!s.HasValue) return;

            IsTrue($"변위가 0.3 미달 ({s.Value.LateralDisplacement:F3})",
                Mathf.Abs(s.Value.LateralDisplacement) < 0.3f);
            IsFalse("제자리에 준하는 이동은 학습 표본에서 제외", s.Value.CountsAsLearningSample);
        }

        private static void TestThreatHitIsNotLearned()
        {
            ThreatSample? s = SimulateApproachingBullet(playerLateralSpeed: 6f, forceHit: true);
            IsTrue("피격도 판정으로 확정됨", s.HasValue);
            if (!s.HasValue) return;

            IsTrue("피격으로 기록", s.Value.WasHit);
            IsFalse("피격은 회피 실패이므로 방향을 학습하지 않는다 (§7.1)", s.Value.CountsAsLearningSample);
        }

        private static void TestThreatResolveWindowIsEnforced()
        {
            // 플레이어가 탄과 같은 속도로 +Y 도망 → TTI가 줄지 않아 자연 판정이 오지 않는다.
            // 🔴 계약: 그래도 0.6초에 강제 확정되어야 한다.
            ThreatEventTracker tracker = NewTracker();
            List<ThreatBullet> bullets = new List<ThreatBullet>(1);

            Vec2 playerPos = Vec2.Zero;
            Vec2 bulletPos = new Vec2(0f, -3f);   // TTI = 3/6 = 0.5 → 즉시 트리거
            Vec2 bulletVel = new Vec2(0f, 6f);

            ThreatSample? resolved = null;
            for (int i = 0; i < 200 && !resolved.HasValue; i++)
            {
                bullets.Clear();
                bullets.Add(new ThreatBullet(11, bulletPos, bulletVel, false));

                IReadOnlyList<ThreatSample> samples = tracker.Tick(TICK, playerPos, bullets);
                if (samples.Count > 0) resolved = samples[0];

                playerPos = playerPos + new Vec2(0f, 6f * TICK);    // 탄과 같은 속도로 도망
                bulletPos = bulletPos + bulletVel * TICK;
            }

            IsTrue("TTI가 줄지 않아도 판정이 확정됨", resolved.HasValue);
            if (!resolved.HasValue) return;

            Approx("0.6초 창에서 강제 확정", resolved.Value.ResolveDelay, 0.6f, 0.02f);
            IsFalse("전후 이동만 했으므로 학습 표본 제외", resolved.Value.CountsAsLearningSample);
        }

        private static void TestThreatPicksShortestTimeToImpact()
        {
            // 🔴 계약: 동시 탄막에서는 TTI 최단 1발만 처리한다.
            ThreatEventTracker tracker = NewTracker();
            List<ThreatBullet> bullets = new List<ThreatBullet>(2)
            {
                new ThreatBullet(201, new Vec2(0f, -3f), new Vec2(0f, 6f), false),   // TTI 0.50
                new ThreatBullet(202, new Vec2(0f, -1.2f), new Vec2(0f, 6f), false)  // TTI 0.20
            };

            tracker.Tick(TICK, Vec2.Zero, bullets);
            IsTrue("TTI 최단(202)을 선택", tracker.ActiveWatchBulletId == 202);

            tracker.Tick(TICK, Vec2.Zero, bullets);
            IsTrue("추적 중에는 다른 탄으로 갈아타지 않는다", tracker.ActiveWatchBulletId == 202);
        }

        private static void TestThreatTriggersOncePerBullet()
        {
            // 🔴 계약: 탄환당 1회. 같은 탄이 계속 위협 범위에 있어도 재트리거 금지.
            ThreatEventTracker tracker = NewTracker();
            List<ThreatBullet> bullets = new List<ThreatBullet>(1)
            {
                new ThreatBullet(301, new Vec2(0f, -3f), new Vec2(0f, 6f), false)   // 위치 고정 → TTI 0.5 유지
            };

            int resolvedCount = 0;
            for (int i = 0; i < 200; i++)
            {
                resolvedCount += tracker.Tick(TICK, Vec2.Zero, bullets).Count;
            }

            IsTrue($"같은 탄은 딱 1번만 판정된다 (실제 {resolvedCount}회)", resolvedCount == 1);
            IsFalse("판정 후 재트리거되지 않는다", tracker.HasActiveWatch);
        }

        private static void TestThreatIgnoresDistantBullet()
        {
            // GDD 미명시 보조 규칙: 예상 근접거리가 크면 위협이 아니다.
            // 탄이 X=+10 지점을 지나 위로 날아간다 — TTI는 0.5지만 10유닛 옆이다.
            ThreatEventTracker tracker = NewTracker();
            List<ThreatBullet> bullets = new List<ThreatBullet>(1)
            {
                new ThreatBullet(401, new Vec2(10f, -3f), new Vec2(0f, 6f), false)
            };

            tracker.Tick(TICK, Vec2.Zero, bullets);
            IsFalse("10유닛 옆으로 지나가는 탄은 위협이 아니다", tracker.HasActiveWatch);
        }

        // ───────────────────────── 시뮬레이션 헬퍼 ─────────────────────────

        /// <summary>
        /// 탄 1발이 (0,-6)에서 +Y로 6u/s 접근. 트리거된 뒤부터 플레이어가 X축으로 이동한다.
        /// </summary>
        /// <param name="playerLateralSpeed">양수면 -X(탄 기준 왼쪽), 음수면 +X로 이동</param>
        private static ThreatSample? SimulateApproachingBullet(float playerLateralSpeed, bool forceHit)
        {
            ThreatEventTracker tracker = NewTracker();
            List<ThreatBullet> bullets = new List<ThreatBullet>(1);

            Vec2 playerPos = Vec2.Zero;
            Vec2 bulletPos = new Vec2(0f, -6f);
            Vec2 bulletVel = new Vec2(0f, 6f);
            bool triggered = false;

            for (int i = 0; i < 300; i++)
            {
                bullets.Clear();
                bullets.Add(new ThreatBullet(101, bulletPos, bulletVel, false));

                int hitId = ThreatEventTracker.NO_BULLET;
                if (forceHit && triggered && bulletPos.Y >= -0.5f) hitId = 101;

                IReadOnlyList<ThreatSample> samples = tracker.Tick(TICK, playerPos, bullets, hitId);
                if (samples.Count > 0) return samples[0];

                if (tracker.HasActiveWatch) triggered = true;
                if (triggered) playerPos = playerPos + new Vec2(-playerLateralSpeed * TICK, 0f);
                bulletPos = bulletPos + bulletVel * TICK;
            }
            return null;
        }

        // ───────────────────────── 역카운터 (§7.5) ─────────────────────────

        /// <summary>예측탄 1발을 접근시키는 시뮬레이션. 탄 진행 (0,1) 기준 -X 이동 = LEFT 회피.</summary>
        private static ThreatSample? SimulatePredictiveBullet(float playerLateralSpeed,
            DodgeDirection predictedDirection, bool forceHit, bool markPredictive = true)
        {
            ThreatEventTracker tracker = NewTracker();
            List<ThreatBullet> bullets = new List<ThreatBullet>(1);

            Vec2 playerPos = Vec2.Zero;
            Vec2 bulletPos = new Vec2(0f, -6f);
            Vec2 bulletVel = new Vec2(0f, 6f);
            bool triggered = false;

            for (int i = 0; i < 300; i++)
            {
                bullets.Clear();
                bullets.Add(new ThreatBullet(202, bulletPos, bulletVel, markPredictive, predictedDirection));

                int hitId = ThreatEventTracker.NO_BULLET;
                if (forceHit && triggered && bulletPos.Y >= -0.5f) hitId = 202;

                IReadOnlyList<ThreatSample> samples = tracker.Tick(TICK, playerPos, bullets, hitId);
                if (samples.Count > 0) return samples[0];

                if (tracker.HasActiveWatch) triggered = true;
                if (triggered) playerPos = playerPos + new Vec2(-playerLateralSpeed * TICK, 0f);
                bulletPos = bulletPos + bulletVel * TICK;
            }
            return null;
        }

        private static void TestCounterDodgeDetected()
        {
            // AI가 RIGHT를 예측 → 플레이어가 LEFT로 회피 성공 = 역카운터 (3조건 전부 충족)
            ThreatSample? s = SimulatePredictiveBullet(6f, DodgeDirection.Right, forceHit: false);
            IsTrue("예측탄 판정 확정", s.HasValue && s.Value.WasPredictive);
            IsTrue("예측 RIGHT + LEFT 회피 성공 → 역카운터 (§7.5)", s.HasValue && s.Value.IsCounterDodge);
        }

        private static void TestCounterRequiresOppositeDirection()
        {
            // 예측대로 움직였는데 우연히 피한 경우 — "읽고 깨뜨린 순간"이 아니다
            ThreatSample? s = SimulatePredictiveBullet(6f, DodgeDirection.Left, forceHit: false);
            IsTrue("예측 LEFT + LEFT 회피 → 역카운터 아님", s.HasValue && !s.Value.IsCounterDodge);
        }

        private static void TestCounterRequiresDodgeSuccess()
        {
            ThreatSample? s = SimulatePredictiveBullet(6f, DodgeDirection.Right, forceHit: true);
            IsTrue("피격이면 반대로 움직였어도 역카운터 아님", s.HasValue && !s.Value.IsCounterDodge);
        }

        private static void TestCounterRequiresDisplacement()
        {
            // 변위 0.3유닛 미달 — 제자리 회피는 방향 판정 자체가 무의미하다
            ThreatSample? s = SimulatePredictiveBullet(0.3f, DodgeDirection.Right, forceHit: false);
            IsTrue("변위 미달 회피 → 역카운터 아님", s.HasValue && !s.Value.IsCounterDodge);
        }

        private static void TestCounterRequiresPredictiveBullet()
        {
            // 일반탄을 확률표 반대로 피한 것은 집계하지 않는다 (§7.5 명시)
            ThreatSample? s = SimulatePredictiveBullet(6f, DodgeDirection.Right, forceHit: false,
                markPredictive: false);
            IsTrue("일반탄 회피 → 역카운터 아님", s.HasValue && !s.Value.IsCounterDodge);
        }

        // ───────────────────────── 프로파일러 (§6.4) ─────────────────────────

        /// <summary>1초를 60틱으로 나눠 흘리는 헬퍼. enemies가 null이면 빈 아레나.</summary>
        private static void TickSeconds(PlayStyleProfiler profiler, float seconds,
            Vec2 playerPosition, List<Vec2> enemies, Vec2 moveInput, bool isFiring)
        {
            int ticks = Mathf.RoundToInt(seconds / TICK);
            List<Vec2> list = enemies ?? new List<Vec2>();
            for (int i = 0; i < ticks; i++)
                profiler.Tick(TICK, playerPosition, list, moveInput, isFiring);
        }

        private static void TestProfilerInitialState()
        {
            PlayStyleProfiler profiler = new PlayStyleProfiler();
            Approx("프로파일러 초기 평균 교전 거리 = 0", profiler.AverageEngageDistance, 0f);
            Approx("프로파일러 초기 무빙샷 비율 = 0", profiler.MovingShotRatio, 0f);
            Approx("프로파일러 초기 직전 웨이브 = 표본 없음(-1)",
                profiler.LastWaveAverageEngageDistance, PlayStyleProfiler.NO_SAMPLE);
        }

        private static void TestProfilerAverageEngageDistance()
        {
            PlayStyleProfiler profiler = new PlayStyleProfiler();
            // 거리 3과 5의 적 2기 → 프레임 평균 4
            List<Vec2> enemies = new List<Vec2> { new Vec2(3f, 0f), new Vec2(0f, 5f) };
            TickSeconds(profiler, 1f, Vec2.Zero, enemies, Vec2.Zero, false);
            Approx("적 2기(거리 3, 5) → 평균 교전 거리 4", profiler.AverageEngageDistance, 4f, 0.01f);
        }

        private static void TestProfilerIgnoresEmptyArena()
        {
            PlayStyleProfiler profiler = new PlayStyleProfiler();
            List<Vec2> enemies = new List<Vec2> { new Vec2(6f, 0f) };
            TickSeconds(profiler, 1f, Vec2.Zero, enemies, Vec2.Zero, false);
            TickSeconds(profiler, 3f, Vec2.Zero, null, Vec2.Zero, false);   // 빈 아레나 3초
            Approx("빈 아레나 시간은 교전 거리에 미집계 (여전히 6)",
                profiler.AverageEngageDistance, 6f, 0.01f);
        }

        private static void TestProfilerMovingShotRatio()
        {
            PlayStyleProfiler profiler = new PlayStyleProfiler();
            // 발사 2초 중 1초만 이동 → 0.5
            TickSeconds(profiler, 1f, Vec2.Zero, null, new Vec2(1f, 0f), true);
            TickSeconds(profiler, 1f, Vec2.Zero, null, Vec2.Zero, true);
            // 발사하지 않는 이동은 분모에 안 들어간다
            TickSeconds(profiler, 5f, Vec2.Zero, null, new Vec2(1f, 0f), false);
            Approx("발사 2초 중 이동 1초 → 무빙샷 0.5", profiler.MovingShotRatio, 0.5f, 0.01f);
        }

        private static void TestProfilerFavoriteQuadrant()
        {
            PlayStyleProfiler profiler = new PlayStyleProfiler();
            TickSeconds(profiler, 2f, new Vec2(-5f, 3f), null, Vec2.Zero, false);   // NW
            TickSeconds(profiler, 1f, new Vec2(5f, -3f), null, Vec2.Zero, false);   // SE
            IsTrue("NW 2초 vs SE 1초 → 선호 구역 NW", profiler.FavoriteQuadrant == Quadrant.NW);
            Approx("NW 체류 비율 = 2/3", profiler.QuadrantRatio(Quadrant.NW), 2f / 3f, 0.01f);
        }

        private static void TestProfilerDirectionHistogram()
        {
            PlayStyleProfiler profiler = new PlayStyleProfiler();
            TickSeconds(profiler, 1f, Vec2.Zero, null, new Vec2(1f, 0f), false);    // 동(0)
            TickSeconds(profiler, 1f, Vec2.Zero, null, new Vec2(1f, 1f), false);    // 북동(1)
            Approx("동쪽 이동 비율 = 0.5", profiler.DirectionRatio(0), 0.5f, 0.01f);
            Approx("북동 이동 비율 = 0.5", profiler.DirectionRatio(1), 0.5f, 0.01f);
            Approx("서쪽 이동 비율 = 0", profiler.DirectionRatio(4), 0f);
        }

        private static void TestProfilerWaveSnapshot()
        {
            PlayStyleProfiler profiler = new PlayStyleProfiler();
            List<Vec2> far = new List<Vec2> { new Vec2(8f, 0f) };
            List<Vec2> near = new List<Vec2> { new Vec2(2f, 0f) };

            TickSeconds(profiler, 1f, Vec2.Zero, far, Vec2.Zero, false);
            profiler.OnWaveEnded();
            Approx("웨이브 1 종료 → 직전 웨이브 평균 8", profiler.LastWaveAverageEngageDistance, 8f, 0.01f);

            TickSeconds(profiler, 1f, Vec2.Zero, near, Vec2.Zero, false);
            profiler.OnWaveEnded();
            Approx("웨이브 2 종료 → 직전 웨이브 평균 2 (웨이브별 독립)",
                profiler.LastWaveAverageEngageDistance, 2f, 0.01f);
            Approx("런 전체 평균은 5 (두 웨이브 시간 동일)", profiler.AverageEngageDistance, 5f, 0.01f);

            profiler.OnWaveEnded();   // 표본 없는 웨이브
            Approx("교전 없는 웨이브 → 표본 없음(-1)",
                profiler.LastWaveAverageEngageDistance, PlayStyleProfiler.NO_SAMPLE);
        }

        private static void TestProfilerReset()
        {
            PlayStyleProfiler profiler = new PlayStyleProfiler();
            List<Vec2> enemies = new List<Vec2> { new Vec2(4f, 0f) };
            TickSeconds(profiler, 1f, new Vec2(-3f, 2f), enemies, new Vec2(1f, 0f), true);
            profiler.OnWaveEnded();
            profiler.Reset();
            Approx("리셋 후 평균 교전 거리 = 0", profiler.AverageEngageDistance, 0f);
            Approx("리셋 후 무빙샷 = 0", profiler.MovingShotRatio, 0f);
            Approx("리셋 후 직전 웨이브 = 표본 없음(-1)",
                profiler.LastWaveAverageEngageDistance, PlayStyleProfiler.NO_SAMPLE);
        }

        private static DodgePredictor NewPredictor() => new DodgePredictor(PredictorSettings.Default);

        private static ThreatEventTracker NewTracker() =>
            new ThreatEventTracker(ThreatDetectionSettings.Default);

        // ───────────────────────── 단정 헬퍼 ─────────────────────────

        private static void Approx(string label, float actual, float expected, float tolerance = EPS)
        {
            bool ok = Mathf.Abs(actual - expected) <= tolerance;
            Record(ok, ok ? label : $"{label} — 기대 {expected:F4}, 실제 {actual:F4}");
        }

        private static void IsTrue(string label, bool condition) => Record(condition, label);

        private static void IsFalse(string label, bool condition) => Record(!condition, label);

        private static void Record(bool ok, string label)
        {
            if (ok) _passed++;
            else _failed++;
            _log.AppendLine((ok ? "  PASS  " : "  FAIL  ") + label);
        }
    }
}
