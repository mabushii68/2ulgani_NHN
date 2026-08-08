// 보스 에셋 빌더 — 멱등(이미 있으면 값 보존). §9 P1 + 웨이브 7 연결.
using UnityEditor;
using UnityEngine;
using Luddite.Combat;
using Luddite.Data;
using Luddite.Enemies;

namespace Luddite.EditorTools
{
    public static class BossSetupTools
    {
        private const string PREFAB_PATH = "Assets/_Project/Prefabs/BossLLM.prefab";
        private const string STATS_PATH = "Assets/_Project/SO/EnemyStats_Boss.asset";
        private const string CONFIG_PATH = "Assets/_Project/SO/BossConfig_Default.asset";
        private const string WAVE7_PATH = "Assets/_Project/SO/WaveConfig_7.asset";
        private const string CHATBOT_PREFAB = "Assets/_Project/Prefabs/ChatbotDrone.prefab";
        private const string PROJECTILE_PREFAB = "Assets/_Project/Prefabs/EnemyProjectile.prefab";
        private const string CIRCLE_SPRITE = "Assets/_Project/Sprites/Placeholder_Circle.png";

        // 무채색 대형 실루엣 (§5.1). P2 마젠타화는 P2 세션에서
        private static readonly Color BODY_COLOR = new Color(0.42f, 0.42f, 0.46f, 1f);
        private static readonly Color LASER_COLOR = new Color(1f, 0.62f, 0.28f, 0.9f);   // 주황 — 마젠타 금지(P1)

        [MenuItem("Luddite/Setup/보스 프리팹·SO 보장 (§9 P1)")]
        public static void EnsureBossAssets()
        {
            EnemyStatsSO stats = EnsureStats();
            BossConfigSO config = EnsureConfig();
            EnsurePrefab(stats, config);
            EnsureWave7Entry();
        }

        /// <summary>§5.1 보스 열: HP 600 / 이동 P1 2.0 / 접촉 8 / 대형 실루엣.</summary>
        private static EnemyStatsSO EnsureStats()
        {
            EnemyStatsSO existing = AssetDatabase.LoadAssetAtPath<EnemyStatsSO>(STATS_PATH);
            if (existing != null)
            {
                Debug.Log("[BossSetup] EnemyStats_Boss 이미 존재 — 값 보존");
                return existing;
            }

            EnemyStatsSO stats = ScriptableObject.CreateInstance<EnemyStatsSO>();
            SerializedObject so = new SerializedObject(stats);
            so.FindProperty("_displayName").stringValue = "거대 LLM";
            so.FindProperty("_maxHp").floatValue = 600f;
            so.FindProperty("_moveSpeed").floatValue = 2f;          // P1 (P2 3.5는 P2 세션에서)
            so.FindProperty("_hitboxDiameter").floatValue = 2f;     // 대형
            so.FindProperty("_knockbackDistance").floatValue = 0.1f; // 보스는 거의 밀리지 않는다 — 연출 최소값
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(stats, STATS_PATH);
            Debug.Log("[BossSetup] EnemyStats_Boss 생성");
            return stats;
        }

        private static BossConfigSO EnsureConfig()
        {
            BossConfigSO existing = AssetDatabase.LoadAssetAtPath<BossConfigSO>(CONFIG_PATH);
            if (existing != null)
            {
                Debug.Log("[BossSetup] BossConfig 이미 존재 — 값 보존");
                return existing;
            }

            GameObject chatbot = AssetDatabase.LoadAssetAtPath<GameObject>(CHATBOT_PREFAB);
            BossConfigSO config = ScriptableObject.CreateInstance<BossConfigSO>();
            SerializedObject so = new SerializedObject(config);
            so.FindProperty("_summonPrefab").objectReferenceValue =
                chatbot != null ? chatbot.GetComponent<EnemyBase>() : null;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(config, CONFIG_PATH);
            Debug.Log("[BossSetup] BossConfig_Default 생성 (패턴별 데미지는 초안 — 기획 검토 대상)");
            return config;
        }

        private static void EnsurePrefab(EnemyStatsSO stats, BossConfigSO config)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH) != null)
            {
                Debug.Log("[BossSetup] BossLLM 프리팹 이미 존재 — 갱신 생략 (손편집 보호)");
                return;
            }

            Sprite circle = AssetDatabase.LoadAssetAtPath<Sprite>(CIRCLE_SPRITE);
            GameObject projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PROJECTILE_PREFAB);
            Material spriteDefault = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            if (circle == null || projectilePrefab == null)
            {
                Debug.LogError("[BossSetup] 원본 에셋 누락");
                return;
            }

            GameObject root = new GameObject("BossLLM");
            root.transform.localScale = Vector3.one * 2f;   // 대형 (히트박스 2u와 함께 실효 지름 4u)
            root.AddComponent<Rigidbody2D>();
            root.AddComponent<CircleCollider2D>();

            GameObject body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = circle;
            bodyRenderer.color = BODY_COLOR;

            // 레이저 텔레그래프·섬광 (비활성)
            GameObject laserObject = new GameObject("LaserLine");
            laserObject.transform.SetParent(root.transform, false);
            LineRenderer laser = laserObject.AddComponent<LineRenderer>();
            laser.useWorldSpace = true;
            laser.positionCount = 2;
            laser.material = spriteDefault;
            laser.startColor = LASER_COLOR;
            laser.endColor = new Color(LASER_COLOR.r, LASER_COLOR.g, LASER_COLOR.b, 0.4f);
            laser.sortingOrder = 10;
            laserObject.SetActive(false);

            BossLLM boss = root.AddComponent<BossLLM>();
            SerializedObject so = new SerializedObject(boss);
            so.FindProperty("_stats").objectReferenceValue = stats;
            so.FindProperty("_config").objectReferenceValue = config;
            so.FindProperty("_projectilePrefab").objectReferenceValue =
                projectilePrefab.GetComponent<Projectile>();
            so.FindProperty("_laserLine").objectReferenceValue = laser;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[BossSetup] BossLLM.prefab 생성");
        }

        /// <summary>웨이브 7 구성에 보스 1기 등록 — WaveManager의 임시 승리 스텁을 실스폰으로 대체.</summary>
        private static void EnsureWave7Entry()
        {
            WaveConfigSO wave7 = AssetDatabase.LoadAssetAtPath<WaveConfigSO>(WAVE7_PATH);
            GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (wave7 == null || bossPrefab == null)
            {
                Debug.LogError("[BossSetup] WaveConfig_7 또는 보스 프리팹 없음");
                return;
            }

            if (wave7.TotalCount > 0)
            {
                Debug.Log("[BossSetup] WaveConfig_7 엔트리 이미 존재 — 값 보존");
                return;
            }

            SerializedObject so = new SerializedObject(wave7);
            SerializedProperty entries = so.FindProperty("_entries");
            entries.arraySize = 1;
            SerializedProperty element = entries.GetArrayElementAtIndex(0);
            element.FindPropertyRelative("_enemyPrefab").objectReferenceValue =
                bossPrefab.GetComponent<EnemyBase>();
            element.FindPropertyRelative("_count").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(wave7);
            AssetDatabase.SaveAssets();
            Debug.Log("[BossSetup] WaveConfig_7에 보스 엔트리 등록 (임시 승리 스텁 대체)");
        }
    }
}
