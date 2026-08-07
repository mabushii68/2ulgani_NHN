using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Luddite.Core;

namespace Luddite.EditorTools
{
    /// <summary>
    /// `Sprites.v2` 아트 후보 라이브러리 일괄 처리 (D3 세션 8).
    ///
    /// <para>
    /// Franuka 팩 3x 전량을 카테고리별로 모아 둔 <b>열람용</b> 라이브러리를 임포트·슬라이스하고,
    /// 캐릭터·몬스터는 씬에 바로 끌어다 놓고 볼 수 있게 프리팹까지 만든다.
    /// 목적은 "느낌을 보고 고르는 것"이므로, 여기 있는 것은 아직 게임에 쓰이지 않는다 —
    /// 고른 것을 <c>Sprites/</c>·<c>Prefabs/</c>로 승격시켜야 빌드에 들어간다.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>이 폴더는 `.gitignore`에 있다.</b> 팩 라이선스가 "as is 재배포 금지"인데 저장소가
    /// 공개되기 때문이다. 여기 파일을 커밋하려 들지 말 것 — 승격 경로로만 리포에 들어간다.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>라이브러리는 D4에 3x → 1x 원본으로 교체됐다</b> (G드라이브 팩의 1x 폴더 기준).
    /// 아래 상수들은 1x 기준이다 — 3x 시절 값(타일 48, PPU 54/96/48)으로 되돌리지 말 것.
    /// 화면상 크기는 PPU가 함께 1/3이 되어 기존 <c>Sprites/</c> 자산과 동일하게 유지된다.
    /// 기존 라이브러리의 일괄 전환은 V2LibraryRescaleTools가 담당했다 (재실행 안전).
    /// </para>
    /// </summary>
    public static class V2LibrarySetupTools
    {
        private const string ROOT = "Assets/_Project/Sprites.v2";
        private const string PREFAB_ROOT = "Assets/_Project/Prefabs.v2";

        /// <summary>환경 타일셋 격자의 기본 단위. 1x 기준 16px.</summary>
        private const int TILE = 16;

        private const int PPU_WORLD = 18;   // 캐릭터·몬스터·환경·아이템
        private const int PPU_ICON = 32;    // 아이콘
        private const int PPU_UI = 16;      // UI

        /// <summary>4방향 시트 판정에 쓰는 행 수. Franuka 캐릭터·몬스터 시트의 공통 규약.</summary>
        private const int DIR_ROWS = 4;

        // ── 배치 진행 상태 ────────────────────────────────────────────
        // 5,000장을 한 호출 안에서 처리하면 에디터가 그 시간 내내 멈추고, MCP는 응답을 못 받아
        // 타임아웃한다(실제로 그렇게 한 번 날렸다). EditorApplication.update로 나눠 돌리면
        // 메뉴가 즉시 반환하므로 밖에서는 로그로 진행을 확인할 수 있다.
        private const int BATCH = 40;

        private static string[] _queue;
        private static int _cursor;
        private static int _sliced, _single, _failed;
        private static List<string> _report;
        private static UnityEditor.U2D.Sprites.SpriteDataProviderFactories _factories;

        [MenuItem("Luddite/Setup/v2 라이브러리 — 임포트 + 슬라이스")]
        public static void ImportAndSlice()
        {
            if (_queue != null)
            {
                Debug.LogWarning($"[v2] 이미 진행 중 — {_cursor}/{_queue.Length}");
                return;
            }
            if (!Directory.Exists(ROOT))
            {
                Debug.LogError($"[v2] {ROOT} 없음 — 복사 스크립트를 먼저 돌릴 것");
                return;
            }

            _queue = Directory.GetFiles(ROOT, "*.png", SearchOption.AllDirectories);
            if (_queue.Length == 0)
            {
                Debug.LogError($"[v2] {ROOT} 에 png가 없다");
                _queue = null;
                return;
            }

            _cursor = 0;
            _sliced = _single = _failed = 0;
            _report = new List<string>();
            _factories = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
            _factories.Init();

            EditorApplication.update += ProcessBatch;
            Debug.Log($"[v2] 임포트 시작 — {_queue.Length}장, {BATCH}장씩 처리");
        }

