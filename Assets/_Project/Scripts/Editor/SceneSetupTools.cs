// 씬 배선 도구 — 전부 멱등(이미 있으면 건너뛰기)하며 기존 오브젝트를 지우지 않는다.
// CLAUDE.md 폴더 규칙: 파괴적 빌더는 삭제하거나 멱등하게 작성할 것. 이 파일은 후자를 택했으므로 상주한다.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Luddite.Core;
using Luddite.Data;

namespace Luddite.EditorTools
{
    public static class SceneSetupTools
    {
        private const string MAIN_SCENE_PATH = "Assets/_Project/Scenes/Main.unity";
        private const string PREDICTOR_CONFIG_PATH = "Assets/_Project/SO/PredictorConfig_Default.asset";
        private const string RUNNER_NAME = "AIBrainRunner";

        [MenuItem("Luddite/Setup/AIBrainRunner를 씬에 보장")]
        public static void EnsureAIBrainRunner()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[SceneSetup] 플레이 모드에서는 씬을 편집하지 않는다");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MAIN_SCENE_PATH)
            {
                Debug.LogError($"[SceneSetup] 활성 씬이 Main.unity가 아니다 (현재: {scene.path}). " +
                               "Main.unity를 열고 다시 실행하세요.");
                return;
            }

            PredictorConfigSO config = AssetDatabase.LoadAssetAtPath<PredictorConfigSO>(PREDICTOR_CONFIG_PATH);
            if (config == null)
            {
                Debug.LogError($"[SceneSetup] PredictorConfigSO를 찾지 못함: {PREDICTOR_CONFIG_PATH}");
                return;
            }

            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
            {
                Debug.LogError("[SceneSetup] Player 태그 오브젝트가 없다");
                return;
            }

            GameObject host = GameObject.Find(RUNNER_NAME);
            bool created = host == null;
            if (created) host = new GameObject(RUNNER_NAME);

            AIBrainRunner runner = host.GetComponent<AIBrainRunner>();
            if (runner == null) runner = host.AddComponent<AIBrainRunner>();

            SerializedObject data = new SerializedObject(runner);
            data.FindProperty("_config").objectReferenceValue = config;
            data.FindProperty("_player").objectReferenceValue = playerObject.transform;
            data.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);

            Debug.Log($"[SceneSetup] AIBrainRunner {(created ? "생성" : "갱신")} " +
                      $"(config={config.name}, player={playerObject.name}) / scene saved={saved}");
        }
    }
}
