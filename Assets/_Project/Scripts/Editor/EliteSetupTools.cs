// 엘리트(프리미엄 구독봇) 에셋 빌더 — 전부 멱등(이미 있으면 건너뛰거나 갱신만).
// CLAUDE.md Scripts/Editor 규칙: 파괴적 빌더 금지. 이미 존재하는 프리팹·SO의 값은 덮어쓰지 않는다
// (아트·밸런스 손편집 보호) — 재생성이 필요하면 에셋을 지우고 다시 실행한다.
using UnityEditor;
using UnityEngine;
using Luddite.Data;
using Luddite.Enemies;

namespace Luddite.EditorTools
{
    public static class EliteSetupTools
    {
        private const string CHATBOT_PREFAB_PATH = "Assets/_Project/Prefabs/ChatbotDrone.prefab";
        private const string ELITE_PREFAB_PATH = "Assets/_Project/Prefabs/EliteDrone.prefab";
        private const string ELITE_STATS_PATH = "Assets/_Project/SO/EnemyStats_Elite.asset";
        private const string PREDICTIVE_CONFIG_PATH = "Assets/_Project/SO/PredictiveShotConfig_Default.asset";
        private const string CIRCLE_SPRITE_PATH = "Assets/_Project/Sprites/Placeholder_Circle.png";

        // 🔴 §10.4: 마젠타 = AI가 나를 읽고 행하는 것. 엘리트 본체·마커·조준선만 이 계열을 쓴다
        private static readonly Color ELITE_BODY = new Color(0.85f, 0.25f, 0.85f, 1f);
        private static readonly Color MAGENTA = Color.magenta;

        [MenuItem("Luddite/Setup/엘리트 프리팹·SO 보장")]
        public static void EnsureEliteAssets()
        {
            EnemyStatsSO stats = EnsureEliteStats();
            PredictiveShotConfigSO config = EnsurePredictiveConfig();
            EnsureElitePrefab(stats, config);
        }

        /// <summary>§5.1 프리미엄 구독봇 수치: HP 60 / 이동 2.5 / 단발탄 8 / 탄속 6 / 간격 1.5초.</summary>
        private static EnemyStatsSO EnsureEliteStats()
        {
            EnemyStatsSO existing = AssetDatabase.LoadAssetAtPath<EnemyStatsSO>(ELITE_STATS_PATH);
            if (existing != null)
            {
                Debug.Log("[EliteSetup] EnemyStats_Elite 이미 존재 — 값 보존");
                return existing;
            }

            EnemyStatsSO stats = ScriptableObject.CreateInstance<EnemyStatsSO>();
            SerializedObject so = new SerializedObject(stats);
            so.FindProperty("_displayName").stringValue = "프리미엄 구독봇";
            so.FindProperty("_maxHp").floatValue = 60f;
            so.FindProperty("_moveSpeed").floatValue = 2.5f;
            so.FindProperty("_attackCooldown").floatValue = 1.5f;
            // 나머지(사거리 8, 조준 0.3, 탄 8/6, 접촉 8, 스폰 0.5)는 챗봇과 동일한 SO 기본값
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(stats, ELITE_STATS_PATH);
            Debug.Log("[EliteSetup] EnemyStats_Elite 생성");
            return stats;
        }

        private static PredictiveShotConfigSO EnsurePredictiveConfig()
        {
            PredictiveShotConfigSO existing =
                AssetDatabase.LoadAssetAtPath<PredictiveShotConfigSO>(PREDICTIVE_CONFIG_PATH);
            if (existing != null)
            {
                Debug.Log("[EliteSetup] PredictiveShotConfig 이미 존재 — 값 보존");
                return existing;
            }

            // 기본값(offset 1.5 / 텔레그래프 0.35 / 2회당 1회)은 SO 정의의 필드 초기값 그대로 (§7.4)
            PredictiveShotConfigSO config = ScriptableObject.CreateInstance<PredictiveShotConfigSO>();
            AssetDatabase.CreateAsset(config, PREDICTIVE_CONFIG_PATH);
            Debug.Log("[EliteSetup] PredictiveShotConfig_Default 생성");
            return config;
        }

