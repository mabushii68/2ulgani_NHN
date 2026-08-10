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

            // ── HP 바 (좌상단 — D7에 좌하단에서 이동, 사람 요청) ──
            GameObject barRoot = EnsureChild(hudPanel, "HpBar");
            RectTransform barRect = barRoot.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 1f);
            barRect.anchoredPosition = new Vector2(24f, -24f);
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

            // ── 무기·탄약 (우하단, D7 신규 — 사람 요청 "엔터 더 건전과 동일") ──
            EnsureAmmoCounter(hudPanel, playerObject);

            // ── PREDICTION FAILED 오버레이 (§10.3 — HUD와 같은 Combat 수명) ──
            GameObject overlayRoot = EnsureChild(hudPanel, "PredictionFailedOverlay");
            Stretch(overlayRoot);

            GameObject overlayContent = EnsureChild(overlayRoot, "Content");
            Stretch(overlayContent);

            GameObject flashObject = EnsureChild(overlayContent, "Flash");
            Stretch(flashObject);
            Image flashImage = EnsureComponent<Image>(flashObject);
            flashImage.color = new Color(1f, 1f, 1f, 0f);
            flashImage.raycastTarget = false;

            GameObject mainTextObject = EnsureChild(overlayContent, "MainText");
            RectTransform mainRect = mainTextObject.GetComponent<RectTransform>();
            CenterRect(mainRect, new Vector2(0f, 60f), new Vector2(1200f, 100f));
            TextMeshProUGUI mainText = EnsureComponent<TextMeshProUGUI>(mainTextObject);
            mainText.text = "PREDICTION FAILED";
            mainText.fontSize = 72f;
            mainText.fontStyle = FontStyles.Bold;
            mainText.color = TEXT_MAIN;
            mainText.alignment = TextAlignmentOptions.Center;
            mainText.raycastTarget = false;

            GameObject subTextObject = EnsureChild(overlayContent, "SubText");
            RectTransform subRect = subTextObject.GetComponent<RectTransform>();
            CenterRect(subRect, new Vector2(0f, -20f), new Vector2(900f, 50f));
            TextMeshProUGUI subText = EnsureComponent<TextMeshProUGUI>(subTextObject);
            subText.text = "";
            subText.fontSize = 34f;
            subText.color = new Color(1f, 0.35f, 1f, 1f); // 마젠타 계열 — AI 모델이 흔들리는 순간의 표기
            subText.alignment = TextAlignmentOptions.Center;
            subText.raycastTarget = false;

            PredictionFailedOverlay overlay = EnsureComponent<PredictionFailedOverlay>(overlayRoot);
            SerializedObject overlaySo = new SerializedObject(overlay);
            overlaySo.FindProperty("_content").objectReferenceValue = overlayContent;
            overlaySo.FindProperty("_flash").objectReferenceValue = flashImage;
            overlaySo.FindProperty("_mainText").objectReferenceValue = mainText;
            overlaySo.FindProperty("_subText").objectReferenceValue = subText;
            overlaySo.ApplyModifiedPropertiesWithoutUndo();
            overlayContent.SetActive(false);

            // GameScreens에 HUD 배선 + 초기 비활성 (Title에서 시작하므로)
            SerializedObject screensSo = new SerializedObject(screens);
            screensSo.FindProperty("_hudPanel").objectReferenceValue = hudPanel;
            screensSo.ApplyModifiedPropertiesWithoutUndo();
            hudPanel.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[HudSetup] HUD 배선 완료 (AI 미니 패널 + HP 바(좌상단) + 무기·탄약(우하단)) / scene saved={saved}");
        }

        /// <summary>
        /// 우하단 무기·탄약 표시. 아이콘은 <b>플레이어가 실제로 쏘는 투사체 스프라이트</b>(FireballBig)를 쓴다 —
        /// 신규 에셋 0이고, 화면에 나가는 탄과 같은 그림이라 무엇을 쏘는지가 그대로 읽힌다.
        /// </summary>
        private static void EnsureAmmoCounter(GameObject hudPanel, GameObject playerObject)
        {
            GameObject root = EnsureChild(hudPanel, "AmmoCounter");
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 24f);
            rect.sizeDelta = new Vector2(272f, 76f);

            GameObject bg = EnsureChild(root, "Background");
            Stretch(bg);
            Image bgImage = EnsureComponent<Image>(bg);
            bgImage.color = PANEL_BACKGROUND;
            bgImage.raycastTarget = false;

            // 무기 아이콘 (좌측)
            GameObject iconObject = EnsureChild(root, "WeaponIcon");
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(14f, 4f);
            iconRect.sizeDelta = new Vector2(52f, 52f);
            Image icon = EnsureComponent<Image>(iconObject);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            Sprite bullet = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Sprites/Projectiles/FireballBig.png");
            if (bullet != null) icon.sprite = bullet;
            else Debug.LogWarning("[HudSetup] FireballBig 스프라이트를 찾지 못함 — 무기 아이콘이 빈다");

            // 잔탄 텍스트 (우측)
            GameObject countObject = EnsureChild(root, "Count");
            RectTransform countRect = countObject.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0f, 0f);
            countRect.anchorMax = new Vector2(1f, 1f);
            countRect.offsetMin = new Vector2(74f, 10f);
            countRect.offsetMax = new Vector2(-14f, 0f);
            TextMeshProUGUI count = EnsureComponent<TextMeshProUGUI>(countObject);
            count.text = "30 / 30";
            count.fontSize = 34f;
            count.color = TEXT_MAIN;
            count.alignment = TextAlignmentOptions.Right;
            count.raycastTarget = false;

            // 재장전 게이지 (하단 얇은 바) — pivot 왼쪽이라 scale.x로 왼→오 충전
            GameObject fillObject = EnsureChild(root, "ReloadFill");
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 0f);
            fillRect.pivot = new Vector2(0f, 0f);
            fillRect.offsetMin = new Vector2(0f, 0f);
            fillRect.offsetMax = new Vector2(0f, 6f);
            Image fillImage = EnsureComponent<Image>(fillObject);
            fillImage.color = BAR_FILL;
            fillImage.raycastTarget = false;

            AmmoCounter counter = EnsureComponent<AmmoCounter>(root);
            SerializedObject so = new SerializedObject(counter);
            // IWeapon은 인터페이스라 직렬화되지 않는다 → 구현 MonoBehaviour를 넣고 런타임에 캐스팅한다
            MonoBehaviour weapon = playerObject != null
                ? playerObject.GetComponentInChildren<Luddite.Combat.BasicWeapon>() : null;
            so.FindProperty("_weaponSource").objectReferenceValue = weapon;
            so.FindProperty("_countLabel").objectReferenceValue = count;
            so.FindProperty("_weaponIcon").objectReferenceValue = icon;
            so.FindProperty("_reloadFill").objectReferenceValue = fillRect;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (weapon == null)
                Debug.LogWarning("[HudSetup] 플레이어에서 BasicWeapon을 찾지 못함 — AmmoCounter가 런타임에 Player 태그로 재탐색한다");
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

        private static void CenterRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
