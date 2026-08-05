// GameState 전환 그래프 스모크 — 플레이 모드에서 전환 API를 직접 호출해 검증한다.
// MCP는 키보드·마우스를 주입할 수 없고 백그라운드에서는 프레임도 돌지 않지만 (D1 세션 5),
// 상태 전환은 전부 동기 호출이므로 프레임 없이도 상태·timeScale·패널 라우팅을 검증할 수 있다.
// 프레임·입력이 필요한 나머지(ESC 토글, BossIntro 2초 자동 복귀, 실제 버튼 클릭)는 사람 검증.
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Luddite.Core;
using Luddite.UI;

namespace Luddite.EditorTools
{
    public static class GameStateSmokeTest
    {
        private const string REPORT_PATH = "Logs/gamestate-smoke.txt"; // gitignore 대상 (Logs/)

        private static int _passed;
        private static int _failed;
        private static StringBuilder _report;

        [MenuItem("Luddite/Dev/GameState 전환 스모크 (플레이 모드)")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[GameStateSmoke] 플레이 모드에서만 실행 가능");
                return;
            }

            GameManager manager = Object.FindFirstObjectByType<GameManager>();
            GameScreens screens = Object.FindFirstObjectByType<GameScreens>();
            if (manager == null || screens == null)
            {
                Debug.LogError("[GameStateSmoke] GameManager 또는 GameScreens가 씬에 없음 — " +
                               "Luddite/Setup/GameState 골격을 씬에 보장 후 재시도");
                return;
            }

            if (manager.State != GameState.Title)
            {
                Debug.LogWarning($"[GameStateSmoke] 시작 상태가 Title이 아님 ({manager.State}) — " +
                                 "플레이 모드를 재진입한 직후 실행하세요");
                return;
            }

            _passed = 0;
            _failed = 0;
            _report = new StringBuilder();
            _report.AppendLine("===== GameState 전환 스모크 (GDD §1) =====");

            Panels panels = ReadPanels(screens);

            // 초기 상태
            Check(manager.State == GameState.Title, "초기 상태 = Title");
            Check(Mathf.Approximately(Time.timeScale, 0f), "Title에서 timeScale = 0");
            CheckPanels(panels, panels.Title, "Title 패널만 활성");

            // 유효하지 않은 전환은 무시된다
            manager.SelectMajor(Major.Science);
            Check(manager.State == GameState.Title, "Title에서 SelectMajor는 무시 (경고 후 상태 유지)");
            manager.EndRun(true);
            Check(manager.State == GameState.Title, "Title에서 EndRun은 무시");

            // 정상 플로우: Title → MajorSelect → Combat
            manager.StartRun();
            Check(manager.State == GameState.MajorSelect, "StartRun → MajorSelect");
            CheckPanels(panels, panels.MajorSelect, "MajorSelect 패널만 활성");

            manager.SelectMajor(Major.Science);
            Check(manager.State == GameState.Combat, "SelectMajor → Combat");
            Check(manager.SelectedMajor == Major.Science, "선택 전공 = Science 보존");
            Check(Mathf.Approximately(Time.timeScale, 1f), "Combat에서 timeScale = 1");
            CheckPanels(panels, null, "Combat에서는 모든 패널 비활성 (HUD는 D3)");

            // Combat ↔ WaveInterval
            manager.BeginWaveInterval();
            Check(manager.State == GameState.WaveInterval, "BeginWaveInterval → WaveInterval");
            Check(Mathf.Approximately(Time.timeScale, 0f), "WaveInterval에서 timeScale = 0 (완전 일시정지)");
            CheckPanels(panels, panels.WaveInterval, "WaveInterval 패널만 활성");

            manager.ContinueToNextWave();
            Check(manager.State == GameState.Combat, "ContinueToNextWave → Combat");

            // 사망 → Result(패배). PlayerDied는 이벤트 버스 경유이므로 여기서 직접 발행해 경로를 검증한다
            GameEvents.RaisePlayerDied();
            Check(manager.State == GameState.Result, "PlayerDied → Result");
            Check(!manager.RunWon, "사망 패배 → RunWon = false");
            CheckPanels(panels, panels.Result, "Result 패널만 활성");
            Check(panels.ResultMessage != null && panels.ResultMessage.text.Contains("REPLACED"),
                "패배 메시지 표시 (§1.4)");

