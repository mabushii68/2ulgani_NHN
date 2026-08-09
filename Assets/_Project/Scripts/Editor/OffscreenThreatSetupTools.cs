// 화면 밖 위협 화살표 배선 — 멱등. 추적 카메라의 조건부 동반 계약 (개정안 §3 보정 ①).
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Luddite.UI;

namespace Luddite.EditorTools
{
    public static class OffscreenThreatSetupTools
    {
        private const string MAIN_SCENE_PATH = "Assets/_Project/Scenes/Main.unity";
        private const string ARROW_SPRITE = "Assets/_Project/Sprites/UI/AimArrow.png";

        [MenuItem("Luddite/Setup/화면 밖 위협 화살표 배선 (개정안 §3)")]
        public static void EnsureIndicator()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[OffscreenSetup] 플레이 모드에서는 씬을 편집하지 않는다");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MAIN_SCENE_PATH)
            {
                Debug.LogError($"[OffscreenSetup] 활성 씬이 Main.unity가 아니다 (현재: {scene.path})");
                return;
            }

            GameObject canvas = GameObject.Find("GameScreensCanvas");
            Transform hudPanel = canvas != null ? canvas.transform.Find("HudPanel") : null;
            if (hudPanel == null)
            {
                Debug.LogError("[OffscreenSetup] HudPanel 없음 — 먼저 'HUD 배선' 실행");
                return;
            }

            Sprite arrow = AssetDatabase.LoadAssetAtPath<Sprite>(ARROW_SPRITE);
            if (arrow == null)
            {
                // 조용한 실패 금지 (D6 세션 2 교훈) — 스프라이트가 없으면 소리 내고 멈춘다
                Debug.LogError($"[OffscreenSetup] 화살표 스프라이트 없음: {ARROW_SPRITE}");
                return;
            }

            Transform existing = hudPanel.Find("OffscreenThreats");
            GameObject root = existing != null ? existing.gameObject : new GameObject("OffscreenThreats", typeof(RectTransform));
            if (existing == null)
            {
                root.transform.SetParent(hudPanel, false);
                root.transform.SetAsFirstSibling();   // 다른 HUD 요소(패널·바) 아래에 그려지게
                RectTransform rect = (RectTransform)root.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            OffscreenThreatIndicator indicator = root.GetComponent<OffscreenThreatIndicator>();
            if (indicator == null) indicator = root.AddComponent<OffscreenThreatIndicator>();
            SerializedObject so = new SerializedObject(indicator);
            so.FindProperty("_arrowSprite").objectReferenceValue = arrow;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[OffscreenSetup] OffscreenThreats 배선 완료 (멱등)");
        }
    }
}
