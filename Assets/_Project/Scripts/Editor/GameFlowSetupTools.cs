// GameState 골격 씬 배선 도구 — 전부 멱등(이미 있으면 갱신만)하며 기존 오브젝트를 지우지 않는다.
// CLAUDE.md Scripts/Editor 규칙: 파괴적 빌더 금지 → 멱등하게 작성해 상주시킨다 (SceneSetupTools와 동일 패턴).
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Luddite.Core;
using Luddite.UI;

namespace Luddite.EditorTools
{
    public static class GameFlowSetupTools
    {
        private const string MAIN_SCENE_PATH = "Assets/_Project/Scenes/Main.unity";

        // 플레이스홀더 팔레트 — 마젠타 금지 (🔴 §10.4: 마젠타 = AI 위협 전용)
        private static readonly Color PANEL_BG = new Color(0.02f, 0.02f, 0.05f, 0.95f);
        private static readonly Color PAUSE_BG = new Color(0f, 0f, 0f, 0.6f); // §1.1 반투명 오버레이
        private static readonly Color BUTTON_BG = new Color(0.16f, 0.16f, 0.20f, 1f);
        private static readonly Color TEXT_MAIN = new Color(0.92f, 0.92f, 0.95f, 1f);
        private static readonly Color TEXT_DIM = new Color(0.55f, 0.58f, 0.62f, 1f);

        // 전공색 (GDD §4.1) — 버튼 배경에만 은은하게
        private static readonly Color MAJOR_BLUE = new Color(0.13f, 0.22f, 0.42f, 1f);
        private static readonly Color MAJOR_GREEN = new Color(0.12f, 0.34f, 0.20f, 1f);
        private static readonly Color MAJOR_YELLOW = new Color(0.42f, 0.36f, 0.10f, 1f);

        [MenuItem("Luddite/Setup/GameState 골격을 씬에 보장")]
        public static void EnsureGameFlow()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[GameFlowSetup] 플레이 모드에서는 씬을 편집하지 않는다");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MAIN_SCENE_PATH)
            {
                Debug.LogError($"[GameFlowSetup] 활성 씬이 Main.unity가 아니다 (현재: {scene.path}). " +
                               "Main.unity를 열고 다시 실행하세요.");
                return;
            }

            // 1) GameManager
            GameObject flowHost = EnsureRootObject("GameManager");
            GameManager manager = EnsureComponent<GameManager>(flowHost);

            // 2) EventSystem (구 Input Manager 경로 = StandaloneInputModule)
            GameObject eventSystem = EnsureRootObject("EventSystem");
            EnsureComponent<EventSystem>(eventSystem);
            EnsureComponent<StandaloneInputModule>(eventSystem);

            // 3) Canvas
            GameObject canvasObject = EnsureRootObject("GameScreensCanvas");
            Canvas canvas = EnsureComponent<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = EnsureComponent<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); // 해상도 규칙 (CLAUDE.md)
            scaler.matchWidthOrHeight = 0.5f;
            EnsureComponent<GraphicRaycaster>(canvasObject);
            GameScreens screens = EnsureComponent<GameScreens>(canvasObject);

            // 4) 패널 6종 + 내용물
            GameObject titlePanel = EnsurePanel(canvasObject, "TitlePanel", PANEL_BG);
            EnsureText(titlePanel, "TitleText", "LUDDITE 2026", 96, TEXT_MAIN, new Vector2(0f, 160f), new Vector2(1400f, 140f));
            EnsureText(titlePanel, "SubtitleText", "AI LEARNS HOW YOU DODGE. SO LIE TO IT.", 28, TEXT_DIM, new Vector2(0f, 40f), new Vector2(1400f, 60f));
            GameObject startButton = EnsureButton(titlePanel, "StartButton", "START", BUTTON_BG, new Vector2(0f, -140f), new Vector2(360f, 72f));

