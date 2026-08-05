// HUD(§10.1) 씬 배선 도구 — 멱등(이미 있으면 갱신만), 기존 오브젝트를 지우지 않는다.
// GameFlowSetupTools가 만든 GameScreensCanvas 아래에 HudPanel을 얹는다.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Luddite.Core;
using Luddite.Player;
using Luddite.UI;

namespace Luddite.EditorTools
{
    public static class HudSetupTools
    {
        private const string MAIN_SCENE_PATH = "Assets/_Project/Scenes/Main.unity";

        private static readonly Color BAR_BACKGROUND = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color BAR_FILL = new Color(0.45f, 0.9f, 0.55f, 1f);
        private static readonly Color PANEL_BACKGROUND = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color TEXT_MAIN = new Color(0.92f, 0.92f, 0.95f, 1f);

        [MenuItem("Luddite/Setup/HUD 배선 (§10.1)")]
        public static void EnsureHud()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[HudSetup] 플레이 모드에서는 씬을 편집하지 않는다");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MAIN_SCENE_PATH)
            {
                Debug.LogError($"[HudSetup] 활성 씬이 Main.unity가 아니다 (현재: {scene.path})");
                return;
            }

            GameObject canvas = GameObject.Find("GameScreensCanvas");
            GameScreens screens = canvas != null ? canvas.GetComponent<GameScreens>() : null;
            GameManager manager = Object.FindFirstObjectByType<GameManager>();
            AIBrainRunner brain = Object.FindFirstObjectByType<AIBrainRunner>();
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            PlayerHealth health = playerObject != null ? playerObject.GetComponent<PlayerHealth>() : null;

            if (canvas == null || screens == null || manager == null || brain == null || health == null)
            {
                Debug.LogError("[HudSetup] 선행 배선 누락 — canvas:" + (canvas != null) +
                               " screens:" + (screens != null) + " manager:" + (manager != null) +
                               " brain:" + (brain != null) + " health:" + (health != null) +
                               " / 먼저 'GameState 골격을 씬에 보장'과 'AIBrainRunner를 씬에 보장'을 실행");
                return;
            }

            // HudPanel — 배경 없는 전체 컨테이너. GameScreens가 Combat에서만 켠다
            GameObject hudPanel = EnsureChild(canvas, "HudPanel");
            Stretch(hudPanel);

            // ── AI 미니 패널 (우상단, §10.1) ──
            GameObject miniRoot = EnsureChild(hudPanel, "AiMiniPanel");
            RectTransform miniRect = miniRoot.GetComponent<RectTransform>();
            miniRect.anchorMin = Vector2.one;
            miniRect.anchorMax = Vector2.one;
            miniRect.pivot = Vector2.one;
            miniRect.anchoredPosition = new Vector2(-24f, -24f);
            miniRect.sizeDelta = new Vector2(460f, 52f);

            GameObject miniContent = EnsureChild(miniRoot, "Content");
            Stretch(miniContent);
            Image miniBackground = EnsureComponent<Image>(miniContent);
            miniBackground.color = PANEL_BACKGROUND;
            miniBackground.raycastTarget = false; // HUD가 조준 클릭을 가로채면 안 된다

            GameObject miniLabel = EnsureChild(miniContent, "Label");
            Stretch(miniLabel);
            TextMeshProUGUI label = EnsureComponent<TextMeshProUGUI>(miniLabel);
            label.text = "AI MODEL: LEARNING...";
            label.fontSize = 26f;
            label.color = TEXT_MAIN;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            AiMiniPanel mini = EnsureComponent<AiMiniPanel>(miniRoot);
            SerializedObject miniSo = new SerializedObject(mini);
            miniSo.FindProperty("_brain").objectReferenceValue = brain;
            miniSo.FindProperty("_content").objectReferenceValue = miniContent;
            miniSo.FindProperty("_label").objectReferenceValue = label;
            miniSo.FindProperty("_background").objectReferenceValue = miniBackground;
            miniSo.ApplyModifiedPropertiesWithoutUndo();

            // ── HP 바 (좌하단, §10.1) ──
            GameObject barRoot = EnsureChild(hudPanel, "HpBar");
            RectTransform barRect = barRoot.GetComponent<RectTransform>();
            barRect.anchorMin = Vector2.zero;
            barRect.anchorMax = Vector2.zero;
            barRect.pivot = Vector2.zero;
            barRect.anchoredPosition = new Vector2(24f, 24f);
            barRect.sizeDelta = new Vector2(360f, 28f);

            GameObject barBackground = EnsureChild(barRoot, "Background");
            Stretch(barBackground);
            Image backgroundImage = EnsureComponent<Image>(barBackground);
            backgroundImage.color = BAR_BACKGROUND;
            backgroundImage.raycastTarget = false;

            GameObject barFill = EnsureChild(barRoot, "Fill");
            RectTransform fillRect = barFill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            fillRect.pivot = new Vector2(0f, 0.5f);   // 왼쪽 기준으로 줄어들게 (HpBar가 scale.x 제어)
            Image fillImage = EnsureComponent<Image>(barFill);
            fillImage.color = BAR_FILL;
            fillImage.raycastTarget = false;

            GameObject majorIcon = EnsureChild(barRoot, "MajorIcon");
            RectTransform iconRect = majorIcon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(1f, 0.5f);
            iconRect.anchorMax = new Vector2(1f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(12f, 0f);
            iconRect.sizeDelta = new Vector2(28f, 28f);
            Image iconImage = EnsureComponent<Image>(majorIcon);
            iconImage.raycastTarget = false;

            HpBar bar = EnsureComponent<HpBar>(barRoot);
            SerializedObject barSo = new SerializedObject(bar);
            barSo.FindProperty("_health").objectReferenceValue = health;
            barSo.FindProperty("_gameManager").objectReferenceValue = manager;
            barSo.FindProperty("_fill").objectReferenceValue = fillRect;
            barSo.FindProperty("_majorIcon").objectReferenceValue = iconImage;
            barSo.ApplyModifiedPropertiesWithoutUndo();

            // GameScreens에 HUD 배선 + 초기 비활성 (Title에서 시작하므로)
            SerializedObject screensSo = new SerializedObject(screens);
            screensSo.FindProperty("_hudPanel").objectReferenceValue = hudPanel;
            screensSo.ApplyModifiedPropertiesWithoutUndo();
            hudPanel.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[HudSetup] HUD 배선 완료 (AI 미니 패널 + HP 바) / scene saved={saved}");
        }

        private static GameObject EnsureChild(GameObject parent, string name)
        {
            Transform found = parent.transform.Find(name);
            if (found != null) return found.gameObject;

            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static T EnsureComponent<T>(GameObject host) where T : Component
        {
            T component = host.GetComponent<T>();
            return component != null ? component : host.AddComponent<T>();
        }

        private static void Stretch(GameObject target)
        {
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
