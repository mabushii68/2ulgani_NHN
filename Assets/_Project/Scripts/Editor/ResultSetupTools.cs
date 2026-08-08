// 결과 화면 프로필 빌더 (§13) — 멱등. NicknameTable 시드 + ResultPanel 프로필 요소 배선.
// 별명 테이블 내용은 GDD 미지정(예시 1개만)이라 초안이며 기획 검토 대상 — SO에서 문구만 고치면 된다.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Luddite.Core;
using Luddite.Data;
using Luddite.UI;

namespace Luddite.EditorTools
{
    public static class ResultSetupTools
    {
        private const string MAIN_SCENE_PATH = "Assets/_Project/Scenes/Main.unity";
        private const string TABLE_PATH = "Assets/_Project/SO/NicknameTable_Default.asset";

        private static readonly Color TEXT_MAIN = new Color(0.92f, 0.92f, 0.95f, 1f);
        private static readonly Color TEXT_DIM = new Color(0.62f, 0.65f, 0.70f, 1f);
        private static readonly Color MAGENTA_SOFT = new Color(1f, 0.35f, 1f, 1f);   // AI의 분석 결과 = 마젠타 (§10.4)

        [MenuItem("Luddite/Setup/결과 화면 프로필 배선 (§13)")]
        public static void EnsureResultProfile()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[ResultSetup] 플레이 모드에서는 씬을 편집하지 않는다");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MAIN_SCENE_PATH)
            {
                Debug.LogError($"[ResultSetup] 활성 씬이 Main.unity가 아니다 (현재: {scene.path})");
                return;
            }

            GameObject canvas = GameObject.Find("GameScreensCanvas");
            Transform panel = canvas != null ? canvas.transform.Find("ResultPanel") : null;
            AIBrainRunner brain = Object.FindFirstObjectByType<AIBrainRunner>();
            if (panel == null || brain == null)
            {
                Debug.LogError("[ResultSetup] ResultPanel 또는 AIBrainRunner 없음 — 선행 Setup 실행 필요");
                return;
            }

            // 구 플레이스홀더 제거 (GameFlowSetupTools에서 생성 코드도 이미 삭제됨)
            Transform oldHint = panel.Find("ProfileHint");
            if (oldHint != null) Object.DestroyImmediate(oldHint.gameObject);

            NicknameTableSO table = EnsureNicknameTable();

            TMP_Text nickname = EnsureText(panel, "Nickname", new Vector2(0f, 280f), new Vector2(1400f, 90f), 56f, MAGENTA_SOFT, TextAlignmentOptions.Center, FontStyles.Bold);
            TMP_Text summary = EnsureText(panel, "SummaryLine", new Vector2(0f, 200f), new Vector2(1400f, 50f), 26f, TEXT_DIM, TextAlignmentOptions.Center, FontStyles.Normal);
            TMP_Text stats = EnsureText(panel, "StatsBlock", new Vector2(-360f, -40f), new Vector2(640f, 300f), 24f, TEXT_MAIN, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            TMP_Text histogram = EnsureText(panel, "HistogramBlock", new Vector2(380f, -40f), new Vector2(600f, 300f), 22f, TEXT_DIM, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            TMP_Text comment = EnsureText(panel, "Comment", new Vector2(0f, -250f), new Vector2(1200f, 50f), 28f, TEXT_MAIN, TextAlignmentOptions.Center, FontStyles.Italic);

            ResultProfile profile = panel.GetComponent<ResultProfile>();
            if (profile == null) profile = panel.gameObject.AddComponent<ResultProfile>();
            SerializedObject so = new SerializedObject(profile);
            so.FindProperty("_brain").objectReferenceValue = brain;
            so.FindProperty("_nicknameTable").objectReferenceValue = table;
            so.FindProperty("_nickname").objectReferenceValue = nickname;
            so.FindProperty("_summaryLine").objectReferenceValue = summary;
            so.FindProperty("_statsBlock").objectReferenceValue = stats;
            so.FindProperty("_histogramBlock").objectReferenceValue = histogram;
            so.FindProperty("_comment").objectReferenceValue = comment;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ResultSetup] 결과 화면 프로필 배선 완료 / scene saved={saved}");
        }