            GameObject majorPanel = EnsurePanel(canvasObject, "MajorSelectPanel", PANEL_BG);
            EnsureText(majorPanel, "HeaderText", "SELECT MAJOR", 56, TEXT_MAIN, new Vector2(0f, 260f), new Vector2(1200f, 90f));
            GameObject liberalButton = EnsureButton(majorPanel, "LiberalArtsButton", "LIBERAL ARTS", MAJOR_BLUE, new Vector2(-420f, -40f), new Vector2(360f, 180f));
            GameObject scienceButton = EnsureButton(majorPanel, "ScienceButton", "SCIENCE", MAJOR_GREEN, new Vector2(0f, -40f), new Vector2(360f, 180f));
            GameObject artsButton = EnsureButton(majorPanel, "ArtsButton", "ARTS", MAJOR_YELLOW, new Vector2(420f, -40f), new Vector2(360f, 180f));

            // 세로 배치 (위→아래): Header 330 / Body 258 / CounterProtocol 150 (DdaSetupTools) /
            // 업그레이드 카드 -110 (UpgradeSetupTools) / NextWave -320. 서로 겹치지 않게 예약된 띠.
            GameObject wavePanel = EnsurePanel(canvasObject, "WaveIntervalPanel", PANEL_BG);
            EnsureText(wavePanel, "HeaderText", "TARGET PROFILE", 56, TEXT_MAIN, new Vector2(0f, 330f), new Vector2(1200f, 80f));
            EnsureText(wavePanel, "BodyText", "( LEARNING STATS — D5 )", 24, TEXT_DIM, new Vector2(0f, 258f), new Vector2(1200f, 50f));
            GameObject nextWaveButton = EnsureButton(wavePanel, "NextWaveButton", "NEXT WAVE", BUTTON_BG, new Vector2(0f, -320f), new Vector2(360f, 72f));

            GameObject bossPanel = EnsurePanel(canvasObject, "BossIntroPanel", PANEL_BG);
            EnsureText(bossPanel, "IntroText", "WAVE 7 — MASSIVE LLM", 72, TEXT_MAIN, new Vector2(0f, 40f), new Vector2(1500f, 110f));
            EnsureText(bossPanel, "SubText", "INITIALIZING...", 30, TEXT_DIM, new Vector2(0f, -60f), new Vector2(1200f, 60f));

            GameObject resultPanel = EnsurePanel(canvasObject, "ResultPanel", PANEL_BG);
            GameObject resultMessage = EnsureText(resultPanel, "ResultMessage", "RESULT", 48, TEXT_MAIN, new Vector2(0f, 80f), new Vector2(1500f, 200f));
            EnsureText(resultPanel, "ProfileHint", "( TARGET PROFILE — D5 )", 24, TEXT_DIM, new Vector2(0f, -80f), new Vector2(1200f, 50f));
            GameObject resultToTitle = EnsureButton(resultPanel, "ToTitleButton", "TO TITLE", BUTTON_BG, new Vector2(0f, -220f), new Vector2(360f, 72f));

            GameObject pausePanel = EnsurePanel(canvasObject, "PausePanel", PAUSE_BG);
            EnsureText(pausePanel, "HeaderText", "PAUSED", 64, TEXT_MAIN, new Vector2(0f, 140f), new Vector2(800f, 100f));
            GameObject resumeButton = EnsureButton(pausePanel, "ResumeButton", "RESUME", BUTTON_BG, new Vector2(0f, -20f), new Vector2(360f, 72f));
            GameObject pauseToTitle = EnsureButton(pausePanel, "ToTitleButton", "TO TITLE", BUTTON_BG, new Vector2(0f, -120f), new Vector2(360f, 72f));

            // 5) 초기 활성 상태 — 플레이 진입 시 GameManager.Start가 Title 통지로 맞춰 주지만,
            //    에디터에서 씬을 열었을 때도 타이틀만 보이는 것이 자연스럽다
            titlePanel.SetActive(true);
            majorPanel.SetActive(false);
            wavePanel.SetActive(false);
            bossPanel.SetActive(false);
            resultPanel.SetActive(false);
            pausePanel.SetActive(false);

