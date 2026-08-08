// 연출 확인용 디버그 메뉴 — 실제 조건(HIGH + 예측탄 회피)을 만들지 않고도
// PREDICTION FAILED 연출·히트스톱을 즉시 트리거해 본다.
using UnityEditor;
using UnityEngine;
using Luddite.AIBrain;
using Luddite.Core;

namespace Luddite.EditorTools
{
    public static class GameFeelDebugTools
    {
        [MenuItem("Luddite/Dev/전투 상태로 진입 (플레이 모드)")]
        public static void ForceCombat()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[GameFeelDebug] 플레이 모드에서만 실행 가능");
                return;
            }

            GameManager manager = Object.FindFirstObjectByType<GameManager>();
            if (manager == null)
            {
                Debug.LogError("[GameFeelDebug] GameManager 없음");
                return;
            }

            // 어느 상태에서든 Combat까지 유효 전환만 밟아 도달한다
            if (manager.State == GameState.Paused) manager.ResumeFromPause();
            if (manager.State == GameState.Result) manager.ReturnToTitle();
            if (manager.State == GameState.WaveInterval) manager.ContinueToNextWave();
            if (manager.State == GameState.Title)
            {
                manager.StartRun();
                manager.SelectMajor(Major.LiberalArts);
            }

            Debug.Log($"[GameFeelDebug] 현재 상태: {manager.State}");
        }

        [MenuItem("Luddite/Dev/보스 웨이브로 점프 (플레이 모드)")]
        public static void JumpToBossWave()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[GameFeelDebug] 플레이 모드에서만 실행 가능");
                return;
            }

            WaveManager waveManager = Object.FindFirstObjectByType<WaveManager>();
            if (waveManager == null)
            {
                Debug.LogError("[GameFeelDebug] WaveManager 없음");
                return;
            }

            ForceCombat();   // Combat이 아니면 유효 전환으로 진입부터
            waveManager.DebugJumpToWave(waveManager.TotalWaves);
        }

        [MenuItem("Luddite/Dev/PREDICTION FAILED 연출 테스트 (플레이 모드)")]
        public static void TriggerPredictionFailed()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[GameFeelDebug] 플레이 모드에서만 실행 가능");
                return;
            }

            GameManager manager = Object.FindFirstObjectByType<GameManager>();
            if (manager != null && manager.State != GameState.Combat)
            {
                Debug.LogWarning($"[GameFeelDebug] Combat 상태가 아님 ({manager.State}) — HUD가 꺼져 있어 연출이 보이지 않는다");
            }

            GameEvents.RaisePredictionFailed(
                new PredictionFailedReport(DodgeDirection.Left, 0.82f, 0.64f));
            Debug.Log("[GameFeelDebug] PREDICTION FAILED 트리거 (LEFT 82% → 64%)");
        }
    }
}
