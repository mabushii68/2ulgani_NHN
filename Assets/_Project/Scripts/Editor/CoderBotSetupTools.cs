// 코딩봇 에셋 빌더 — 멱등(이미 있으면 건너뜀). 기존 에셋 값을 덮어쓰지 않는다.
// 삼각 스프라이트가 없어서(§5.1 실루엣 = 삼각) 여기서 절차 생성한다 — 기존 원/사각 플레이스홀더와 같은 방식.
using System.IO;
using UnityEditor;
using UnityEngine;
using Luddite.Data;
using Luddite.Enemies;

namespace Luddite.EditorTools
{
    public static class CoderBotSetupTools
    {
        private const string PREFAB_PATH = "Assets/_Project/Prefabs/CoderBot.prefab";
        private const string STATS_PATH = "Assets/_Project/SO/EnemyStats_CoderBot.asset";
        private const string TRIANGLE_SPRITE_PATH = "Assets/_Project/Sprites/Placeholder_Triangle.png";

        private const int SPRITE_SIZE = 64;   // 기존 플레이스홀더와 동일 (PPU 64 = 1유닛)

        private static readonly Color BODY_COLOR = new Color(0.55f, 0.57f, 0.60f, 1f); // 무채색 (§5.1)

        [MenuItem("Luddite/Setup/코딩봇 프리팹·SO 보장")]
        public static void EnsureCoderBotAssets()
        {
            Sprite triangle = EnsureTriangleSprite();
            EnemyStatsSO stats = EnsureStats();
            EnsurePrefab(stats, triangle);
        }

        /// <summary>오른쪽(+X)을 가리키는 흰 삼각형 — 진행 방향이 실루엣에서 읽힌다.</summary>
        private static Sprite EnsureTriangleSprite()
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(TRIANGLE_SPRITE_PATH);
            if (existing != null) return existing;

            Texture2D texture = new Texture2D(SPRITE_SIZE, SPRITE_SIZE, TextureFormat.RGBA32, false);
            Color clear = new Color(0f, 0f, 0f, 0f);
            const float LEFT = 6f, RIGHT = 58f, HALF_HEIGHT = 26f;

            for (int y = 0; y < SPRITE_SIZE; y++)
            {
                for (int x = 0; x < SPRITE_SIZE; x++)
                {
                    // x가 오른쪽 꼭짓점에 가까울수록 허용 반높이가 0으로 수렴
                    float allowed = HALF_HEIGHT * Mathf.Clamp01((RIGHT - x) / (RIGHT - LEFT));
                    bool inside = x >= LEFT && x <= RIGHT && Mathf.Abs(y - SPRITE_SIZE * 0.5f) <= allowed;
                    texture.SetPixel(x, y, inside ? Color.white : clear);
                }
            }

            File.WriteAllBytes(TRIANGLE_SPRITE_PATH, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(TRIANGLE_SPRITE_PATH);

            // 기존 플레이스홀더와 동일한 임포트 설정
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(TRIANGLE_SPRITE_PATH);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            Debug.Log("[CoderBotSetup] Placeholder_Triangle.png 생성 (절차 생성 — 외부 에셋 아님)");
            return AssetDatabase.LoadAssetAtPath<Sprite>(TRIANGLE_SPRITE_PATH);
        }

        /// <summary>§5.1 코딩봇: HP 20 / 이동 5.5 / 돌진 접촉 12 / 돌진 10u/s / 쿨다운 2.0초 / 거리 5 트리거.</summary>
        private static EnemyStatsSO EnsureStats()
        {
            EnemyStatsSO existing = AssetDatabase.LoadAssetAtPath<EnemyStatsSO>(STATS_PATH);
            if (existing != null)
            {
                Debug.Log("[CoderBotSetup] EnemyStats_CoderBot 이미 존재 — 값 보존");
                return existing;
            }

            EnemyStatsSO stats = ScriptableObject.CreateInstance<EnemyStatsSO>();
            SerializedObject so = new SerializedObject(stats);
            so.FindProperty("_displayName").stringValue = "코딩봇";
            so.FindProperty("_maxHp").floatValue = 20f;
            so.FindProperty("_moveSpeed").floatValue = 5.5f;
            so.FindProperty("_attackRange").floatValue = 5f;     // §5.2: 거리 < 5에서 돌진 텔레그래프
            so.FindProperty("_aimDuration").floatValue = 0.4f;   // §5.2: ChargeTelegraph(0.4s)
            so.FindProperty("_attackCooldown").floatValue = 2f;  // §5.1: 쿨다운 2.0초
            // 돌진 10u/s·0.6s·경직 0.8s·돌진 접촉 12는 SO 정의의 필드 초기값 그대로.
            // 탄환 필드는 미사용 (코딩봇은 쏘지 않는다)
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(stats, STATS_PATH);
            Debug.Log("[CoderBotSetup] EnemyStats_CoderBot 생성");
            return stats;
        }

        private static void EnsurePrefab(EnemyStatsSO stats, Sprite triangle)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH) != null)
            {
                Debug.Log("[CoderBotSetup] CoderBot 프리팹 이미 존재 — 갱신 생략 (손편집 보호)");
                return;
            }

            if (triangle == null)
            {
                Debug.LogError("[CoderBotSetup] 삼각 스프라이트 생성 실패");
                return;
            }

            GameObject root = new GameObject("CoderBot");
            root.AddComponent<Rigidbody2D>();       // 물리 속성은 EnemyBase.Awake가 설정
            root.AddComponent<CircleCollider2D>();  // 반지름은 stats.HitboxRadius로 설정

            // 본체가 곧 조준 피벗 — 삼각형이 진행·돌진 방향을 가리킨다
            GameObject body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = triangle;
            bodyRenderer.color = BODY_COLOR;

            CoderBot bot = root.AddComponent<CoderBot>();
            SerializedObject botSo = new SerializedObject(bot);
            botSo.FindProperty("_stats").objectReferenceValue = stats;
            botSo.FindProperty("_aimPivot").objectReferenceValue = body.transform;
            botSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[CoderBotSetup] CoderBot.prefab 생성");
        }

        /// <summary>테스트용 코딩봇 1기를 씬에 배치. TODO(D4): WaveManager 스폰으로 대체 후 제거.</summary>
        [MenuItem("Luddite/Setup/코딩봇 1기 씬에 배치 (테스트용)")]
        public static void PlaceTestCoderBot()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[CoderBotSetup] 플레이 모드에서는 씬을 편집하지 않는다");
                return;
            }

            if (GameObject.Find("CoderBot_Test") != null)
            {
                Debug.Log("[CoderBotSetup] CoderBot_Test 이미 배치됨");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogError("[CoderBotSetup] CoderBot.prefab 없음 — 먼저 '코딩봇 프리팹·SO 보장' 실행");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "CoderBot_Test";
            instance.transform.position = new Vector3(0f, -4f, 0f);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(instance.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(instance.scene);
            Debug.Log("[CoderBotSetup] CoderBot_Test 배치 (0, -4) — D4 웨이브 스폰 도입 시 제거할 것");
        }
    }
}
