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
            // 텍스트 규칙 (§10.5): 인간 세계 = 한국어 원문 / AI 시스템 발화 = 영어 대문자 터미널체.
            // 타이틀 로고는 영문 유지 + 정체성 문구를 한국어 부제로 (D7 한국어 교체)
            GameObject titlePanel = EnsurePanel(canvasObject, "TitlePanel", PANEL_BG);
            EnsureText(titlePanel, "TitleText", "LUDDITE 2026", 96, TEXT_MAIN, new Vector2(0f, 160f), new Vector2(1400f, 140f));
            EnsureText(titlePanel, "SubtitleText", "AI는 당신의 플레이를 학습합니다. 그러니 AI에게 거짓말하세요.", 28, TEXT_DIM, new Vector2(0f, 40f), new Vector2(1400f, 60f));
            GameObject startButton = EnsureButton(titlePanel, "StartButton", "시작", BUTTON_BG, new Vector2(0f, -140f), new Vector2(360f, 72f));

            GameObject majorPanel = EnsurePanel(canvasObject, "MajorSelectPanel", PANEL_BG);
            EnsureText(majorPanel, "HeaderText", "전공을 선택하세요", 56, TEXT_MAIN, new Vector2(0f, 260f), new Vector2(1200f, 90f));
            // 간격 560: 버튼 폭 360 + 여백 200 — 박스 스프라이트 스킨이 시각적으로 붙어 보이던 문제 (D7)
            GameObject liberalButton = EnsureButton(majorPanel, "LiberalArtsButton", "문과\n<size=60%>펜은 칼보다 강하다</size>", MAJOR_BLUE, new Vector2(-560f, -40f), new Vector2(360f, 180f));
            GameObject scienceButton = EnsureButton(majorPanel, "ScienceButton", "이과\n<size=60%>증명 끝. (Q.E.D.)</size>", MAJOR_GREEN, new Vector2(0f, -40f), new Vector2(360f, 180f));
            GameObject artsButton = EnsureButton(majorPanel, "ArtsButton", "예체능\n<size=60%>영혼은 학습되지 않는다</size>", MAJOR_YELLOW, new Vector2(560f, -40f), new Vector2(360f, 180f));

            // 세로 배치 (위→아래): Header 330 / Body 258 / CounterProtocol 150 (DdaSetupTools) /
            // 업그레이드 카드 -110 (UpgradeSetupTools) / NextWave -320. 서로 겹치지 않게 예약된 띠.
            GameObject wavePanel = EnsurePanel(canvasObject, "WaveIntervalPanel", PANEL_BG);
            EnsureText(wavePanel, "HeaderText", "TARGET PROFILE", 56, TEXT_MAIN, new Vector2(0f, 330f), new Vector2(1200f, 80f));
            EnsureText(wavePanel, "BodyText", "AI가 당신의 회피 습관을 분석했습니다. 업그레이드를 고르면 다음 전투가 시작됩니다.", 24, TEXT_DIM, new Vector2(0f, 258f), new Vector2(1200f, 50f));
            GameObject nextWaveButton = EnsureButton(wavePanel, "NextWaveButton", "다음 전투로", BUTTON_BG, new Vector2(0f, -320f), new Vector2(360f, 72f));

            // 상자를 열면 이 패널이 뜬다 (D7 — 자동 오픈 폐기). 팝으로 들어와 "열었다"는 인과가 보이게.
            // PanelPopIn은 unscaled로 돈다 — 인터벌은 timeScale 0이라 scaled면 첫 프레임에서 멈춘다.
            if (wavePanel.GetComponent<PanelPopIn>() == null) wavePanel.AddComponent<PanelPopIn>();

            GameObject bossPanel = EnsurePanel(canvasObject, "BossIntroPanel", PANEL_BG);
            EnsureText(bossPanel, "IntroText", "최종 전투 — 거대 LLM", 72, TEXT_MAIN, new Vector2(0f, 40f), new Vector2(1500f, 110f));
            EnsureText(bossPanel, "SubText", "INITIALIZING...", 30, TEXT_DIM, new Vector2(0f, -60f), new Vector2(1200f, 60f));

            // 세로 배치: 승패 380 / 별명 280 / 요약 200 / 통계·히스토그램 -40 / 코멘트 -240 / 버튼 -340
            // (별명~코멘트는 ResultSetupTools가 얹는다 — §13 프로필)
            GameObject resultPanel = EnsurePanel(canvasObject, "ResultPanel", PANEL_BG);
            GameObject resultMessage = EnsureText(resultPanel, "ResultMessage", "RESULT", 38, TEXT_MAIN, new Vector2(0f, 380f), new Vector2(1500f, 80f));
            GameObject resultToTitle = EnsureButton(resultPanel, "ToTitleButton", "타이틀로", BUTTON_BG, new Vector2(0f, -340f), new Vector2(360f, 72f));

            GameObject pausePanel = EnsurePanel(canvasObject, "PausePanel", PAUSE_BG);
            EnsureText(pausePanel, "HeaderText", "일시정지", 64, TEXT_MAIN, new Vector2(0f, 140f), new Vector2(800f, 100f));
            GameObject resumeButton = EnsureButton(pausePanel, "ResumeButton", "계속", BUTTON_BG, new Vector2(0f, -20f), new Vector2(360f, 72f));
            GameObject pauseToTitle = EnsureButton(pausePanel, "ToTitleButton", "타이틀로", BUTTON_BG, new Vector2(0f, -120f), new Vector2(360f, 72f));

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