            // 6) GameScreens 직렬화 필드 배선
            SerializedObject so = new SerializedObject(screens);
            so.FindProperty("_gameManager").objectReferenceValue = manager;
            so.FindProperty("_titlePanel").objectReferenceValue = titlePanel;
            so.FindProperty("_majorSelectPanel").objectReferenceValue = majorPanel;
            so.FindProperty("_waveIntervalPanel").objectReferenceValue = wavePanel;
            so.FindProperty("_bossIntroPanel").objectReferenceValue = bossPanel;
            so.FindProperty("_resultPanel").objectReferenceValue = resultPanel;
            so.FindProperty("_pausePanel").objectReferenceValue = pausePanel;
            so.FindProperty("_startButton").objectReferenceValue = startButton.GetComponent<Button>();
            so.FindProperty("_majorLiberalArtsButton").objectReferenceValue = liberalButton.GetComponent<Button>();
            so.FindProperty("_majorScienceButton").objectReferenceValue = scienceButton.GetComponent<Button>();
            so.FindProperty("_majorArtsButton").objectReferenceValue = artsButton.GetComponent<Button>();
            so.FindProperty("_nextWaveButton").objectReferenceValue = nextWaveButton.GetComponent<Button>();
            so.FindProperty("_resumeButton").objectReferenceValue = resumeButton.GetComponent<Button>();
            so.FindProperty("_pauseToTitleButton").objectReferenceValue = pauseToTitle.GetComponent<Button>();
            so.FindProperty("_resultToTitleButton").objectReferenceValue = resultToTitle.GetComponent<Button>();
            so.FindProperty("_resultMessage").objectReferenceValue = resultMessage.GetComponent<TMP_Text>();
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[GameFlowSetup] GameState 골격 배선 완료 / scene saved={saved}");
        }

        // ── 멱등 헬퍼 ──

        private static GameObject EnsureRootObject(string name)
        {
            GameObject found = GameObject.Find(name);
            return found != null ? found : new GameObject(name);
        }

        private static T EnsureComponent<T>(GameObject host) where T : Component
        {
            T component = host.GetComponent<T>();
            return component != null ? component : host.AddComponent<T>();
        }

        private static GameObject EnsureChild(GameObject parent, string name)
        {
            Transform found = parent.transform.Find(name);
            if (found != null) return found.gameObject;

            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        /// <summary>화면 전체를 덮는 패널 (배경 Image 포함).</summary>
        private static GameObject EnsurePanel(GameObject canvas, string name, Color background)
        {
            GameObject panel = EnsureChild(canvas, name);

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = EnsureComponent<Image>(panel);
            image.color = background;
            image.raycastTarget = true; // 패널 아래(아레나)로 클릭이 새지 않게

            return panel;
        }

        private static GameObject EnsureText(GameObject parent, string name, string content,
            float fontSize, Color color, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject textObject = EnsureChild(parent, name);
            SetCenterRect(textObject, anchoredPosition, size);

            TextMeshProUGUI text = EnsureComponent<TextMeshProUGUI>(textObject);
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            return textObject;
        }

        private static GameObject EnsureButton(GameObject parent, string name, string label,
            Color background, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject buttonObject = EnsureChild(parent, name);
            SetCenterRect(buttonObject, anchoredPosition, size);

            Image image = EnsureComponent<Image>(buttonObject);
            image.color = background;

            Button button = EnsureComponent<Button>(buttonObject);
            button.targetGraphic = image;

            GameObject labelObject = EnsureChild(buttonObject, "Label");
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = EnsureComponent<TextMeshProUGUI>(labelObject);
            text.text = label;
            text.fontSize = 28f;
            text.color = TEXT_MAIN;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            return buttonObject;
        }

        private static void SetCenterRect(GameObject target, Vector2 anchoredPosition, Vector2 size)
        {
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
