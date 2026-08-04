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
        /// 적 탄환이 플레이어에게 명중했다. 인자는 탄환의 <c>GetInstanceID()</c>.
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
        /// 정적 이벤트는 도메인 리로드를 끄고 플레이 모드를 재시작하면 죽은 구독자를 물고 있다.
        /// 플레이 모드 진입 시점에 한 번 비워 그 사고를 막는다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayModeStart()
        {
            ProjectileHitPlayer = null;
        }
    }
}
