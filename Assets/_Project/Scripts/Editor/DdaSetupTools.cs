// 매크로 DDA 배선 도구 — 멱등. DdaConfig 생성 + WaveManager 연결 + COUNTER PROTOCOL 라벨 (§6.3/§10.2).
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
    public static class DdaSetupTools
    {
        private const string MAIN_SCENE_PATH = "Assets/_Project/Scenes/Main.unity";
        private const string CONFIG_PATH = "Assets/_Project/SO/DdaConfig_Default.asset";
        private const string CHATBOT_PREFAB = "Assets/_Project/Prefabs/ChatbotDrone.prefab";
        private const string CODER_PREFAB = "Assets/_Project/Prefabs/CoderBot.prefab";
        private const string PAINTER_PREFAB = "Assets/_Project/Prefabs/PainterBot.prefab";

        [MenuItem("Luddite/Setup/매크로 DDA 배선 (§6.3)")]
        public static void EnsureDda()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[DdaSetup] 플레이 모드에서는 씬을 편집하지 않는다");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MAIN_SCENE_PATH)
            {
                Debug.LogError($"[DdaSetup] 활성 씬이 Main.unity가 아니다 (현재: {scene.path})");
                return;
            }

            WaveManager waveManager = Object.FindFirstObjectByType<WaveManager>();
            AIBrainRunner brain = Object.FindFirstObjectByType<AIBrainRunner>();
            if (waveManager == null || brain == null)
            {
                Debug.LogError("[DdaSetup] WaveManager 또는 AIBrainRunner 없음 — 선행 Setup 메뉴 실행 필요");
                return;
            }

            DdaConfigSO config = EnsureConfig();
            if (config == null) return;

            SerializedObject so = new SerializedObject(waveManager);
            so.FindProperty("_ddaConfig").objectReferenceValue = config;
            so.FindProperty("_brain").objectReferenceValue = brain;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureCounterProtocolLabel(waveManager, brain);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[DdaSetup] 매크로 DDA 배선 완료 / scene saved={saved}");
        }

        private static DdaConfigSO EnsureConfig()
        {
            DdaConfigSO existing = AssetDatabase.LoadAssetAtPath<DdaConfigSO>(CONFIG_PATH);
            if (existing != null) return existing;

            EnemyBase chatbot = Load(CHATBOT_PREFAB);
            EnemyBase coder = Load(CODER_PREFAB);
            EnemyBase painter = Load(PAINTER_PREFAB);
            if (chatbot == null || coder == null || painter == null)
            {
                Debug.LogError("[DdaSetup] 적 프리팹 누락");
                return null;
            }

            // 임계 6 / 3.5, 비율 0.3, 웨이브 4부터는 SO 정의의 필드 초기값 그대로 (§6.3)
            DdaConfigSO config = ScriptableObject.CreateInstance<DdaConfigSO>();
            SerializedObject so = new SerializedObject(config);
            so.FindProperty("_chatbotPrefab").objectReferenceValue = chatbot;
            so.FindProperty("_rushReplacement").objectReferenceValue = coder;
            so.FindProperty("_rangedReplacement").objectReferenceValue = painter;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(config, CONFIG_PATH);
            Debug.Log("[DdaSetup] DdaConfig_Default 생성");
            return config;
        }

        private static EnemyBase Load(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null ? prefab.GetComponent<EnemyBase>() : null;
        }

        private static void EnsureCounterProtocolLabel(WaveManager waveManager, AIBrainRunner brain)
        {
            GameObject canvas = GameObject.Find("GameScreensCanvas");
            Transform panel = canvas != null ? canvas.transform.Find("WaveIntervalPanel") : null;
            if (panel == null)
            {
                Debug.LogError("[DdaSetup] WaveIntervalPanel 없음");
                return;
            }

            Transform found = panel.Find("CounterProtocol");
            GameObject labelRoot = found != null ? found.gameObject : new GameObject("CounterProtocol", typeof(RectTransform));
            labelRoot.transform.SetParent(panel, false);

            RectTransform rect = labelRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 150f);
            rect.sizeDelta = new Vector2(900f, 110f);

            TextMeshProUGUI text = labelRoot.GetComponent<TextMeshProUGUI>();
            if (text == null) text = labelRoot.AddComponent<TextMeshProUGUI>();
            text.fontSize = 26f;
            text.color = new Color(1f, 0.35f, 1f, 1f);   // 마젠타 계열 — "AI가 나를 읽고 행하는 것" (§10.4)
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            CounterProtocolLabel label = labelRoot.GetComponent<CounterProtocolLabel>();
            if (label == null) label = labelRoot.AddComponent<CounterProtocolLabel>();
            SerializedObject so = new SerializedObject(label);
            so.FindProperty("_waveManager").objectReferenceValue = waveManager;
            so.FindProperty("_brain").objectReferenceValue = brain;
            so.FindProperty("_label").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
