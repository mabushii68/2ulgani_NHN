// AIBrain 런타임 디버그 도구. 웨이브 시스템·업그레이드 UI가 없는 D2 단계에서
// 감쇠·업그레이드 효과를 사람이 직접 확인할 수 있게 하는 [MenuItem] 모음.
// TODO(D4): WaveManager와 업그레이드 카드가 붙으면 이 도구의 존재 이유가 줄어든다.
using UnityEditor;
using UnityEngine;
using Luddite.Core;

namespace Luddite.EditorTools
{
    public static class AIBrainDebugTools
    {
        [MenuItem("Luddite/AIBrain/상태 덤프", priority = 0)]
        public static void DumpState()
        {
            AIBrainRunner runner = FindRunner();
            if (runner == null) return;
            Debug.Log("[AIBrain 상태] " + runner.DescribeState());
        }

        [MenuItem("Luddite/AIBrain/웨이브 감쇠 강제 (×0.8)", priority = 1)]
        public static void ForceWaveDecay()
        {
            AIBrainRunner runner = FindRunner();
            if (runner == null) return;
            runner.OnWaveEnded();
        }

        [MenuItem("Luddite/AIBrain/업그레이드 — 행동교정 (×0.2)", priority = 2)]
        public static void ApplyBehaviourCorrection()
        {
            AIBrainRunner runner = FindRunner();
            if (runner == null) return;
            runner.ApplyBehaviourCorrection();
        }

        [MenuItem("Luddite/AIBrain/업그레이드 — 논문조작 (가짜 표본 8)", priority = 3)]
        public static void ApplyDataFabrication()
        {
            AIBrainRunner runner = FindRunner();
            if (runner == null) return;
            runner.ApplyDataFabrication();
        }

        [MenuItem("Luddite/AIBrain/런 초기화", priority = 4)]
        public static void ResetRun()
        {
            AIBrainRunner runner = FindRunner();
            if (runner == null) return;
            runner.ResetRun();
            Debug.Log("[AIBrain] 런 초기화 완료 → " + runner.DescribeState());
        }

        private static AIBrainRunner FindRunner()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[AIBrain] 플레이 모드에서 실행할 것 — 런타임 상태가 필요합니다");
                return null;
            }

            AIBrainRunner runner = Object.FindFirstObjectByType<AIBrainRunner>();
            if (runner == null) Debug.LogError("[AIBrain] 씬에 AIBrainRunner가 없습니다");
            return runner;
        }
    }
}
