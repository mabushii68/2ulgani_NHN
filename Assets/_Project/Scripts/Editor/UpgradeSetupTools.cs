// 업그레이드 시스템 빌더 — 멱등(이미 있으면 값 보존).
// §8의 8종 SO 생성 + UpgradeManager 배선 + WaveIntervalPanel에 3택 카드 UI 추가.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Luddite.Core;
using Luddite.Data;
using Luddite.Player;
using Luddite.UI;

namespace Luddite.EditorTools
{
    public static class UpgradeSetupTools
    {
        private const string MAIN_SCENE_PATH = "Assets/_Project/Scenes/Main.unity";
        private const string SO_PATH_FORMAT = "Assets/_Project/SO/Upgrade_{0}.asset";

        private static readonly Color CARD_BG = new Color(0.13f, 0.13f, 0.18f, 1f);
        private static readonly Color TEXT_MAIN = new Color(0.92f, 0.92f, 0.95f, 1f);
        private static readonly Color TEXT_DIM = new Color(0.62f, 0.65f, 0.70f, 1f);

        [MenuItem("Luddite/Setup/업그레이드 시스템 보장 (§8)")]
        public static void EnsureUpgradeSystem()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[UpgradeSetup] 플레이 모드에서는 씬을 편집하지 않는다");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MAIN_SCENE_PATH)
            {
                Debug.LogError($"[UpgradeSetup] 활성 씬이 Main.unity가 아니다 (현재: {scene.path})");
                return;
            }

            // ── §8 표 그대로 8종 ──
            UpgradeSO[] pool =
            {
                Ensure("FirstAuthor", "논문 1저자", "FIRST AUTHOR",
                    "공격력이 20% 증가한다.", "ATTACK +20%",
                    UpgradeEffect.DamagePercent, 0.20f, 3, 1, true),
                Ensure("AllNighter", "벼락치기", "ALL-NIGHTER",
                    "연사 속도가 15% 증가한다.", "FIRE RATE +15%",
                    UpgradeEffect.FireRatePercent, 0.15f, 3, 1, true),
                Ensure("PerfectSchedule", "수강신청 올클", "PERFECT SCHEDULE",
                    "이동 속도가 10% 증가한다.", "MOVE SPEED +10%",
                    UpgradeEffect.MoveSpeedPercent, 0.10f, 3, 1, true),
                Ensure("Scholarship", "국가장학금", "SCHOLARSHIP",
                    "최대 HP가 25 증가하고 즉시 25 회복한다.", "MAX HP +25, HEAL 25",
                    UpgradeEffect.MaxHpFlat, 25f, 3, 1, true),
                Ensure("ResumePadding", "스펙 부풀리기", "RESUME PADDING",
                    "투사체 크기가 25% 증가한다.", "PROJECTILE SIZE +25%",
                    UpgradeEffect.ProjectileSizePercent, 0.25f, 2, 1, true),
                Ensure("MajorMastery", "전공 심화", "MAJOR MASTERY",
                    "전공별 무기 특성이 강화된다.", "ENHANCE MAJOR WEAPON",
                    UpgradeEffect.MajorMastery, 0.20f, 2, 1, false),   // D6 최종 무기 도입 시 편입
                Ensure("BehaviourCorrection", "행동교정", "BEHAVIOR CORRECTION",
                    "AI의 관측 데이터를 80% 지운다.", "ERASE 80% OF AI OBSERVATIONS",
                    UpgradeEffect.BehaviourCorrection, 0.2f, 0, 3, true),
                Ensure("DataFabrication", "논문조작", "DATA FABRICATION",
                    "AI의 논문에 조작된 데이터를 끼워 넣는다.", "PLANT FABRICATED DATA IN THE AI'S PAPER",
                    UpgradeEffect.DataFabrication, 8f, 0, 3, true),
            };

            // ── UpgradeManager 배선 ──
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
            {
                Debug.LogError("[UpgradeSetup] Player 태그 오브젝트 없음");
                return;
            }

            PlayerUpgrades playerUpgrades = playerObject.GetComponent<PlayerUpgrades>();
            if (playerUpgrades == null) playerUpgrades = playerObject.AddComponent<PlayerUpgrades>();

            GameObject host = GameObject.Find("UpgradeManager");
            if (host == null) host = new GameObject("UpgradeManager");
            UpgradeManager manager = host.GetComponent<UpgradeManager>();
            if (manager == null) manager = host.AddComponent<UpgradeManager>();

            SerializedObject managerSo = new SerializedObject(manager);
            SerializedProperty poolProperty = managerSo.FindProperty("_pool");
            poolProperty.arraySize = pool.Length;
            for (int i = 0; i < pool.Length; i++)
                poolProperty.GetArrayElementAtIndex(i).objectReferenceValue = pool[i];
            managerSo.FindProperty("_playerUpgrades").objectReferenceValue = playerUpgrades;
            managerSo.FindProperty("_playerHealth").objectReferenceValue = playerObject.GetComponent<PlayerHealth>();
            managerSo.FindProperty("_brain").objectReferenceValue = Object.FindFirstObjectByType<AIBrainRunner>();
            managerSo.FindProperty("_waveManager").objectReferenceValue = Object.FindFirstObjectByType<WaveManager>();
            managerSo.ApplyModifiedPropertiesWithoutUndo();