        private static void ProcessBatch()
        {
            int end = Mathf.Min(_cursor + BATCH, _queue.Length);

            AssetDatabase.StartAssetEditing();
            try
            {
                for (; _cursor < end; _cursor++)
                {
                    string path = _queue[_cursor].Replace('\\', '/');

                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) { _failed++; continue; }

                    string category = CategoryOf(path);
                    ApplyBaseSettings(importer, PpuOf(category));

                    Vector2Int grid = DetectGrid(path, category, out Vector2Int size);
                    if (grid.x <= 1 && grid.y <= 1)
                    {
                        // Single로 되돌리는 것이 중요하다 — 최초 임포트가 Multiple 자동 슬라이스로
                        // 들어와 불규칙한 조각이 잔뜩 생긴 상태라, 명시하지 않으면 그게 남는다
                        importer.spriteImportMode = SpriteImportMode.Single;
                        _single++;
                    }
                    else
                    {
                        importer.spriteImportMode = SpriteImportMode.Multiple;
                        if (WriteGrid(_factories, importer, path, size, grid.x, grid.y)) _sliced++;
                        else { _failed++; _report.Add($"슬라이스 실패: {path}"); }
                    }

                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                }
            }
            catch (System.Exception e)
            {
                _report.Add($"예외 @{_cursor}: {e.Message}");
                _failed++;
                _cursor++;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (_cursor % 400 < BATCH)
                Debug.Log($"[v2] 진행 {_cursor}/{_queue.Length}");

            if (_cursor < _queue.Length) return;

            EditorApplication.update -= ProcessBatch;
            AssetDatabase.Refresh();
            Debug.Log($"[v2] 임포트 완료 — 총 {_queue.Length} / 슬라이스 {_sliced} / 단일 {_single} / 실패 {_failed}");
            foreach (string line in _report) Debug.LogWarning("[v2] " + line);
            _queue = null;
            _report = null;
            _factories = null;
        }

        // ────────────────────────────────── 임포트 설정

        private static string CategoryOf(string path)
        {
            string rest = path.Substring(ROOT.Length + 1);
            int slash = rest.IndexOf('/');
            return slash < 0 ? rest : rest.Substring(0, slash);
        }

        private static int PpuOf(string category)
        {
            if (category == "Icons") return PPU_ICON;
            if (category == "UI") return PPU_UI;
            return PPU_WORLD;
        }

        private static void ApplyBaseSettings(TextureImporter importer, int ppu)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
        }

        // ────────────────────────────────── 격자 판정