            manager.ReturnToTitle();
            Check(manager.State == GameState.Title, "Result → ReturnToTitle → Title");

            // 승리 경로
            manager.StartRun();
            manager.SelectMajor(Major.LiberalArts);
            manager.EndRun(true);
            Check(manager.State == GameState.Result, "EndRun(true) → Result");
            Check(manager.RunWon, "보스 격파 승리 → RunWon = true");
            Check(panels.ResultMessage != null && panels.ResultMessage.text.Contains("24 HOURS"),
                "승리 메시지 표시 (§1.4)");

            manager.ReturnToTitle();

            // BossIntro 진입 (2초 자동 복귀는 프레임 필요 → 사람 검증)
            manager.StartRun();
            manager.SelectMajor(Major.Arts);
            manager.BeginBossIntro();
            Check(manager.State == GameState.BossIntro, "BeginBossIntro → BossIntro");
            CheckPanels(panels, panels.BossIntro, "BossIntro 패널만 활성");
            _report.AppendLine("(주의) 스모크는 BossIntro 상태로 끝난다 — 에디터가 틱을 돌면 2초 후 Combat 자동 복귀");

            string summary = $"[GameStateSmoke] 결과: {_passed} 통과 / {_failed} 실패 — 상세: {REPORT_PATH}";
            Directory.CreateDirectory(Path.GetDirectoryName(REPORT_PATH));
            File.WriteAllText(REPORT_PATH, _report.ToString(), Encoding.UTF8);

            if (_failed > 0) Debug.LogError(summary + "\n" + _report);
            else Debug.Log(summary + "\n" + _report);
        }

        private struct Panels
        {
            public GameObject Title, MajorSelect, WaveInterval, BossIntro, Result, Pause;
            public TMP_Text ResultMessage;
        }

        /// <summary>GameScreens의 직렬화 필드에서 패널 참조를 읽는다 (비활성 오브젝트는 Find로 못 찾으므로).</summary>
        private static Panels ReadPanels(GameScreens screens)
        {
            SerializedObject so = new SerializedObject(screens);
            return new Panels
            {
                Title = so.FindProperty("_titlePanel").objectReferenceValue as GameObject,
                MajorSelect = so.FindProperty("_majorSelectPanel").objectReferenceValue as GameObject,
                WaveInterval = so.FindProperty("_waveIntervalPanel").objectReferenceValue as GameObject,
                BossIntro = so.FindProperty("_bossIntroPanel").objectReferenceValue as GameObject,
                Result = so.FindProperty("_resultPanel").objectReferenceValue as GameObject,
                Pause = so.FindProperty("_pausePanel").objectReferenceValue as GameObject,
                ResultMessage = so.FindProperty("_resultMessage").objectReferenceValue as TMP_Text,
            };
        }

        /// <summary>expectedActive만 켜져 있고 나머지 패널은 전부 꺼져 있는지. null이면 전부 꺼져 있어야 한다.</summary>
        private static void CheckPanels(Panels panels, GameObject expectedActive, string label)
        {
            bool ok =
                IsActiveExpected(panels.Title, expectedActive) &&
                IsActiveExpected(panels.MajorSelect, expectedActive) &&
                IsActiveExpected(panels.WaveInterval, expectedActive) &&
                IsActiveExpected(panels.BossIntro, expectedActive) &&
                IsActiveExpected(panels.Result, expectedActive) &&
                IsActiveExpected(panels.Pause, expectedActive);
            Check(ok, label);
        }

        private static bool IsActiveExpected(GameObject panel, GameObject expectedActive)
        {
            if (panel == null) return false; // 배선 누락도 실패로 잡는다
            return panel.activeSelf == (panel == expectedActive);
        }

        private static void Check(bool condition, string label)
        {
            if (condition) _passed++;
            else _failed++;
            _report.AppendLine($"{(condition ? "PASS" : "FAIL")}  {label}");
        }
    }
}
