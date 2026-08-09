using System;
using UnityEngine;

namespace Luddite.Core
{
    /// <summary>
    /// 프로젝트의 <b>유일한</b> 정적 이벤트 버스 (CLAUDE.md 규칙 4).
    /// UnityEvent 남발과 DI 프레임워크 없이 시스템 간 결합을 끊는 용도다.
    ///
    /// <para>
    /// 여기에 이벤트를 추가할 때 기준: <b>발행자와 구독자가 서로를 알면 안 되는 관계</b>인가.
    /// 그렇지 않다면 직접 참조나 인터페이스가 낫다 — 이 버스가 전역 결합의 하수구가 되면
    /// 규칙 4의 목적이 뒤집힌다.
    /// </para>
    /// </summary>
    public static class GameEvents
    {
        /// <summary>
        /// 적 탄환이 플레이어 <b>몸에 닿았다</b>. 인자는 탄환의 <c>GetInstanceID()</c>.
        /// 데미지 발생 여부와 무관하다 — 무적 중 관통도 §7.1의 "피격"이다 (D2 해석 확정).
        /// 관통을 회피 성공으로 두면 표본이 오염되기 때문.
        /// <para>
        /// 발행: <c>Luddite.Combat.Projectile</c> — 자기 ID를 아는 유일한 주체.
        /// 구독: <see cref="AIBrainRunner"/> — 어느 탄에 맞았는지 알아야 §7.1 위기 이벤트를
        /// "회피 실패"로 확정할 수 있다. 투사체가 AIBrain을 알 필요는 없으므로 버스를 경유한다.
        /// </para>
        /// <para>접촉 데미지는 여기에 해당하지 않는다 — §7.1의 원시 단위는 <b>탄환</b>이다.</para>
        /// </summary>
        public static event Action<int> ProjectileHitPlayer;

        public static void RaiseProjectileHitPlayer(int projectileInstanceId)
        {
            ProjectileHitPlayer?.Invoke(projectileInstanceId);
        }

        /// <summary>
        /// 게임 상태가 전환됐다. 인자는 (이전, 다음).
        /// <para>
        /// 발행: <see cref="GameManager"/> — 전환 규칙의 유일한 소유자.
        /// 구독: UI 화면 라우팅, 플레이어 입력 게이트 등 "지금이 어느 상태인지"만 알면 되는
        /// 모든 시스템. GameManager를 직접 참조하면 전환 API까지 노출되므로 버스를 경유한다.
        /// </para>
        /// </summary>
        public static event Action<GameState, GameState> GameStateChanged;

        public static void RaiseGameStateChanged(GameState previous, GameState next)
        {
            GameStateChanged?.Invoke(previous, next);
        }

        /// <summary>
        /// 플레이어가 사망했다 (HP 0).
        /// <para>
        /// 발행: <c>Luddite.Player.PlayerHealth</c>. 구독: <see cref="GameManager"/> —
        /// Result(패배) 전환 (§1.4). 체력 컴포넌트가 게임 플로우를 알 필요는 없다.
        /// </para>
        /// </summary>
        public static event Action PlayerDied;

        public static void RaisePlayerDied()
        {
            PlayerDied?.Invoke();
        }

        /// <summary>
        /// 새 런이 시작됐다 (전공 선택 확정 → Combat 진입 직전).
        /// <para>
        /// 발행: <see cref="GameManager"/>. 구독: 런 단위 상태를 가진 시스템 전부 —
        /// <see cref="AIBrainRunner"/>(모델 리셋), <c>PlayerHealth</c>(체력 복원),
        /// D4의 WaveManager(웨이브 1부터). 리셋 대상이 늘 때마다 GameManager를 고치지
        /// 않기 위해 버스를 경유한다.
        /// </para>
        /// </summary>
        public static event Action RunStarted;

        public static void RaiseRunStarted()
        {
            RunStarted?.Invoke();
        }

        /// <summary>
        /// 웨이브가 전멸로 종료됐다 (§6.1 — 전멸형 종료가 🔴 계약). 인자는 끝난 웨이브 번호(1-based).
        /// <para>
        /// 발행: <see cref="WaveManager"/>. 구독: <see cref="AIBrainRunner"/> —
        /// §7.2 "매 웨이브 종료 시 관측 카운트 ×0.8 감쇠"의 트리거.
        /// 웨이브 시스템이 AIBrain을 알 필요가 없도록 버스를 경유한다.
        /// </para>
        /// </summary>
        public static event Action<int> WaveEnded;

        public static void RaiseWaveEnded(int waveNumber)
        {
            WaveEnded?.Invoke(waveNumber);
        }

        /// <summary>
        /// 플레이어가 예측탄을 회피했다 — "게임에서 가장 중요한 1초" (§10.3).
        /// <para>
        /// 발행: <see cref="AIBrainRunner"/> — 예측탄 위기 이벤트가 회피 성공으로 확정되는 곳.
        /// 구독: <see cref="GameManager"/>(히트스톱), <c>Luddite.UI.PredictionFailedOverlay</c>(플래시·문구).
        /// 로직(판정)과 비주얼(연출)이 서로를 모른 채 붙는 접점이라 버스를 경유한다.
        /// </para>
        /// </summary>
        public static event Action<PredictionFailedReport> PredictionFailed;

        public static void RaisePredictionFailed(PredictionFailedReport report)
        {
            PredictionFailed?.Invoke(report);
        }

        /// <summary>
        /// 보스가 P2 「PATTERN: YOU」로 전환됐다 (§9 — HP 60%, 전환 무적 시작 시점).
        /// <para>
        /// 발행: <c>Luddite.Enemies.BossLLM</c>. 구독: <c>Luddite.UI.BossPhaseOverlay</c> —
        /// "USER MODEL LOADED / COPY COMPLETE / PATTERN: YOU" 연출. 보스가 UI를 알 필요가
        /// 없도록 버스를 경유한다 (PredictionFailed와 같은 로직×비주얼 접점).
        /// </para>
        /// </summary>
        public static event Action BossPhaseTwoStarted;

        public static void RaiseBossPhaseTwoStarted()
        {
            BossPhaseTwoStarted?.Invoke();
        }

        /// <summary>
        /// 정적 이벤트는 도메인 리로드를 끄고 플레이 모드를 재시작하면 죽은 구독자를 물고 있다.
        /// 플레이 모드 진입 시점에 한 번 비워 그 사고를 막는다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayModeStart()
        {
            ProjectileHitPlayer = null;
            GameStateChanged = null;
            PlayerDied = null;
            RunStarted = null;
            PredictionFailed = null;
            WaveEnded = null;
            BossPhaseTwoStarted = null;
        }
    }
}
