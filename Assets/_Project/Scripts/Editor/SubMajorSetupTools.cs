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
using Luddite.Core;
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
