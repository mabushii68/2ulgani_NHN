// 보스 에셋 빌더 — 멱등(이미 있으면 값 보존). §9 P1 + 웨이브 7 연결 + P2 컴포넌트.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Luddite.Combat;
using Luddite.Data;
using Luddite.Enemies;
using Luddite.UI;

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
        private const string PREDICTIVE_CONFIG_PATH = "Assets/_Project/SO/PredictiveShotConfig_Default.asset";
        private const string DARK_ORB_SPRITE = "Assets/_Project/Sprites/Projectiles/DarkOrb.png";
        private const string MAIN_SCENE_PATH = "Assets/_Project/Scenes/Main.unity";

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

        // ── P2 PATTERN: YOU (§9) — 기존 프리팹에 가산 · 멱등 ──

        /// <summary>
        /// P2에 필요한 컴포넌트를 기존 BossLLM.prefab에 <b>가산</b>한다 (기존 자식·값 무손상):
        /// EnemyGun(예측탄 발사기) + 마젠타 텔레그래프 자식 3종 + <see cref="EliteModifier"/> +
        /// <c>_zoneSprite</c> 배선, 그리고 HUD에 <see cref="BossPhaseOverlay"/>.
        /// 실행 후 <b>폰트 빌더를 마지막에 재실행</b>할 것 (CLAUDE.md 폰트 규칙).
        /// </summary>
        [MenuItem("Luddite/Setup/보스 P2 컴포넌트 보장 (§9 P2)")]
        public static void EnsurePhaseTwoComponents()
        {
            EnsurePrefabPhaseTwo();
            EnsurePhaseOverlayInScene();
        }

        private static void EnsurePrefabPhaseTwo()
        {
            EnemyStatsSO stats = AssetDatabase.LoadAssetAtPath<EnemyStatsSO>(STATS_PATH);
            PredictiveShotConfigSO predictiveConfig =
                AssetDatabase.LoadAssetAtPath<PredictiveShotConfigSO>(PREDICTIVE_CONFIG_PATH);
            GameObject projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PROJECTILE_PREFAB);
            Sprite circle = AssetDatabase.LoadAssetAtPath<Sprite>(CIRCLE_SPRITE);
            Sprite darkOrb = AssetDatabase.LoadAssetAtPath<Sprite>(DARK_ORB_SPRITE);
            Material spriteDefault = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

            if (stats == null || predictiveConfig == null || projectilePrefab == null || circle == null)
            {
                Debug.LogError("[BossSetup/P2] 선행 에셋 누락 — stats:" + (stats != null) +
                               " predictiveConfig:" + (predictiveConfig != null) +
                               " projectile:" + (projectilePrefab != null) + " circle:" + (circle != null) +
                               " / 먼저 '보스 프리팹·SO 보장'과 '엘리트 프리팹·SO 보장' 실행");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
            if (root == null)
            {
                Debug.LogError("[BossSetup/P2] BossLLM.prefab 없음 — 먼저 '보스 프리팹·SO 보장' 실행");
                return;
            }

            try
            {
                int changes = 0;
                Color magenta = Color.magenta;

                // ① 예측탄 발사기 — EliteModifier가 요구한다. 총구는 보스 몸통(반경 ~2u) 밖으로
                EnemyGun gun = root.GetComponentInChildren<EnemyGun>(true);
                if (gun == null)
                {
                    GameObject gunObject = new GameObject("Gun");
                    gunObject.transform.SetParent(root.transform, false);
                    gun = gunObject.AddComponent<EnemyGun>();
                    SerializedObject gunSo = new SerializedObject(gun);
                    gunSo.FindProperty("_stats").objectReferenceValue = stats;
                    gunSo.FindProperty("_projectilePrefab").objectReferenceValue =
                        projectilePrefab.GetComponent<Projectile>();
                    gunSo.FindProperty("_muzzleOffset").floatValue = 1.4f;   // BossLLM.SpawnProjectile과 동일
                    gunSo.ApplyModifiedPropertiesWithoutUndo();
                    changes++;
                }

                // ② 마젠타 텔레그래프 자식 3종 (EliteSetupTools와 동일 구조·순서, VFX 레이어)
                LineRenderer aimLine = FindChildComponent<LineRenderer>(root.transform, "AimLine");
                if (aimLine == null)
                {
                    GameObject lineObject = new GameObject("AimLine");
                    lineObject.transform.SetParent(root.transform, false);
                    aimLine = lineObject.AddComponent<LineRenderer>();
                    aimLine.useWorldSpace = true;
                    aimLine.positionCount = 2;
                    aimLine.startWidth = 0.09f;
                    aimLine.endWidth = 0.09f;
                    aimLine.material = spriteDefault;
                    aimLine.startColor = new Color(magenta.r, magenta.g, magenta.b, 0.85f);
                    aimLine.endColor = new Color(magenta.r, magenta.g, magenta.b, 0.85f);
                    ApplyVfxSorting(aimLine, 10);
                    lineObject.SetActive(false);
                    changes++;
                }

                Transform marker = root.transform.Find("TargetMarker");
                if (marker == null)
                {
                    GameObject markerObject = new GameObject("TargetMarker");
                    markerObject.transform.SetParent(root.transform, false);
                    // 루트 스케일 ×2가 곱해지므로 로컬 0.45 ≈ 엘리트 마커(0.9)와 같은 화면 크기
                    markerObject.transform.localScale = Vector3.one * 0.45f;
                    SpriteRenderer markerRenderer = markerObject.AddComponent<SpriteRenderer>();
                    markerRenderer.sprite = circle;
                    markerRenderer.color = new Color(magenta.r, magenta.g, magenta.b, 0.4f);
                    markerRenderer.material = spriteDefault;
                    ApplyVfxSorting(markerRenderer, 9);
                    markerObject.SetActive(false);
                    marker = markerObject.transform;
                    changes++;
                }

                TrailRenderer trail = FindChildComponent<TrailRenderer>(root.transform, "TrailTemplate");
                if (trail == null)
                {
                    GameObject trailObject = new GameObject("TrailTemplate");
                    trailObject.transform.SetParent(root.transform, false);
                    trail = trailObject.AddComponent<TrailRenderer>();
                    trail.time = 0.25f;
                    trail.startWidth = 0.16f;
                    trail.endWidth = 0f;
                    trail.material = spriteDefault;
                    trail.startColor = magenta;
                    trail.endColor = new Color(magenta.r, magenta.g, magenta.b, 0f);
                    ApplyVfxSorting(trail, 8);
                    trailObject.SetActive(false);
                    changes++;
                }

                // ③ EliteModifier — 부착만으로 HUD AI 패널의 "생존 시 표시"(ActiveCount)도 충족된다
                EliteModifier elite = root.GetComponent<EliteModifier>();
                if (elite == null)
                {
                    elite = root.AddComponent<EliteModifier>();
                    changes++;
                }
                SerializedObject eliteSo = new SerializedObject(elite);
                if (eliteSo.FindProperty("_config").objectReferenceValue == null)
                    eliteSo.FindProperty("_config").objectReferenceValue = predictiveConfig;
                if (eliteSo.FindProperty("_gun").objectReferenceValue == null)
                    eliteSo.FindProperty("_gun").objectReferenceValue = gun;
                if (eliteSo.FindProperty("_aimLine").objectReferenceValue == null)
                    eliteSo.FindProperty("_aimLine").objectReferenceValue = aimLine;
                if (eliteSo.FindProperty("_targetMarker").objectReferenceValue == null)
                    eliteSo.FindProperty("_targetMarker").objectReferenceValue = marker;
                if (eliteSo.FindProperty("_trailTemplate").objectReferenceValue == null)
                    eliteSo.FindProperty("_trailTemplate").objectReferenceValue = trail;
                if (eliteSo.FindProperty("_predictiveSprite").objectReferenceValue == null && darkOrb != null)
                    eliteSo.FindProperty("_predictiveSprite").objectReferenceValue = darkOrb;   // 실루엣 차별 (§10.4 선례)
                eliteSo.ApplyModifiedPropertiesWithoutUndo();

                // ④ BossLLM._zoneSprite — 장판·오라용 흰 원
                BossLLM boss = root.GetComponent<BossLLM>();
                if (boss != null)
                {
                    SerializedObject bossSo = new SerializedObject(boss);
                    if (bossSo.FindProperty("_zoneSprite").objectReferenceValue == null)
                    {
                        bossSo.FindProperty("_zoneSprite").objectReferenceValue = circle;
                        bossSo.ApplyModifiedPropertiesWithoutUndo();
                        changes++;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
                Debug.Log($"[BossSetup/P2] BossLLM.prefab P2 컴포넌트 {changes}건 변경 (0건 = 이미 완비 · 멱등 확인)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>새 렌더러는 VFX 레이어에 — Default는 Ground 밑이라 바닥에 깔린다 (D6 세션 2 결함의 재발 방지).</summary>
        private static void ApplyVfxSorting(Renderer renderer, int order)
        {
            int vfxId = SortingLayer.NameToID("VFX");
            if (vfxId != 0) renderer.sortingLayerID = vfxId;
            else Debug.LogWarning("[BossSetup/P2] VFX Sorting Layer 없음 — Default 유지 (바닥 밑에 깔릴 수 있음)");
            renderer.sortingOrder = order;
        }

        private static T FindChildComponent<T>(Transform root, string childName) where T : Component
        {
            Transform child = root.Find(childName);
            return child != null ? child.GetComponent<T>() : null;
        }

        /// <summary>HUD에 P2 전환 오버레이 (USER MODEL LOADED / COPY COMPLETE / PATTERN: YOU) 보장.</summary>
        private static void EnsurePhaseOverlayInScene()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MAIN_SCENE_PATH)
            {
                Debug.LogError($"[BossSetup/P2] 활성 씬이 Main.unity가 아니다 (현재: {scene.path}) — 오버레이 생략");
                return;
            }

            GameObject canvas = GameObject.Find("GameScreensCanvas");
            Transform hudPanel = canvas != null ? canvas.transform.Find("HudPanel") : null;
            if (hudPanel == null)
            {
                Debug.LogError("[BossSetup/P2] HudPanel 없음 — 먼저 'HUD를 씬에 보장' 실행");
                return;
            }

            Transform existing = hudPanel.Find("BossPhaseOverlay");
            GameObject overlayRoot = existing != null ? existing.gameObject : new GameObject("BossPhaseOverlay");
            if (existing == null)
            {
                overlayRoot.transform.SetParent(hudPanel, false);
                StretchRect(overlayRoot);
            }

            Transform contentExisting = overlayRoot.transform.Find("Content");
            GameObject content = contentExisting != null ? contentExisting.gameObject : new GameObject("Content");
            if (contentExisting == null)
            {
                content.transform.SetParent(overlayRoot.transform, false);
                StretchRect(content);
            }

            Transform textExisting = content.transform.Find("MainText");
            GameObject textObject = textExisting != null ? textExisting.gameObject : new GameObject("MainText");
            TextMeshProUGUI mainText;
            if (textExisting == null)
            {
                textObject.transform.SetParent(content.transform, false);
                RectTransform rect = textObject.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, 160f);   // PREDICTION FAILED(중앙 +60)와 겹치지 않게 위쪽 띠
                rect.sizeDelta = new Vector2(1200f, 90f);
                mainText = textObject.AddComponent<TextMeshProUGUI>();
                mainText.text = "PATTERN: YOU";
                mainText.fontSize = 64f;
                mainText.fontStyle = FontStyles.Bold;
                mainText.color = new Color(1f, 0.35f, 1f, 1f);   // 마젠타 — AI가 나를 읽은 결과의 선언 (§10.4)
                mainText.alignment = TextAlignmentOptions.Center;
                mainText.raycastTarget = false;
            }
            else
            {
                mainText = textObject.GetComponent<TextMeshProUGUI>();
            }

            BossPhaseOverlay overlay = overlayRoot.GetComponent<BossPhaseOverlay>();
            if (overlay == null) overlay = overlayRoot.AddComponent<BossPhaseOverlay>();
            SerializedObject overlaySo = new SerializedObject(overlay);
            overlaySo.FindProperty("_content").objectReferenceValue = content;
            overlaySo.FindProperty("_mainText").objectReferenceValue = mainText;
            overlaySo.ApplyModifiedPropertiesWithoutUndo();
            content.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BossSetup/P2] BossPhaseOverlay 보장 — ⚠️ 새 TMP 텍스트가 생겼으면 폰트 빌더('한글 폰트 세팅')를 마지막에 재실행할 것");
        }

        private static void StretchRect(GameObject target)
        {
            RectTransform rect = target.GetComponent<RectTransform>();
            if (rect == null) rect = target.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