            // ── WaveIntervalPanel에 카드 3장 ──
            EnsureCards(manager);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[UpgradeSetup] 업그레이드 시스템 배선 완료 — SO 8종 / scene saved={saved}");
        }

        private static UpgradeSO Ensure(string fileKey, string nameKo, string nameEn,
            string tooltipKo, string tooltipEn, UpgradeEffect effect, float value,
            int maxStacks, int fromWave, bool inPool)
        {
            string path = string.Format(SO_PATH_FORMAT, fileKey);
            UpgradeSO existing = AssetDatabase.LoadAssetAtPath<UpgradeSO>(path);
            if (existing != null) return existing;   // 값 보존

            UpgradeSO upgrade = ScriptableObject.CreateInstance<UpgradeSO>();
            SerializedObject so = new SerializedObject(upgrade);
            so.FindProperty("_displayName").stringValue = nameKo;
            so.FindProperty("_displayNameEn").stringValue = nameEn;
            so.FindProperty("_tooltip").stringValue = tooltipKo;
            so.FindProperty("_tooltipEn").stringValue = tooltipEn;
            so.FindProperty("_effect").enumValueIndex = (int)effect;
            so.FindProperty("_value").floatValue = value;
            so.FindProperty("_maxStacks").intValue = maxStacks;
            so.FindProperty("_availableFromWave").intValue = fromWave;
            so.FindProperty("_inPool").boolValue = inPool;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(upgrade, path);
            Debug.Log($"[UpgradeSetup] Upgrade_{fileKey} 생성");
            return upgrade;
        }

        private static void EnsureCards(UpgradeManager manager)
        {
            GameObject canvas = GameObject.Find("GameScreensCanvas");
            Transform panel = canvas != null ? canvas.transform.Find("WaveIntervalPanel") : null;
            if (panel == null)
            {
                Debug.LogError("[UpgradeSetup] WaveIntervalPanel 없음 — 'GameState 골격을 씬에 보장' 먼저 실행");
                return;
            }

            Button[] cardButtons = new Button[3];
            TMP_Text[] cardNames = new TMP_Text[3];
            TMP_Text[] cardTooltips = new TMP_Text[3];

            for (int i = 0; i < 3; i++)
            {
                string cardName = $"UpgradeCard{i}";
                Transform found = panel.Find(cardName);
                GameObject card = found != null ? found.gameObject : new GameObject(cardName, typeof(RectTransform));
                card.transform.SetParent(panel, false);

                RectTransform rect = card.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                // y -110: CounterProtocol(95~205)와 NextWave(-320) 사이의 예약 띠 (-230~10)
                rect.anchoredPosition = new Vector2(-380f + i * 380f, -110f);
                rect.sizeDelta = new Vector2(340f, 240f);

                Image image = card.GetComponent<Image>();
                if (image == null) image = card.AddComponent<Image>();
                image.color = CARD_BG;

                Button button = card.GetComponent<Button>();
                if (button == null) button = card.AddComponent<Button>();
                button.targetGraphic = image;
                cardButtons[i] = button;

                cardNames[i] = EnsureCardText(card, "Name", new Vector2(0f, 70f), new Vector2(320f, 70f), 30f, TEXT_MAIN);
                cardTooltips[i] = EnsureCardText(card, "Tooltip", new Vector2(0f, -40f), new Vector2(300f, 120f), 20f, TEXT_DIM);
            }

            UpgradePanel upgradePanel = panel.GetComponent<UpgradePanel>();
            if (upgradePanel == null) upgradePanel = panel.gameObject.AddComponent<UpgradePanel>();

            Transform nextWaveButton = panel.Find("NextWaveButton");
            GameManager gameManager = Object.FindFirstObjectByType<GameManager>();

            SerializedObject so = new SerializedObject(upgradePanel);
            so.FindProperty("_upgradeManager").objectReferenceValue = manager;
            so.FindProperty("_gameManager").objectReferenceValue = gameManager;
            so.FindProperty("_nextWaveButton").objectReferenceValue =
                nextWaveButton != null ? nextWaveButton.GetComponent<Button>() : null;

            FillArray(so.FindProperty("_cardButtons"), cardButtons);
            FillArray(so.FindProperty("_cardNames"), cardNames);
            FillArray(so.FindProperty("_cardTooltips"), cardTooltips);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TMP_Text EnsureCardText(GameObject card, string name,
            Vector2 anchoredPosition, Vector2 size, float fontSize, Color color)
        {
            Transform found = card.transform.Find(name);
            GameObject textObject = found != null ? found.gameObject : new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(card.transform, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            if (text == null) text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void FillArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