        /// <summary>3축 12조합 시드. 유일한 GDD 확정 항목은 원거리+고편향(+정지) = "겁쟁이 저격수".</summary>
        private static NicknameTableSO EnsureNicknameTable()
        {
            NicknameTableSO existing = AssetDatabase.LoadAssetAtPath<NicknameTableSO>(TABLE_PATH);
            if (existing != null)
            {
                Debug.Log("[ResultSetup] NicknameTable 이미 존재 — 값 보존");
                return existing;
            }

            const string FAR_KO = "당신은 가까워지는 것을 싫어합니다.";
            const string FAR_EN = "YOU HATE GETTING CLOSE.";
            const string MID_KO = "당신은 안전한 거리를 알고 있습니다.";
            const string MID_EN = "YOU KNOW YOUR SAFE DISTANCE.";
            const string NEAR_KO = "당신은 겁이 없습니다.";
            const string NEAR_EN = "YOU HAVE NO FEAR.";

            NicknameTableSO table = ScriptableObject.CreateInstance<NicknameTableSO>();
            table.EditorSetEntries(new[]
            {
                new NicknameTableSO.Entry(DistanceBand.Far, true, false, "겁쟁이 저격수", "COWARD SNIPER", FAR_KO, FAR_EN),
                new NicknameTableSO.Entry(DistanceBand.Far, true, true, "도망치는 습관가", "FLEEING CREATURE OF HABIT", FAR_KO, FAR_EN),
                new NicknameTableSO.Entry(DistanceBand.Far, false, false, "거리의 철학자", "DISTANT PHILOSOPHER", FAR_KO, FAR_EN),
                new NicknameTableSO.Entry(DistanceBand.Far, false, true, "유령 포수", "PHANTOM GUNNER", FAR_KO, FAR_EN),
                new NicknameTableSO.Entry(DistanceBand.Mid, true, false, "한쪽 눈 파수꾼", "ONE-EYED SENTRY", MID_KO, MID_EN),
                new NicknameTableSO.Entry(DistanceBand.Mid, true, true, "규칙적인 무용수", "PREDICTABLE DANCER", MID_KO, MID_EN),
                new NicknameTableSO.Entry(DistanceBand.Mid, false, false, "침착한 도박사", "CALM GAMBLER", MID_KO, MID_EN),
                new NicknameTableSO.Entry(DistanceBand.Mid, false, true, "균형의 곡예사", "BALANCED ACROBAT", MID_KO, MID_EN),
                new NicknameTableSO.Entry(DistanceBand.Near, true, false, "고집 센 백병전가", "STUBBORN BRAWLER", NEAR_KO, NEAR_EN),
                new NicknameTableSO.Entry(DistanceBand.Near, true, true, "저돌적 회전목마", "RECKLESS CAROUSEL", NEAR_KO, NEAR_EN),
                new NicknameTableSO.Entry(DistanceBand.Near, false, false, "강철 심장", "IRON HEART", NEAR_KO, NEAR_EN),
                new NicknameTableSO.Entry(DistanceBand.Near, false, true, "예측 불가 검투사", "UNPREDICTABLE GLADIATOR", NEAR_KO, NEAR_EN),
            });

            AssetDatabase.CreateAsset(table, TABLE_PATH);
            AssetDatabase.SaveAssets();
            Debug.Log("[ResultSetup] NicknameTable_Default 생성 (초안 12종 — 기획 검토 대상)");
            return table;
        }

        private static TMP_Text EnsureText(Transform panel, string name, Vector2 anchoredPosition,
            Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment, FontStyles style)
        {
            Transform found = panel.Find(name);
            GameObject textObject = found != null ? found.gameObject : new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(panel, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            if (text == null) text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.raycastTarget = false;
            return text;
        }
    }
}