        /// <summary>
        /// 격자(열, 행)를 정한다. 1×1이면 자르지 않는다.
        ///
        /// <para>
        /// 환경 타일셋은 팩 문서가 "타일은 16×16(3x는 48×48)"이라고 못박아 두어 격자가 자명하다.
        /// 문제는 캐릭터·몬스터 시트인데, <b>프레임 크기가 파일마다 다르고</b> 세로만 4행(4방향)으로
        /// 고정돼 있다. 세로에서 셀 높이를 얻은 뒤 가로 셀 폭을 후보 약수 중에서 고르는데,
        /// 정사각형이 정답인 경우가 많지만 아닌 것도 있어(예: 1x 기준 Djinn_attack 64×32,
        /// Wizard_attack 48×40) 정사각형 가정만으로는 틀린다. 그래서
        /// <b>셀 경계에 투명한 세로 이음매가 있는지</b>로 후보를 채점한다 — 스프라이트 사이에는
        /// 대개 빈 열이 있으므로 이 신호가 실제 프레임 폭을 가리킨다.
        /// </para>
        /// </summary>
        private static Vector2Int DetectGrid(string path, string category, out Vector2Int size)
        {
            size = Vector2Int.zero;
            if (category == "Icons" || category == "UI") return Vector2Int.one;

            Texture2D texture = LoadReadable(path);
            if (texture == null) return Vector2Int.one;

            try
            {
                int w = texture.width, h = texture.height;
                size = new Vector2Int(w, h);

                if (category == "Environment")
                {
                    if (w % TILE == 0 && h % TILE == 0 && (w > TILE || h > TILE))
                        return new Vector2Int(w / TILE, h / TILE);
                    return Vector2Int.one;
                }

                // 캐릭터·몬스터·아이템: 4방향 시트 우선, 안 되면 가로 한 줄 애니메이션으로 본다
                int rows = h % DIR_ROWS == 0 ? DIR_ROWS : 1;
                int cellH = h / rows;
                if (cellH <= 0 || w % cellH != 0 && !HasDivisorNear(w, cellH)) return Vector2Int.one;

                int cellW = PickCellWidth(texture, w, h, rows, cellH);
                if (cellW <= 0) return Vector2Int.one;

                int cols = w / cellW;
                if (cols < 1 || cols > 24) return Vector2Int.one;
                if (cols == 1 && rows == 1) return Vector2Int.one;

                return new Vector2Int(cols, rows);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static bool HasDivisorNear(int w, int cellH)
        {
            for (int c = Mathf.Max(1, cellH / 2); c <= cellH * 2; c++)
                if (w % c == 0) return true;
            return false;
        }

        /// <summary>
        /// 후보 셀 폭들을 이음매 점수로 채점한다. 동점이면 정사각형(셀폭 = 셀높이)을 택한다 —
        /// 팩에서 가장 흔한 형태라 근거 없는 추측보다 낫다.
        /// </summary>
        private static int PickCellWidth(Texture2D texture, int w, int h, int rows, int cellH)
        {
            int lo = Mathf.Max(1, Mathf.RoundToInt(cellH * 0.5f));
            int hi = Mathf.RoundToInt(cellH * 2.5f);

            bool[] emptyColumn = ComputeEmptyColumns(texture, w, h);

            int best = -1;
            float bestScore = -1f;
            for (int cellW = lo; cellW <= hi; cellW++)
            {
                if (w % cellW != 0) continue;
                int cols = w / cellW;
                if (cols > 24) continue;

                float score;
                if (cols == 1)
                {
                    score = 0.5f;   // 경계가 없으니 중립. 다른 후보가 없을 때만 채택된다
                }
                else
                {
                    int hits = 0, checks = 0;
                    for (int k = 1; k < cols; k++)
                    {
                        int x = k * cellW;
                        checks += 2;
                        if (emptyColumn[x]) hits++;
                        if (emptyColumn[x - 1]) hits++;
                    }
                    score = (float)hits / checks;
                }

                bool square = cellW == cellH;
                float adjusted = score + (square ? 0.05f : 0f);   // 동점 깨기용 미세 가중

                if (adjusted > bestScore)
                {
                    bestScore = adjusted;
                    best = cellW;
                }
            }
            return best;
        }

        /// <summary>
        /// 열별 "완전 투명" 여부. 1x는 업스케일 블록이 없으므로 모든 행을 본다
        /// (3x 시절에는 3픽셀 간격이었다 — 1x는 픽셀 수가 1/9이라 전수여도 더 싸다).
        /// </summary>
        private static bool[] ComputeEmptyColumns(Texture2D texture, int w, int h)
        {
            Color32[] pixels = texture.GetPixels32();
            var empty = new bool[w];
            for (int x = 0; x < w; x++)
            {
                bool isEmpty = true;
                for (int y = 0; y < h; y++)
                {
                    if (pixels[y * w + x].a != 0) { isEmpty = false; break; }
                }
                empty[x] = isEmpty;
            }
            return empty;
        }

        /// <summary>
        /// 임포트 설정과 무관하게 픽셀을 읽는다. 애셋의 Texture2D는 Read/Write가 꺼져 있어
        /// <c>GetPixels32</c>가 실패하므로, 원본 png 바이트를 임시 텍스처에 직접 올린다.
        /// </summary>
        private static Texture2D LoadReadable(string path)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, bytes, false))
                {
                    Object.DestroyImmediate(texture);
                    return null;
                }
                return texture;
            }
            catch
            {
                return null;
            }
        }

        // ────────────────────────────────── 슬라이스 기록

        private static bool WriteGrid(
            UnityEditor.U2D.Sprites.SpriteDataProviderFactories factories,
            TextureImporter importer, string path, Vector2Int size, int cols, int rows)
        {
            int w = size.x, h = size.y;
            if (w <= 0 || h <= 0 || w % cols != 0 || h % rows != 0) return false;

            int cellW = w / cols, cellH = h / rows;
            string baseName = Path.GetFileNameWithoutExtension(path);

            var rects = new List<SpriteRect>(cols * rows);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var rect = new SpriteRect();
                    rect.name = baseName + "_" + (r * cols + c);
                    rect.spriteID = GUID.Generate();
                    rect.rect = new Rect(c * cellW, h - (r + 1) * cellH, cellW, cellH);
                    rect.alignment = SpriteAlignment.Center;
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rects.Add(rect);
                }
            }

            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            provider.SetSpriteRects(rects.ToArray());

            var nameProvider = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
            if (nameProvider != null)
            {
                var pairs = new List<SpriteNameFileIdPair>(rects.Count);
                for (int i = 0; i < rects.Count; i++) pairs.Add(new SpriteNameFileIdPair(rects[i].name, rects[i].spriteID));
                nameProvider.SetNameFileIdPairs(pairs);
            }
            provider.Apply();
            return true;
        }

        // ────────────────────────────────── 프리팹

        /// <summary>
        /// 캐릭터·몬스터의 idle 시트마다 프리팹 1개. 씬에 끌어다 놓으면 4방향 애니메이션이 도는
        /// 상태로 보이는 것이 목적이다 — "느낌 보고 고르기"에 필요한 건 그것뿐이다.
        /// 콜라이더·FSM은 붙이지 않는다. 승격할 때 실제 프리팹 빌더가 담당할 몫이라
        /// 여기서 미리 만들면 두 벌이 어긋난다.
        /// </summary>
        [MenuItem("Luddite/Setup/v2 라이브러리 — 프리팹 생성")]
        public static void BuildPrefabs()
        {
            string[] categories = { "Characters", "Monsters" };
            int made = 0, skipped = 0;

            // StartAssetEditing으로 감싸지 않는다 — 그 블록 안에서 SaveAsPrefabAsset을 부르면
            // 참조가 아직 임포트되지 않은 상태로 저장돼 빈 프리팹이 나오는 경우가 있다.
            try
            {
                foreach (string category in categories)
                {
                    string dir = ROOT + "/" + category;
                    if (!Directory.Exists(dir)) continue;

                    string[] files = Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories);
                    for (int i = 0; i < files.Length; i++)
                    {
                        string path = files[i].Replace('\\', '/');
                        string name = Path.GetFileNameWithoutExtension(path);

                        // idle 시트만 프리팹의 기준으로 삼는다 — 한 캐릭터당 하나가 되도록
                        if (!name.EndsWith("_idle", System.StringComparison.OrdinalIgnoreCase)) continue;

                        if (i % 10 == 0)
                            EditorUtility.DisplayProgressBar("v2 프리팹", $"{category}  {name}", (float)i / files.Length);

                        if (BuildOne(path, name, category)) made++; else skipped++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[v2] 프리팹 생성 — 성공 {made} / 건너뜀 {skipped}");
        }

        private static bool BuildOne(string idlePath, string idleName, string category)
        {
            List<Sprite> idleFrames = LoadOrdered(idlePath);
            if (idleFrames == null || idleFrames.Count % DIR_ROWS != 0) return false;

            string character = idleName.Substring(0, idleName.Length - "_idle".Length);
            string folder = Path.GetDirectoryName(idlePath).Replace('\\', '/');

            // 이동 시트 이름이 팩마다 다르다 (walk / move / run)
            List<Sprite> moveFrames = null;
            foreach (string suffix in new[] { "_walk", "_move", "_run" })
            {
                string candidate = folder + "/" + character + suffix + ".png";
                if (!File.Exists(candidate)) continue;
                moveFrames = LoadOrdered(candidate);
                if (moveFrames != null && moveFrames.Count % DIR_ROWS == 0) break;
                moveFrames = null;
            }

            var root = new GameObject(character);
            try
            {
                var body = new GameObject("Body");
                body.transform.SetParent(root.transform, false);
                var renderer = body.AddComponent<SpriteRenderer>();
                renderer.sprite = idleFrames[0];

                var animator = root.AddComponent<DirectionalSpriteAnimator>();
                var so = new SerializedObject(animator);
                so.FindProperty("_renderer").objectReferenceValue = renderer;
                so.FindProperty("_defaultClip").stringValue = DirectionalSpriteAnimator.CLIP_IDLE;
                so.FindProperty("_autoDriveFromBody").boolValue = false;   // Rigidbody2D가 없다
                so.FindProperty("_facingSource").objectReferenceValue = null;

                var clips = new List<KeyValuePair<string, List<Sprite>>>
                {
                    new KeyValuePair<string, List<Sprite>>(DirectionalSpriteAnimator.CLIP_IDLE, idleFrames)
                };
                if (moveFrames != null)
                    clips.Add(new KeyValuePair<string, List<Sprite>>(DirectionalSpriteAnimator.CLIP_WALK, moveFrames));

                SerializedProperty clipsProp = so.FindProperty("_clips");
                clipsProp.arraySize = clips.Count;
                for (int i = 0; i < clips.Count; i++)
                {
                    List<Sprite> frames = clips[i].Value;
                    int cols = frames.Count / DIR_ROWS;

                    SerializedProperty clipProp = clipsProp.GetArrayElementAtIndex(i);
                    clipProp.FindPropertyRelative("_name").stringValue = clips[i].Key;
                    clipProp.FindPropertyRelative("_fps").floatValue = i == 0 ? 6f : 10f;
                    clipProp.FindPropertyRelative("_loop").boolValue = true;

                    SerializedProperty rowsProp = clipProp.FindPropertyRelative("_rows");
                    rowsProp.arraySize = DIR_ROWS;
                    for (int r = 0; r < DIR_ROWS; r++)
                    {
                        SerializedProperty framesProp = rowsProp.GetArrayElementAtIndex(r).FindPropertyRelative("_frames");
                        framesProp.arraySize = cols;
                        for (int c = 0; c < cols; c++)
                            framesProp.GetArrayElementAtIndex(c).objectReferenceValue = frames[r * cols + c];
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                // Sprites.v2 아래 경로를 Prefabs.v2에 그대로 옮겨 담아 원본을 되짚기 쉽게 한다
                string relative = folder.Substring(ROOT.Length + 1);
                string outDir = PREFAB_ROOT + "/" + relative;
                Directory.CreateDirectory(outDir);
                PrefabUtility.SaveAsPrefabAsset(root, outDir + "/" + character + ".prefab");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>슬라이스된 서브 애셋을 <c>{파일명}_{인덱스}</c> 순서로. 이름 순서가 곧 방향·프레임 순서다.</summary>
        private static List<Sprite> LoadOrdered(string path)
        {
            string baseName = Path.GetFileNameWithoutExtension(path);
            var byName = new Dictionary<string, Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite) byName[sprite.name] = sprite;
            }
            if (byName.Count < DIR_ROWS) return null;

            var ordered = new List<Sprite>(byName.Count);
            for (int i = 0; i < byName.Count; i++)
            {
                if (!byName.TryGetValue(baseName + "_" + i, out Sprite sprite)) return null;
                ordered.Add(sprite);
            }
            return ordered;
        }
    }
}
