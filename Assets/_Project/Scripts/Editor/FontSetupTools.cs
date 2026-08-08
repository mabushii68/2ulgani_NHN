// 한글 폰트 세팅 빌더 (§10.5 "인간 세계 = 한국어") — 멱등.
// ttf를 Fonts/로 두고, 사람이 Font Asset Creator로 구운 SDF 애셋을 씬 전체에 적용한다.
//
// 왜 SDF인가 (D3에 비트맵을 시도했다 폐기한 결론):
//   Canvas Scaler가 Scale With Screen Size(기준 1920×1080)라 화면이 기준 해상도가 아니면 UI가
//   비정수 배율로 스케일된다. WebGL은 브라우저 캔버스 크기가 임의라 정수 배율이 사실상 안 나온다.
//   비트맵 렌더 모드(RASTER=1비트, SMOOTH=8비트 AA)는 그 상황에서 획이 빠지거나 뭉개진다.
//   SDF는 배율에 무관하게 형태가 유지된다 — 픽셀 폰트의 각이 아주 약간 둥글어지는 것이 대가.
//   ⚠️ 그러므로 이 폰트를 비트맵 모드로 다시 굽지 말 것. 같은 실패를 반복하게 된다.
//
// 씬 텍스트의 크기가 12의 배수(24/36/60/72/96)인 것은 비트맵 시도 때 픽셀 정합을 맞추려고
// 스냅한 흔적이다. SDF에서는 불필요한 제약이지만 보기에 문제없어 그대로 둔다.
//
// ⚠️ 다른 Setup 빌더(GameFlow/Hud/Upgrade/Result/Dda)는 텍스트를 만들 때 각자 폰트·크기를 지정한다.
//    그 빌더들을 다시 실행하면 폰트가 TMP 기본값으로 돌아갈 수 있으므로, **항상 이 빌더를 마지막에 실행**한다.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Luddite.EditorTools
{
    public static class FontSetupTools
    {
        private const string MAIN_SCENE_PATH = "Assets/_Project/Scenes/Main.unity";

        private const string FONT_DIR = "Assets/_Project/Fonts";
        private const string TTF_SRC = "Assets/_Project/Art/x10y12pxDenkiChipHangul.ttf";
        private const string TTF_DST = FONT_DIR + "/x10y12pxDenkiChipHangul.ttf";
        private const string SDF_ASSET_PATH = FONT_DIR + "/x10y12pxDenkiChipHangul SDF.asset";

        /// <summary>Dynamic 전환 시 쓸 아틀라스 1장 크기 (Static 4096²에서 축소).</summary>
        private const int DYNAMIC_ATLAS_SIZE = 1024;

        [MenuItem("Luddite/Setup/한글 폰트 세팅 (§10.5)")]
        public static void SetupKoreanFont()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FontSetup] 플레이 모드에서는 실행하지 않는다");
                return;
            }

            MoveTtfToFontsFolder();   // ttf가 이미 Fonts/에 있으면 아무 일도 하지 않는다

            TMP_FontAsset sdf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SDF_ASSET_PATH);
            if (sdf == null)
            {
                Debug.LogError($"[FontSetup] SDF 폰트 애셋이 없다 — {SDF_ASSET_PATH}\n" +
                               "Window > TextMeshPro > Font Asset Creator 에서 ttf로 구운 뒤 이 경로에 저장할 것");
                return;
            }

            ApplyAsTmpDefault(sdf);
            int replaced = ReplaceSceneFonts(sdf);

            AssetDatabase.SaveAssets();
            Debug.Log($"[FontSetup] 완료 — '{sdf.name}' 적용, 씬 텍스트 {replaced}개 교체");
        }

        // ── ttf 를 Fonts/ 로 이동 (GUID 보존) ─────────────────────────────────
        private static void MoveTtfToFontsFolder()
        {
            if (AssetDatabase.LoadAssetAtPath<Font>(TTF_DST) != null) return;   // 이미 이동됨
            if (AssetDatabase.LoadAssetAtPath<Font>(TTF_SRC) == null) return;   // 원본도 없으면 할 일 없음

            if (!AssetDatabase.IsValidFolder(FONT_DIR))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Fonts");
                Debug.Log($"[FontSetup] 폴더 생성 — {FONT_DIR}");
            }

            // MoveAsset은 .meta를 함께 옮겨 GUID를 보존한다 (탐색기로 옮기면 참조가 끊긴다)
            string error = AssetDatabase.MoveAsset(TTF_SRC, TTF_DST);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"[FontSetup] ttf 이동 실패 — {error}");
                return;
            }

            Debug.Log($"[FontSetup] ttf 이동 — {TTF_SRC} → {TTF_DST}");
        }

        // ── TMP 기본 폰트로 지정 ──────────────────────────────────────────────
        // 씬의 텍스트는 폰트를 명시 참조하므로 이것만으로는 안 바뀐다.
        // 이후 새로 만드는 텍스트(빌더·런타임 생성)가 한글을 바로 쓰게 하는 조치다.
        private static void ApplyAsTmpDefault(TMP_FontAsset fontAsset)
        {
            TMP_Settings settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings == null)
            {
                Debug.LogWarning("[FontSetup] TMP Settings를 찾지 못해 기본 폰트 지정을 건너뛴다");
                return;
            }

            SerializedObject so = new SerializedObject(settings);
            SerializedProperty prop = so.FindProperty("m_defaultFontAsset");
            if (prop == null)
            {
                Debug.LogWarning("[FontSetup] TMP Settings의 m_defaultFontAsset 프로퍼티를 찾지 못했다");
                return;
            }

            if (prop.objectReferenceValue == fontAsset) return;

            prop.objectReferenceValue = fontAsset;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            Debug.Log("[FontSetup] TMP 기본 폰트를 한글 폰트로 지정");
        }

        // ── 씬의 모든 TMP 텍스트 교체 ─────────────────────────────────────────
        private static int ReplaceSceneFonts(TMP_FontAsset fontAsset)
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MAIN_SCENE_PATH)
            {
                Debug.LogError($"[FontSetup] 활성 씬이 Main.unity가 아니다 (현재: {scene.path}) — 씬 교체를 건너뛴다");
                return 0;
            }

            List<TMP_Text> texts = new List<TMP_Text>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                // 비활성 패널(Title/Result 등)의 텍스트까지 포함해야 한다
                texts.AddRange(root.GetComponentsInChildren<TMP_Text>(true));
            }

            int replaced = 0;
            foreach (TMP_Text text in texts)
            {
                if (text.font == fontAsset) continue;

                text.font = fontAsset;
                // 머티리얼은 폰트 애셋의 서브 애셋이라 같이 갈아끼워야 한다
                // (비트맵 ↔ SDF는 셰이더가 달라서, 남아 있으면 글자가 아예 안 보인다)
                text.fontSharedMaterial = fontAsset.material;
                EditorUtility.SetDirty(text);
                replaced++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return replaced;
        }

        // ── SDF 애셋 용량 절감: Static → Dynamic (D6 WebGL 검증에서 판단) ──────
        // Font Asset Creator가 구운 Static SDF는 4096² Alpha8 = 텍스처 원본 16MB가 빌드에 통째로 실린다
        // (WebGL 예산 50MB). Dynamic으로 바꾸면 아틀라스가 비고 실제 쓰인 글자만 런타임에 구워지므로,
        // 빌드에는 원본 ttf(548KB)만 실린다. 화면 결과는 동일하다 — 같은 SDF를 언제 굽느냐의 차이뿐.
        // 대신 처음 등장하는 글자를 그 프레임에 굽는 비용이 생긴다.
        //
        // D3 판단: 실측 없이 바꾸지 않는다. D6 WebGL 빌드에서 실제 사이즈를 재고 결정한다.
        // ⚠️ 실행하면 구워 둔 아틀라스를 버린다. 되돌리려면 Font Asset Creator로 다시 구우면 된다.
        [MenuItem("Luddite/Setup/SDF 애셋 Dynamic 전환 (빌드 16MB 절감)")]
        public static void ConvertSdfToDynamic()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FontSetup] 플레이 모드에서는 실행하지 않는다");
                return;
            }

            TMP_FontAsset sdf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SDF_ASSET_PATH);
            Font source = AssetDatabase.LoadAssetAtPath<Font>(TTF_DST);
            if (sdf == null || source == null)
            {
                Debug.LogError($"[FontSetup] SDF 애셋 또는 원본 ttf를 찾지 못했다 ({SDF_ASSET_PATH} / {TTF_DST})");
                return;
            }

            SerializedObject so = new SerializedObject(sdf);

            // Static 애셋은 원본 폰트 참조가 비어 있다 — Dynamic은 런타임에 이 파일로 글리프를 굽는다
            SerializedProperty sourceProp = so.FindProperty("m_SourceFontFile");
            SerializedProperty modeProp = so.FindProperty("m_AtlasPopulationMode");
            SerializedProperty widthProp = so.FindProperty("m_AtlasWidth");
            SerializedProperty heightProp = so.FindProperty("m_AtlasHeight");
            SerializedProperty multiProp = so.FindProperty("m_IsMultiAtlasTexturesEnabled");
            if (sourceProp == null || modeProp == null)
            {
                Debug.LogError("[FontSetup] SDF 애셋의 프로퍼티 구조가 예상과 다르다 — 전환 중단");
                return;
            }

            sourceProp.objectReferenceValue = source;
            modeProp.intValue = (int)AtlasPopulationMode.Dynamic;
            if (widthProp != null) widthProp.intValue = DYNAMIC_ATLAS_SIZE;
            if (heightProp != null) heightProp.intValue = DYNAMIC_ATLAS_SIZE;
            if (multiProp != null) multiProp.boolValue = true;   // 아틀라스 1장이 차면 자동 증설
            so.ApplyModifiedPropertiesWithoutUndo();

            sdf.ClearFontAssetData(true);   // 구워 둔 4096² 아틀라스를 버린다

            EditorUtility.SetDirty(sdf);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FontSetup] SDF 애셋 Dynamic 전환 완료 — 아틀라스 {DYNAMIC_ATLAS_SIZE}², 원본 폰트 재연결. 빌드에서 텍스처 16MB가 빠진다");
        }
    }
}
