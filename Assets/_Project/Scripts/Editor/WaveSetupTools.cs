// 웨이브 시스템 에셋·씬 빌더 — 멱등(이미 있으면 값 보존).
// GDD §6.2 구성표를 WaveConfigSO ×7로 생성하고, WaveManager를 씬에 배선하며,
// 웨이브 스폰으로 대체되는 테스트 배치 적들을 씬에서 제거한다 (각 배치 도구의 "제거할 것" 이행).
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Luddite.Core;
using Luddite.Data;
using Luddite.Enemies;
using Luddite.UI;

namespace Luddite.EditorTools
{
    public static class WaveSetupTools
    {
        private const string MAIN_SCENE_PATH = "Assets/_Project/Scenes/Main.unity";
        private const string SYSTEM_CONFIG_PATH = "Assets/_Project/SO/WaveSystemConfig_Default.asset";
        private const string WAVE_CONFIG_PATH_FORMAT = "Assets/_Project/SO/WaveConfig_{0}.asset";

        private const string CHATBOT_PREFAB = "Assets/_Project/Prefabs/ChatbotDrone.prefab";
        private const string PAINTER_PREFAB = "Assets/_Project/Prefabs/PainterBot.prefab";
        private const string CODER_PREFAB = "Assets/_Project/Prefabs/CoderBot.prefab";
        private const string ELITE_PREFAB = "Assets/_Project/Prefabs/EliteDrone.prefab";

        /// <summary>웨이브 스폰으로 대체되어 제거할 테스트 배치물 (각 Setup 도구의 TODO 이행).</summary>
        private static readonly string[] TEST_ENEMY_NAMES =
        {
            "ChatbotDrone_1", "ChatbotDrone_2", "ChatbotDrone_3",
            "PainterBot_Test", "CoderBot_Test", "EliteDrone_Test",
        };

        [MenuItem("Luddite/Setup/웨이브 시스템 보장 (§6)")]
        public static void EnsureWaveSystem()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[WaveSetup] 플레이 모드에서는 씬을 편집하지 않는다");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MAIN_SCENE_PATH)
            {
                Debug.LogError($"[WaveSetup] 활성 씬이 Main.unity가 아니다 (현재: {scene.path})");
                return;
            }

            EnemyBase chatbot = LoadEnemy(CHATBOT_PREFAB);
            EnemyBase painter = LoadEnemy(PAINTER_PREFAB);
            EnemyBase coder = LoadEnemy(CODER_PREFAB);
            EnemyBase elite = LoadEnemy(ELITE_PREFAB);
            if (chatbot == null || painter == null || coder == null || elite == null)
            {
                Debug.LogError("[WaveSetup] 적 프리팹 누락 — 각 Setup 메뉴를 먼저 실행하세요");
                return;
            }

            WaveSystemConfigSO systemConfig = EnsureSystemConfig();

            // §6.2 구성표 — (챗봇, 그림봇, 코딩봇, 엘리트)
            WaveConfigSO[] waves =
            {
                EnsureWave(1, false, (chatbot, 5)),
                EnsureWave(2, false, (chatbot, 8)),
                EnsureWave(3, false, (chatbot, 6), (painter, 4), (elite, 1)),
                EnsureWave(4, false, (chatbot, 6), (painter, 4), (coder, 2), (elite, 1)),
                EnsureWave(5, false, (chatbot, 6), (painter, 4), (coder, 4), (elite, 2)),
                EnsureWave(6, false, (chatbot, 6), (painter, 6), (coder, 4), (elite, 2)),
                EnsureWave(7, true),
            };

            // WaveManager 배선
            GameObject host = GameObject.Find("WaveManager");
            if (host == null) host = new GameObject("WaveManager");
            WaveManager manager = host.GetComponent<WaveManager>();
            if (manager == null) manager = host.AddComponent<WaveManager>();

            GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
            SerializedObject managerSo = new SerializedObject(manager);
            managerSo.FindProperty("_systemConfig").objectReferenceValue = systemConfig;
            managerSo.FindProperty("_gameManager").objectReferenceValue = gameManager;
            SerializedProperty wavesProperty = managerSo.FindProperty("_waves");
            wavesProperty.arraySize = waves.Length;
            for (int i = 0; i < waves.Length; i++)
                wavesProperty.GetArrayElementAtIndex(i).objectReferenceValue = waves[i];
            managerSo.ApplyModifiedPropertiesWithoutUndo();

            // HUD 좌상단 WAVE 라벨 (§10.1)
            EnsureWaveLabel(manager);

            // 테스트 배치 적 제거 — 이제 스폰은 WaveManager가 담당한다
            int removed = 0;
            foreach (string name in TEST_ENEMY_NAMES)
            {
                GameObject found = GameObject.Find(name);
                if (found == null) continue;
                Object.DestroyImmediate(found);
                removed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[WaveSetup] 웨이브 시스템 배선 완료 — 구성 7개, 테스트 적 {removed}기 제거 / scene saved={saved}");
        }

        private static EnemyBase LoadEnemy(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null ? prefab.GetComponent<EnemyBase>() : null;
        }

        private static WaveSystemConfigSO EnsureSystemConfig()
        {
            WaveSystemConfigSO existing = AssetDatabase.LoadAssetAtPath<WaveSystemConfigSO>(SYSTEM_CONFIG_PATH);
            if (existing != null) return existing;

            // 기본값(간격 1.5 / 상한 10 / 아레나 24×14 / 인셋 1)은 SO 정의 초기값 그대로 (§6.1/§2)
            WaveSystemConfigSO config = ScriptableObject.CreateInstance<WaveSystemConfigSO>();
            AssetDatabase.CreateAsset(config, SYSTEM_CONFIG_PATH);
            Debug.Log("[WaveSetup] WaveSystemConfig_Default 생성");
            return config;
        }

        private static WaveConfigSO EnsureWave(int number, bool isBossWave,
            params (EnemyBase prefab, int count)[] entries)
        {
            string path = string.Format(WAVE_CONFIG_PATH_FORMAT, number);
            WaveConfigSO existing = AssetDatabase.LoadAssetAtPath<WaveConfigSO>(path);
            if (existing != null) return existing;   // 값 보존 (밸런스 손편집 보호)

            WaveConfigSO config = ScriptableObject.CreateInstance<WaveConfigSO>();
            SerializedObject so = new SerializedObject(config);
            so.FindProperty("_isBossWave").boolValue = isBossWave;

            SerializedProperty entriesProperty = so.FindProperty("_entries");
            entriesProperty.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty element = entriesProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_enemyPrefab").objectReferenceValue = entries[i].prefab;
                element.FindPropertyRelative("_count").intValue = entries[i].count;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(config, path);
            Debug.Log($"[WaveSetup] WaveConfig_{number} 생성");
            return config;
        }

        private static void EnsureWaveLabel(WaveManager manager)
        {
            GameObject canvas = GameObject.Find("GameScreensCanvas");
            Transform hudPanel = canvas != null ? canvas.transform.Find("HudPanel") : null;
            if (hudPanel == null)
            {
                Debug.LogWarning("[WaveSetup] HudPanel 없음 — 'HUD 배선'을 먼저 실행하면 WAVE 라벨이 붙는다");
                return;
            }

            Transform found = hudPanel.Find("WaveLabel");
            GameObject labelRoot = found != null ? found.gameObject : new GameObject("WaveLabel", typeof(RectTransform));
            labelRoot.transform.SetParent(hudPanel, false);

            RectTransform rect = labelRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(300f, 44f);

            TextMeshProUGUI text = labelRoot.GetComponent<TextMeshProUGUI>();
            if (text == null) text = labelRoot.AddComponent<TextMeshProUGUI>();
            text.text = "WAVE 1/7";
            text.fontSize = 32f;
            text.color = new Color(0.92f, 0.92f, 0.95f, 1f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;

            WaveLabel label = labelRoot.GetComponent<WaveLabel>();
            if (label == null) label = labelRoot.AddComponent<WaveLabel>();
            SerializedObject so = new SerializedObject(label);
            so.FindProperty("_waveManager").objectReferenceValue = manager;
            so.FindProperty("_label").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
