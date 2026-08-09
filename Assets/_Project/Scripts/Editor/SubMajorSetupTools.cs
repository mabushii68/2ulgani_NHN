// 세부전공 선택 배선 빌더 (D7) — 멱등(이미 있으면 갱신만).
// WaveIntervalPanel에 세부전공 카드 3장 + SubMajorPanel 컴포넌트를 얹는다.
// 카드 자리는 UpgradeCard와 같은 띠(y -110)를 그대로 쓴다 — 첫 인터벌에는 업그레이드
// 카드가 숨고 세부전공 카드만 나오므로 겹치지 않는다 (UpgradePanel 양보 로직).
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Luddite.Combat;
using Luddite.Core;
using Luddite.Data;
using Luddite.UI;

namespace Luddite.EditorTools
{
    public static class SubMajorSetupTools
    {
        private const string MAIN_SCENE_PATH = "Assets/_Project/Scenes/Main.unity";

        private static readonly Color CARD_BG = new Color(0.13f, 0.13f, 0.18f, 1f); // UpgradeCard와 동일
        private static readonly Color TEXT_MAIN = new Color(0.92f, 0.92f, 0.95f, 1f);

        [MenuItem("Luddite/Setup/세부전공 선택 배선 (첫 인터벌)")]
        public static void EnsureSubMajorSelect()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[SubMajorSetup] 플레이 모드에서는 씬을 편집하지 않는다");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MAIN_SCENE_PATH)
            {
                Debug.LogError($"[SubMajorSetup] 활성 씬이 Main.unity가 아니다 (현재: {scene.path})");
                return;
            }

            GameObject canvas = GameObject.Find("GameScreensCanvas");
            Transform panel = canvas != null ? canvas.transform.Find("WaveIntervalPanel") : null;
            if (panel == null)
            {
                Debug.LogError("[SubMajorSetup] WaveIntervalPanel 없음 — 'GameState 골격을 씬에 보장' 먼저 실행");
                return;
            }

            Button[] cardButtons = new Button[3];
            TMP_Text[] cardNames = new TMP_Text[3];

            for (int i = 0; i < 3; i++)
            {
                string cardName = $"SubMajorCard{i}";
                Transform found = panel.Find(cardName);
                GameObject card = found != null ? found.gameObject : new GameObject(cardName, typeof(RectTransform));
                card.transform.SetParent(panel, false);

                RectTransform rect = card.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(-380f + i * 380f, -110f); // UpgradeCard와 같은 띠
                rect.sizeDelta = new Vector2(340f, 240f);

                Image image = card.GetComponent<Image>();
                if (image == null) image = card.AddComponent<Image>();
                image.color = CARD_BG;

                Button button = card.GetComponent<Button>();
                if (button == null) button = card.AddComponent<Button>();
                button.targetGraphic = image;
                cardButtons[i] = button;

                cardNames[i] = EnsureNameText(card);
            }

            SubMajorPanel subMajorPanel = panel.GetComponent<SubMajorPanel>();
            if (subMajorPanel == null) subMajorPanel = panel.gameObject.AddComponent<SubMajorPanel>();

            Transform bodyText = panel.Find("BodyText");

            SerializedObject so = new SerializedObject(subMajorPanel);
            so.FindProperty("_gameManager").objectReferenceValue = Object.FindFirstObjectByType<GameManager>();
            so.FindProperty("_bodyText").objectReferenceValue =
                bodyText != null ? bodyText.GetComponent<TMP_Text>() : null;
            SerializedProperty buttonsProperty = so.FindProperty("_cardButtons");
            SerializedProperty namesProperty = so.FindProperty("_cardNames");
            buttonsProperty.arraySize = cardButtons.Length;
            namesProperty.arraySize = cardNames.Length;
            for (int i = 0; i < cardButtons.Length; i++)
            {
                buttonsProperty.GetArrayElementAtIndex(i).objectReferenceValue = cardButtons[i];
                namesProperty.GetArrayElementAtIndex(i).objectReferenceValue = cardNames[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SubMajorSetup] 세부전공 선택 배선 완료 — 카드 3장 / scene saved={saved}");
        }

