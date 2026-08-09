// 보스 P2 스모크 — 배선·수치·이벤트 경로를 코드로 직접 검사 (MCP 입력 주입 불가 우회, CLAUDE.md 세션 규칙).
// 실플레이 검증(전환 연출·거리 복제 체감·장판 회피)은 사람 몫으로 남는다.
using UnityEditor;
using UnityEngine;
using Luddite.Core;
using Luddite.Data;
using Luddite.Enemies;
using Luddite.UI;

namespace Luddite.EditorTools
{
    public static class BossP2SmokeTest
    {
        private const string PREFAB_PATH = "Assets/_Project/Prefabs/BossLLM.prefab";
        private const string CONFIG_PATH = "Assets/_Project/SO/BossConfig_Default.asset";

        private static int _passed;
        private static int _failed;

        [MenuItem("Luddite/Dev/보스 P2 스모크")]
        public static void RunSmoke()
        {
            _passed = 0;
            _failed = 0;

            CheckPrefab();
            CheckConfig();
            CheckSceneOverlay();
            CheckEventPath();

            string summary = $"[BossP2Smoke] {_passed + _failed}건 중 통과 {_passed} / 실패 {_failed}";
            if (_failed > 0) Debug.LogError(summary + " — 실패 항목을 먼저 해결할 것");
            else Debug.Log(summary + " ✅");
        }

        private static void CheckPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (!Check(prefab != null, "BossLLM.prefab 존재")) return;

            BossLLM boss = prefab.GetComponent<BossLLM>();
            Check(boss != null, "BossLLM 컴포넌트");

            EliteModifier elite = prefab.GetComponent<EliteModifier>();
            if (Check(elite != null, "EliteModifier 부착 (P2 예측탄 + AI 패널 표시 조건)"))
            {
                SerializedObject so = new SerializedObject(elite);
                Check(so.FindProperty("_config").objectReferenceValue != null, "EliteModifier._config 배선");
                Check(so.FindProperty("_gun").objectReferenceValue != null, "EliteModifier._gun 배선");
                Check(so.FindProperty("_aimLine").objectReferenceValue != null, "EliteModifier._aimLine 배선");
                Check(so.FindProperty("_targetMarker").objectReferenceValue != null, "EliteModifier._targetMarker 배선");
            }

            EnemyGun gun = prefab.GetComponentInChildren<EnemyGun>(true);
            if (Check(gun != null, "EnemyGun 자식 존재"))
            {
                SerializedObject so = new SerializedObject(gun);
                Check(so.FindProperty("_stats").objectReferenceValue != null, "EnemyGun._stats 배선");
                Check(so.FindProperty("_projectilePrefab").objectReferenceValue != null, "EnemyGun._projectilePrefab 배선");
            }

            if (boss != null)
            {
                SerializedObject so = new SerializedObject(boss);
                Check(so.FindProperty("_zoneSprite").objectReferenceValue != null, "BossLLM._zoneSprite 배선 (장판·오라)");
            }

            // D6 세션 2 계열 재발 방지: 새 텔레그래프 자식이 Default 레이어에 남아 있으면 바닥 밑에 깔린다
            foreach (string childName in new[] { "AimLine", "TargetMarker", "TrailTemplate" })
            {
                Transform child = prefab.transform.Find(childName);
                if (!Check(child != null, $"{childName} 자식 존재")) continue;

                Renderer renderer = child.GetComponent<Renderer>();
                if (renderer == null) continue;   // TrailRenderer 등도 Renderer라 여기 걸린다
                bool notDefault = renderer.sortingLayerID != 0 || SortingLayer.NameToID("VFX") == 0;
                Check(notDefault, $"{childName}이 Default 정렬 레이어가 아님");
            }
        }

        private static void CheckConfig()
        {
            BossConfigSO config = AssetDatabase.LoadAssetAtPath<BossConfigSO>(CONFIG_PATH);
            if (!Check(config != null, "BossConfig_Default 존재")) return;

            // §9가 명시한 확정값 — 이탈은 계약 아님(밸런스)이지만 초기값 회귀는 잡는다
            Check(Mathf.Approximately(config.ZoneTelegraph, 2f), $"장판 텔레그래프 2s (§9) — 현재 {config.ZoneTelegraph}");
            Check(Mathf.Approximately(config.ZoneActiveDuration, 3f), $"장판 지속 3s (§9) — 현재 {config.ZoneActiveDuration}");
            Check(Mathf.Approximately(config.ZoneDamagePerSecond, 8f), $"장판 데미지 8/s (§9) — 현재 {config.ZoneDamagePerSecond}");
            Check(Mathf.Approximately(config.P2MoveSpeed, 3.5f), $"P2 이동 3.5 (§5.1) — 현재 {config.P2MoveSpeed}");
            Check(config.ZoneInterval > config.ZoneTelegraph + config.ZoneActiveDuration - 0.01f,
                "장판 주기 ≥ 텔레그래프+지속 (동시 2장판 방지 초안)");
        }

