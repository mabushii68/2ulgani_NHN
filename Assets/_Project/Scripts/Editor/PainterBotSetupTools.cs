// 그림봇 에셋 빌더 — 멱등(이미 있으면 건너뜀). 기존 에셋 값을 덮어쓰지 않는다.
// 프리팹 구조는 ChatbotDrone.prefab의 관례를 따른다: 루트(물리+FSM+총) / Body(본체) / AimPivot→Muzzle(총구 시각).
using UnityEditor;
using UnityEngine;
using Luddite.Combat;
using Luddite.Data;
using Luddite.Enemies;

namespace Luddite.EditorTools
{
    public static class PainterBotSetupTools
    {
        private const string PREFAB_PATH = "Assets/_Project/Prefabs/PainterBot.prefab";
        private const string STATS_PATH = "Assets/_Project/SO/EnemyStats_PainterBot.asset";
        private const string PROJECTILE_PREFAB_PATH = "Assets/_Project/Prefabs/EnemyProjectile.prefab";
        private const string SQUARE_SPRITE_PATH = "Assets/_Project/Sprites/Placeholder_Square.png";

        // 무채색 규칙 (§5.1) — 챗봇(0.60,0.63,0.67)과 톤을 살짝 달리해 실루엣+명도로 구분
        private static readonly Color BODY_COLOR = new Color(0.72f, 0.72f, 0.70f, 1f);
        private static readonly Color MUZZLE_COLOR = new Color(0.78f, 0.81f, 0.85f, 1f);

        [MenuItem("Luddite/Setup/그림봇 프리팹·SO 보장")]
        public static void EnsurePainterBotAssets()
        {
            EnemyStatsSO stats = EnsureStats();
            EnsurePrefab(stats);
        }

        /// <summary>§5.1 그림봇: HP 40 / 이동 3.0 / 부채꼴 각 6 / 탄속 5 / 간격 2.5초 / 거리 6~9 / 텔레그래프 0.4초.</summary>
        private static EnemyStatsSO EnsureStats()
        {
            EnemyStatsSO existing = AssetDatabase.LoadAssetAtPath<EnemyStatsSO>(STATS_PATH);
            if (existing != null)
            {
                Debug.Log("[PainterBotSetup] EnemyStats_PainterBot 이미 존재 — 값 보존");
                return existing;
            }

            EnemyStatsSO stats = ScriptableObject.CreateInstance<EnemyStatsSO>();
            SerializedObject so = new SerializedObject(stats);
            so.FindProperty("_displayName").stringValue = "그림봇";
            so.FindProperty("_maxHp").floatValue = 40f;
            so.FindProperty("_moveSpeed").floatValue = 3f;
            so.FindProperty("_attackCooldown").floatValue = 2.5f;
            so.FindProperty("_aimDuration").floatValue = 0.4f;      // §5.2 Telegraph(0.4s)
            so.FindProperty("_attackRange").floatValue = 9f;        // 유지 거리 상한과 일치 (그림봇은 Preferred 범위를 쓴다)
            so.FindProperty("_projectileDamage").floatValue = 6f;   // 부채꼴 각 6
            so.FindProperty("_projectileSpeed").floatValue = 5f;
            // 거리 6~9 / 부채꼴 3발·30° / 재배치 0.8초는 SO 정의의 필드 초기값 그대로
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(stats, STATS_PATH);
            Debug.Log("[PainterBotSetup] EnemyStats_PainterBot 생성");
            return stats;
        }

        private static void EnsurePrefab(EnemyStatsSO stats)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH) != null)
            {
                Debug.Log("[PainterBotSetup] PainterBot 프리팹 이미 존재 — 갱신 생략 (손편집 보호)");
                return;
            }

            Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SQUARE_SPRITE_PATH);
            GameObject projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PROJECTILE_PREFAB_PATH);
            if (square == null || projectilePrefab == null)
            {
                Debug.LogError($"[PainterBotSetup] 원본 에셋 누락 — 사각 스프라이트:{square != null} 적탄 프리팹:{projectilePrefab != null}");
                return;
            }

            GameObject root = new GameObject("PainterBot");
            root.AddComponent<Rigidbody2D>();       // 물리 속성은 EnemyBase.Awake가 설정한다
            root.AddComponent<CircleCollider2D>();  // 반지름은 stats.HitboxRadius로 설정된다

            // 본체: 사각 실루엣 (§5.1) — 스프라이트 원본이 1유닛이므로 스케일 1 = 히트박스 지름 1과 일치
            GameObject body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = square;
            bodyRenderer.color = BODY_COLOR;

            // 총구 시각 (챗봇과 동일 관례)
            GameObject aimPivot = new GameObject("AimPivot");
            aimPivot.transform.SetParent(root.transform, false);
            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(aimPivot.transform, false);
            muzzle.transform.localPosition = new Vector3(0.5f, 0f, 0f);
            muzzle.transform.localScale = new Vector3(0.5f, 0.12f, 1f);
            SpriteRenderer muzzleRenderer = muzzle.AddComponent<SpriteRenderer>();
            muzzleRenderer.sprite = square;
            muzzleRenderer.color = MUZZLE_COLOR;

            EnemyGun gun = root.AddComponent<EnemyGun>();
            SerializedObject gunSo = new SerializedObject(gun);
            gunSo.FindProperty("_stats").objectReferenceValue = stats;
            gunSo.FindProperty("_projectilePrefab").objectReferenceValue =
                projectilePrefab.GetComponent<Projectile>();
            gunSo.ApplyModifiedPropertiesWithoutUndo();

            PainterBot bot = root.AddComponent<PainterBot>();
            SerializedObject botSo = new SerializedObject(bot);
            botSo.FindProperty("_stats").objectReferenceValue = stats;
            botSo.FindProperty("_gun").objectReferenceValue = gun;
            botSo.FindProperty("_aimPivot").objectReferenceValue = aimPivot.transform;
            botSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[PainterBotSetup] PainterBot.prefab 생성");
        }

        /// <summary>테스트용 그림봇 1기를 씬에 배치. TODO(D4): WaveManager 스폰으로 대체 후 제거.</summary>
        [MenuItem("Luddite/Setup/그림봇 1기 씬에 배치 (테스트용)")]
        public static void PlaceTestPainterBot()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[PainterBotSetup] 플레이 모드에서는 씬을 편집하지 않는다");
                return;
            }

            if (GameObject.Find("PainterBot_Test") != null)
            {
                Debug.Log("[PainterBotSetup] PainterBot_Test 이미 배치됨");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogError("[PainterBotSetup] PainterBot.prefab 없음 — 먼저 '그림봇 프리팹·SO 보장' 실행");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "PainterBot_Test";
            instance.transform.position = new Vector3(-6f, 3.5f, 0f);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(instance.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(instance.scene);
            Debug.Log("[PainterBotSetup] PainterBot_Test 배치 (-6, 3.5) — D4 웨이브 스폰 도입 시 제거할 것");
        }
    }
}