        // ── 세부전공 탄막 배선 (D7 — 사람 지정 매핑) ──
        // 어문계=펜 / 상경계=돈 / 법조계=책 / 자연과학=숫자 / 공학=기계(번개) /
        // 컴퓨터과학=컴퓨터 / 체육=공 / 미술=붓 / 음악=음표.
        // 스프라이트 매핑은 이 빌더가 소유한다 — 재실행하면 아래 표로 재설정된다.

        private const string BULLET_SET_PATH = "Assets/_Project/SO/SubMajorBulletSet.asset";

        private static readonly (string field, string spritePath)[] BULLET_MAP =
        {
            ("_linguistics",       "Assets/_Project/Sprites/Icons/Procedural/Proc_Pencil.png"),
            ("_commerce",          "Assets/_Project/Sprites/Icons/Icon_092_Coin.png"),
            ("_law",               "Assets/_Project/Sprites/Icons/Icon_248_Scroll.png"),
            ("_naturalScience",    "Assets/_Project/Sprites/Icons/Icon_234_Plus.png"),
            ("_engineering",       "Assets/_Project/Sprites/Icons/Procedural/Proc_Bolt.png"),
            ("_computerScience",   "Assets/_Project/Sprites/Icons/Procedural/Proc_Monitor.png"),
            ("_physicalEducation", "Assets/_Project/Sprites/Icons/Procedural/Proc_Ball.png"),
            ("_fineArts",          "Assets/_Project/Sprites/Icons/Icon_261_Brush.png"),
            ("_music",             "Assets/_Project/Sprites/Icons/Procedural/Proc_Note.png"),
        };

        [MenuItem("Luddite/Setup/세부전공 탄막 배선 (9종)")]
        public static void EnsureSubMajorBullets()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[SubMajorSetup] 플레이 모드에서는 씬을 편집하지 않는다");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MAIN_SCENE_PATH)
            {
                Debug.LogError($"[SubMajorSetup] 활성 씬이 Main.unity가 아니다 (현재: {scene.path})");
                return;
            }

            // 1) SO 인스턴스 보장 + 스프라이트 9종 배선
            SubMajorBulletSetSO bulletSet = AssetDatabase.LoadAssetAtPath<SubMajorBulletSetSO>(BULLET_SET_PATH);
            if (bulletSet == null)
            {
                bulletSet = ScriptableObject.CreateInstance<SubMajorBulletSetSO>();
                AssetDatabase.CreateAsset(bulletSet, BULLET_SET_PATH);
            }

            SerializedObject bulletSo = new SerializedObject(bulletSet);
            int wired = 0;
            foreach (var (field, spritePath) in BULLET_MAP)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite == null)
                {
                    Debug.LogError($"[SubMajorSetup] 스프라이트 없음: {spritePath}");
                    continue;
                }
                bulletSo.FindProperty(field).objectReferenceValue = sprite;
                wired++;
            }
            bulletSo.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            // 2) 플레이어 무기에 배선
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            BasicWeapon weapon = playerObject != null ? playerObject.GetComponentInChildren<BasicWeapon>(true) : null;
            if (weapon == null)
            {
                Debug.LogError("[SubMajorSetup] Player의 BasicWeapon 없음 — 탄막 배선 실패");
                return;
            }

            SerializedObject weaponSo = new SerializedObject(weapon);
            weaponSo.FindProperty("_subMajorBullets").objectReferenceValue = bulletSet;
            weaponSo.FindProperty("_gameManager").objectReferenceValue = Object.FindFirstObjectByType<GameManager>();
            weaponSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SubMajorSetup] 세부전공 탄막 배선 완료 — 스프라이트 {wired}/9 / scene saved={saved}");
        }

        private static TMP_Text EnsureNameText(GameObject card)
        {
            Transform found = card.transform.Find("Name");
            GameObject textObject = found != null ? found.gameObject : new GameObject("Name", typeof(RectTransform));
            textObject.transform.SetParent(card.transform, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(320f, 120f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            if (text == null) text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 40f;
            text.color = TEXT_MAIN;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }
    }
}
