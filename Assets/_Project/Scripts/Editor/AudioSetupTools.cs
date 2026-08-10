// 오디오 배선 빌더 — 멱등. Audio/의 자체 생성 클립(§12)을 씬 AudioDirector에 연결한다.
// 클립 재생성은 Audio/Generator~/generate_sfx.py (결정론 — 재실행해도 같은 파일).
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Luddite.Core;

namespace Luddite.EditorTools
{
    public static class AudioSetupTools
    {
        private const string MAIN_SCENE_PATH = "Assets/_Project/Scenes/Main.unity";
        private const string AUDIO_DIR = "Assets/_Project/Audio/";

        // (직렬화 필드, 클립 파일명) — 이름이 어긋나면 로그로 드러난다 (D6 세션 2의 조용한 실패 교훈)
        private static readonly (string field, string clip)[] WIRING =
        {
            ("_playerShoot", "Sfx_PlayerShoot"),
            ("_playerHit", "Sfx_PlayerHit"),
            ("_enemyDeath", "Sfx_EnemyDeath"),
            ("_aiAnalyze", "Sfx_AiAnalyze"),
            ("_predictionShot", "Sfx_PredictionShot"),
            ("_predictionFailed", "Sfx_PredictionFailed"),
            ("_waveClear", "Sfx_WaveClear"),
            ("_uiButton", "Sfx_UiButton"),
            ("_bossPhase", "Sfx_BossPhase"),
            ("_combatLoop", "Bgm_CombatLoop"),
        };

        [MenuItem("Luddite/Setup/오디오 배선 (§12)")]
        public static void EnsureAudioDirector()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[AudioSetup] 플레이 모드에서는 씬을 편집하지 않는다");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MAIN_SCENE_PATH)
            {
                Debug.LogError($"[AudioSetup] 활성 씬이 Main.unity가 아니다 (현재: {scene.path})");
                return;
            }

            EnsureAudioListener();

            GameObject host = GameObject.Find("AudioDirector");
            if (host == null) host = new GameObject("AudioDirector");

            AudioDirector director = host.GetComponent<AudioDirector>();
            if (director == null) director = host.AddComponent<AudioDirector>();

            SerializedObject so = new SerializedObject(director);
            int wired = 0;
            int missing = 0;
            foreach ((string field, string clip) in WIRING)
            {
                AudioClip loaded = AssetDatabase.LoadAssetAtPath<AudioClip>(AUDIO_DIR + clip + ".wav");
                if (loaded == null)
                {
                    // 조용한 continue 금지 — 파일명 오타·미생성이 침묵하면 "소리 없는 게임"으로만 드러난다
                    Debug.LogError($"[AudioSetup] 클립 없음: {AUDIO_DIR}{clip}.wav — generate_sfx.py 실행 여부 확인");
                    missing++;
                    continue;
                }

                SerializedProperty property = so.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"[AudioSetup] AudioDirector에 필드 없음: {field}");
                    missing++;
                    continue;
                }

                if (property.objectReferenceValue != loaded)
                {
                    property.objectReferenceValue = loaded;
                    wired++;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[AudioSetup] 배선 {wired}건 변경 / 누락 {missing}건 (0/0 = 이미 완비 · 멱등 확인)");
        }

        /// <summary>
        /// 씬에 AudioListener를 보장한다. 리스너가 0개면 <b>AudioSource가 아무리 정상이어도 전부 무음</b>이며,
        /// 이 경우 Unity는 경고조차 내지 않는다 (2개 이상일 때만 경고) — D7에 실제로 낸 결함.
        /// URP 2D 템플릿의 Main Camera에 원래 붙어 있었으나 던전 카메라 재구성 과정에서 유실됐다.
        /// </summary>
        private static void EnsureAudioListener()
        {
            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (listeners.Length > 0)
            {
                if (listeners.Length > 1)
                    Debug.LogWarning($"[AudioSetup] AudioListener {listeners.Length}개 — 1개만 남겨야 한다 (Unity가 나머지를 무시)");
                return;
            }

            Camera camera = Camera.main != null
                ? Camera.main
                : Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogError("[AudioSetup] 씬에 카메라가 없어 AudioListener를 붙일 곳이 없다 — 무음 상태로 남는다");
                return;
            }

            camera.gameObject.AddComponent<AudioListener>();
            Debug.Log($"[AudioSetup] AudioListener 추가: {camera.name} (없으면 전부 무음이었다)");
        }
    }
}