        private static void CheckSceneOverlay()
        {
            GameObject canvas = GameObject.Find("GameScreensCanvas");
            Transform hudPanel = canvas != null ? canvas.transform.Find("HudPanel") : null;
            Transform overlayRoot = hudPanel != null ? hudPanel.Find("BossPhaseOverlay") : null;
            if (!Check(overlayRoot != null, "씬에 BossPhaseOverlay 존재 (없으면 'Luddite/Setup/보스 P2 컴포넌트 보장' 실행)"))
                return;

            BossPhaseOverlay overlay = overlayRoot.GetComponent<BossPhaseOverlay>();
            if (Check(overlay != null, "BossPhaseOverlay 컴포넌트"))
            {
                SerializedObject so = new SerializedObject(overlay);
                Check(so.FindProperty("_content").objectReferenceValue != null, "BossPhaseOverlay._content 배선");
                Check(so.FindProperty("_mainText").objectReferenceValue != null, "BossPhaseOverlay._mainText 배선");
            }
        }

        /// <summary>이벤트 버스 경로 — 발행 → 수신이 동기로 이어지는지 (순수 C#, 플레이 모드 불필요).</summary>
        private static void CheckEventPath()
        {
            bool received = false;
            System.Action handler = () => received = true;
            GameEvents.BossPhaseTwoStarted += handler;
            GameEvents.RaiseBossPhaseTwoStarted();
            GameEvents.BossPhaseTwoStarted -= handler;
            Check(received, "GameEvents.BossPhaseTwoStarted 발행 → 수신");
        }

        private static bool Check(bool condition, string label)
        {
            if (condition) { _passed++; return true; }
            _failed++;
            Debug.LogError($"[BossP2Smoke] 실패: {label}");
            return false;
        }

        // ── 플레이 모드 보조 (사람 검증 절차의 도구) ──

        /// <summary>보스 HP를 P2 전환 직전(61%)까지 깎는다 — 한두 발 더 맞히면 전환을 눈으로 확인.</summary>
        [MenuItem("Luddite/Dev/보스 HP를 P2 직전(61%)으로")]
        public static void DamageBossToNearPhaseTwo()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[BossP2Smoke] 플레이 모드 전용"); return; }

            BossLLM boss = Object.FindFirstObjectByType<BossLLM>();
            if (boss == null) { Debug.LogWarning("[BossP2Smoke] 보스 없음 — '보스 웨이브로 점프' 먼저"); return; }

            if (!boss.CanBeDamaged)
            {
                Debug.LogWarning("[BossP2Smoke] 보스가 피격 불가 상태 (스폰 텔레그래프 중?) — 잠시 후 재시도");
                return;
            }

            float targetHp = boss.MaxHp * 0.61f;
            float amount = boss.Hp - targetHp;
            if (amount <= 0f) { Debug.Log("[BossP2Smoke] 이미 61% 이하"); return; }

            boss.TakeDamage(amount, Vector2.right);
            Debug.Log($"[BossP2Smoke] 보스 HP {boss.Hp:F0}/{boss.MaxHp:F0} — 이제 몇 발이면 P2 전환");
        }

        [MenuItem("Luddite/Dev/보스 P2 상태 덤프")]
        public static void DumpBossState()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[BossP2Smoke] 플레이 모드 전용"); return; }

            BossLLM boss = Object.FindFirstObjectByType<BossLLM>();
            AIBrainRunner brain = Object.FindFirstObjectByType<AIBrainRunner>();
            if (boss == null) { Debug.LogWarning("[BossP2Smoke] 보스 없음"); return; }

            Debug.Log($"[BossP2Smoke] {boss.StateName} | HP {boss.Hp:F0}/{boss.MaxHp:F0} " +
                      $"| P2활성={boss.IsPhaseTwoActive} | " +
                      (brain != null
                          ? $"평균거리={brain.AverageEngageDistance:F1} 구역={brain.FavoriteQuadrant} HIGH={brain.IsHighConfidence}"
                          : "AIBrain 없음"));
        }
    }
}