        /// <summary>
        /// 챗봇 프리팹의 <b>변형(Prefab Variant)</b>으로 엘리트를 만든다 — 챗봇 수정이 자동 전파된다.
        /// §5.1: 원 ×1.3 / 마젠타 + 👁 마커. 엘리트는 별도 클래스가 아니라 EliteModifier 부착.
        /// </summary>
        private static void EnsureElitePrefab(EnemyStatsSO stats, PredictiveShotConfigSO config)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ELITE_PREFAB_PATH) != null)
            {
                Debug.Log("[EliteSetup] EliteDrone 프리팹 이미 존재 — 갱신 생략 (아트 손편집 보호)");
                return;
            }

            GameObject chatbotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CHATBOT_PREFAB_PATH);
            Sprite circle = AssetDatabase.LoadAssetAtPath<Sprite>(CIRCLE_SPRITE_PATH);
            Material spriteDefault = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            if (chatbotPrefab == null || circle == null)
            {
                Debug.LogError($"[EliteSetup] 원본 에셋 누락 — 프리팹:{chatbotPrefab != null} 원 스프라이트:{circle != null}");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(chatbotPrefab);
            instance.name = "EliteDrone";
            instance.transform.localScale = Vector3.one * 1.3f;   // §5.1 원 ×1.3 (EnemyBase가 기본 스케일로 캡처)

            // 본체 마젠타 (스폰 텔레그래프·피격 플래시의 기준색이 된다)
            SpriteRenderer body = instance.GetComponentInChildren<SpriteRenderer>();
            if (body != null) body.color = ELITE_BODY;

            // 스탯 교체: EnemyBase + EnemyGun 둘 다 엘리트 SO를 본다
            SerializedObject drone = new SerializedObject(instance.GetComponent<ChatbotDrone>());
            drone.FindProperty("_stats").objectReferenceValue = stats;
            drone.ApplyModifiedPropertiesWithoutUndo();

            EnemyGun gun = instance.GetComponentInChildren<EnemyGun>();
            SerializedObject gunSo = new SerializedObject(gun);
            gunSo.FindProperty("_stats").objectReferenceValue = stats;
            gunSo.ApplyModifiedPropertiesWithoutUndo();

            // 👁 마커 (플레이스홀더: 본체 위 작은 원 2겹 — 흰자+동공)
            GameObject eyeWhite = CreateSpriteChild(instance.transform, "EyeMarker", circle, Color.white, 0.42f, spriteDefault, 5);
            eyeWhite.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            GameObject eyePupil = CreateSpriteChild(eyeWhite.transform, "Pupil", circle, new Color(0.1f, 0.02f, 0.1f, 1f), 0.5f, spriteDefault, 6);
            eyePupil.transform.localPosition = Vector3.zero;

            // 조준선 (비활성 — EliteModifier가 텔레그래프 중에만 켠다)
            GameObject lineObject = new GameObject("AimLine");
            lineObject.transform.SetParent(instance.transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.07f;
            line.endWidth = 0.07f;
            line.material = spriteDefault;
            line.startColor = new Color(MAGENTA.r, MAGENTA.g, MAGENTA.b, 0.85f);
            line.endColor = new Color(MAGENTA.r, MAGENTA.g, MAGENTA.b, 0.85f);
            line.sortingOrder = 10;
            // TODO(아트 D3): 점선 텍스처로 교체 (§7.4 "점선 조준선") — 지금은 실선 플레이스홀더
            lineObject.SetActive(false);

            // 예측 지점 원형 마커 (비활성)
            GameObject marker = CreateSpriteChild(instance.transform, "TargetMarker", circle,
                new Color(MAGENTA.r, MAGENTA.g, MAGENTA.b, 0.4f), 0.9f, spriteDefault, 9);
            marker.SetActive(false);

            // 예측탄 트레일 원본 (비활성 — 발사 시 복제해 탄에 부착)
            GameObject trailObject = new GameObject("TrailTemplate");
            trailObject.transform.SetParent(instance.transform, false);
            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = 0.16f;
            trail.endWidth = 0f;
            trail.material = spriteDefault;
            trail.startColor = MAGENTA;
            trail.endColor = new Color(MAGENTA.r, MAGENTA.g, MAGENTA.b, 0f);
            trail.sortingOrder = 8;
            trailObject.SetActive(false);

            // EliteModifier 부착 + 배선
            EliteModifier elite = instance.AddComponent<EliteModifier>();
            SerializedObject eliteSo = new SerializedObject(elite);
            eliteSo.FindProperty("_config").objectReferenceValue = config;
            eliteSo.FindProperty("_gun").objectReferenceValue = gun;
            eliteSo.FindProperty("_aimLine").objectReferenceValue = line;
            eliteSo.FindProperty("_targetMarker").objectReferenceValue = marker.transform;
            eliteSo.FindProperty("_trailTemplate").objectReferenceValue = trail;
            eliteSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(instance, ELITE_PREFAB_PATH);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            Debug.Log("[EliteSetup] EliteDrone.prefab 생성 (ChatbotDrone 변형)");
        }

        /// <summary>테스트용 엘리트 1기를 씬에 배치. TODO(D4): WaveManager 스폰으로 대체 후 제거.</summary>
        [MenuItem("Luddite/Setup/엘리트 1기 씬에 배치 (테스트용)")]
        public static void PlaceTestElite()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[EliteSetup] 플레이 모드에서는 씬을 편집하지 않는다");
                return;
            }

            if (GameObject.Find("EliteDrone_Test") != null)
            {
                Debug.Log("[EliteSetup] EliteDrone_Test 이미 배치됨");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ELITE_PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogError("[EliteSetup] EliteDrone.prefab 없음 — 먼저 '엘리트 프리팹·SO 보장' 실행");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "EliteDrone_Test";
            instance.transform.position = new Vector3(6f, 3.5f, 0f);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(instance.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(instance.scene);
            Debug.Log("[EliteSetup] EliteDrone_Test 배치 (6, 3.5) — D4 웨이브 스폰 도입 시 제거할 것");
        }

        private static GameObject CreateSpriteChild(Transform parent, string name, Sprite sprite,
            Color color, float scale, Material material, int sortingOrder)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localScale = Vector3.one * scale;

            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.material = material;
            renderer.sortingOrder = sortingOrder;
            return child;
        }
    }
}
