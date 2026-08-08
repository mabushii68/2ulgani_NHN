using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Luddite.EditorTools
{
    /// <summary>
    /// `Sprites.v2` 라이브러리를 3x → 1x 원본으로 교체한 뒤의 임포트 설정 복구 (D4).
    ///
    /// <para>
    /// png 내용만 1x로 바뀌고 `.meta`는 3x 시절 그대로라, Multiple 모드 3,224장의
    /// 슬라이스 rect가 텍스처 밖을 가리키고 PPU도 3배 값(54/96/48)으로 남아 있다.
    /// 이 도구는 <b>기존 rect를 그대로 ÷3</b> 하고 PPU를 1x 기준값(18/32/16)으로 내린다.
    /// 재감지가 아니라 축소이므로 spriteID·이름이 보존된다 — `Prefabs.v2` 프리팹들의
    /// 스프라이트 참조가 끊기지 않는 것이 핵심.
    /// </para>
    ///
    /// <para>
    /// 멱등: PPU가 이미 1x 값(18/32/16)인 파일은 건너뛴다. 3x 값(54/96/48)일 때만 축소하므로
    /// 두 번 돌려도 rect가 또 줄어들지 않는다.
    /// </para>
    ///
    /// TODO(임시): 라이브러리가 1x로 안정화되면 이 파일은 삭제해도 된다.
    /// </summary>
    public static class V2LibraryRescaleTools
    {
        private const string ROOT = "Assets/_Project/Sprites.v2";
        private const int SCALE = 3;

        // 1x 기준 PPU (V2LibrarySetupTools의 "1x 기준 값의 3배" 주석 참조)
        private const int PPU_WORLD_1X = 18;
        private const int PPU_ICON_1X = 32;
        private const int PPU_UI_1X = 16;

        private const int BATCH = 40;

        private static string[] _queue;
        private static int _cursor;
        private static int _rescaled, _skipped, _failed;
        private static List<string> _report;
        private static UnityEditor.U2D.Sprites.SpriteDataProviderFactories _factories;

        // ────────────────────────────────── 검사

        [MenuItem("Luddite/Setup/v2 라이브러리 — 1x 슬라이스 검사")]
        public static void Inspect()
        {
            string[] files = Directory.GetFiles(ROOT, "*.png", SearchOption.AllDirectories);
            int multiple = 0, outOfBounds = 0, ppu3x = 0, checkedCount = 0;
            string sample = null;

            foreach (string raw in files)
            {
                string path = raw.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                checkedCount++;

                if (Mathf.Approximately(importer.spritePixelsPerUnit, 54f)
                    || Mathf.Approximately(importer.spritePixelsPerUnit, 96f)
                    || Mathf.Approximately(importer.spritePixelsPerUnit, 48f))
                    ppu3x++;

                if (importer.spriteImportMode != SpriteImportMode.Multiple) continue;
                multiple++;

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;

                bool broken = false;
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var s = o as Sprite;
                    if (s == null) continue;
                    if (s.rect.xMax > tex.width + 0.01f || s.rect.yMax > tex.height + 0.01f)
                    {
                        broken = true;
                        if (sample == null)
                            sample = path + " → " + s.name + " rect=" + s.rect + " (tex " + tex.width + "x" + tex.height + ")";
                        break;
                    }
                }
                if (broken) outOfBounds++;
            }

            Debug.Log("[v2 검사] 전체 " + checkedCount + " / Multiple " + multiple
                + " / rect 범위 초과 " + outOfBounds + " / PPU 3x값(54·96·48) " + ppu3x);
            if (sample != null) Debug.Log("[v2 검사] 예시: " + sample);
        }

        // ────────────────────────────────── 재조정

        [MenuItem("Luddite/Setup/v2 라이브러리 — 1x 재조정 (PPU + 슬라이스 ÷3)")]
        public static void Rescale()
        {
            if (_queue != null)
            {
                Debug.LogWarning("[v2 재조정] 이미 진행 중 — " + _cursor + "/" + _queue.Length);
                return;
            }
            if (!Directory.Exists(ROOT))
            {
                Debug.LogError("[v2 재조정] " + ROOT + " 없음");
                return;
            }

            _queue = Directory.GetFiles(ROOT, "*.png", SearchOption.AllDirectories);
            _cursor = 0;
            _rescaled = _skipped = _failed = 0;
            _report = new List<string>();
            _factories = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
            _factories.Init();

            EditorApplication.update += ProcessBatch;
            Debug.Log("[v2 재조정] 시작 — " + _queue.Length + "장, " + BATCH + "장씩");
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

                    float ppu = importer.spritePixelsPerUnit;
                    int targetPpu = TargetPpuOf(ppu);
                    if (targetPpu < 0)
                    {
                        _skipped++;   // 이미 1x 값 — 멱등 보장
                        continue;
                    }

                    importer.spritePixelsPerUnit = targetPpu;

                    if (importer.spriteImportMode == SpriteImportMode.Multiple)
                    {
                        if (!ShrinkRects(importer, path))
                        {
                            _failed++;
                            _report.Add("rect ÷3 불가(3의 배수 아님): " + path);
                            // PPU만이라도 반영해 두면 재실행 시 또 건드리므로, 실패 파일은 원복
                            importer.spritePixelsPerUnit = ppu;
                            continue;
                        }
                    }

                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                    _rescaled++;
                }
            }
            catch (System.Exception e)
            {
                _report.Add("예외 @" + _cursor + ": " + e.Message);
                _failed++;
                _cursor++;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (_cursor % 400 < BATCH)
                Debug.Log("[v2 재조정] 진행 " + _cursor + "/" + _queue.Length);

            if (_cursor < _queue.Length) return;

            EditorApplication.update -= ProcessBatch;
            AssetDatabase.Refresh();
            Debug.Log("[v2 재조정] 완료 — 총 " + _queue.Length + " / 재조정 " + _rescaled
                + " / 건너뜀 " + _skipped + " / 실패 " + _failed);
            foreach (string line in _report) Debug.LogWarning("[v2 재조정] " + line);
            _queue = null;
            _report = null;
            _factories = null;
        }

        /// <summary>3x PPU → 1x PPU. 이미 1x 값이면 -1 (건너뜀).</summary>
        private static int TargetPpuOf(float ppu)
        {
            if (Mathf.Approximately(ppu, 54f)) return PPU_WORLD_1X;
            if (Mathf.Approximately(ppu, 96f)) return PPU_ICON_1X;
            if (Mathf.Approximately(ppu, 48f)) return PPU_UI_1X;
            return -1;
        }

        /// <summary>
        /// 기존 SpriteRect들을 그대로 ÷3. spriteID·이름·피벗을 유지하고 rect(와 border)만 줄인다.
        /// 좌표 하나라도 3의 배수가 아니면 파일 전체를 실패 처리한다 (반쪽 축소 방지).
        /// </summary>
        private static bool ShrinkRects(TextureImporter importer, string path)
        {
            var provider = _factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            SpriteRect[] rects = provider.GetSpriteRects();
            if (rects == null || rects.Length == 0) return true;   // 자를 것이 없으면 그대로

            foreach (SpriteRect r in rects)
            {
                Rect rect = r.rect;
                if (!DivisibleBy3(rect.x) || !DivisibleBy3(rect.y)
                    || !DivisibleBy3(rect.width) || !DivisibleBy3(rect.height))
                    return false;
            }

            foreach (SpriteRect r in rects)
            {
                Rect rect = r.rect;
                r.rect = new Rect(rect.x / SCALE, rect.y / SCALE, rect.width / SCALE, rect.height / SCALE);
                r.border = r.border / SCALE;
            }

            provider.SetSpriteRects(rects);
            provider.Apply();
            return true;
        }

        private static bool DivisibleBy3(float v)
        {
            int i = Mathf.RoundToInt(v);
            return Mathf.Approximately(v, i) && i % SCALE == 0;
        }

        // ────────────────────────────────── 실패분 재슬라이스

        /// <summary>1x 기준 타일 크기. V2LibrarySetupTools.TILE(48)의 1x 값.</summary>
        private const int TILE_1X = 16;
        private const int DIR_ROWS = 4;

        /// <summary>
        /// rect ÷3이 불가능했던 파일(3x 슬라이스 폭이 3의 배수가 아니었던 33장)을
        /// 1x 이미지 기준으로 격자를 새로 감지해 재슬라이스한다. 감지 로직은
        /// V2LibrarySetupTools.DetectGrid와 같되 1x 수치(타일 16, 이음매 스캔 간격 1)를 쓴다.
        ///
        /// <para>
        /// ⚠️ spriteID가 새로 생성되므로 프리팹이 참조하는 시트에는 쓰면 안 된다.
        /// 대상 선별이 "PPU가 아직 3x 값인 파일"이라 ÷3에 성공한 시트(프리팹 참조 대상 전부)는
        /// 애초에 걸리지 않는다 — _idle/_walk 시트는 모두 ÷3로 처리됐음을 확인하고 쓸 것.
        /// </para>
        /// </summary>
        [MenuItem("Luddite/Setup/v2 라이브러리 — 실패분 재슬라이스 (1x 감지)")]
        public static void ResliceFailed()
        {
            var factories = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
            factories.Init();

            string[] files = Directory.GetFiles(ROOT, "*.png", SearchOption.AllDirectories);
            int sliced = 0, single = 0, failed = 0;

            foreach (string raw in files)
            {
                string path = raw.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                int targetPpu = TargetPpuOf(importer.spritePixelsPerUnit);
                if (targetPpu < 0) continue;   // 이미 1x — ÷3 성공분은 건드리지 않는다

                importer.spritePixelsPerUnit = targetPpu;

                string category = CategoryOf(path);
                Vector2Int grid = DetectGrid1x(path, category, out Vector2Int size);
                if (grid.x <= 1 && grid.y <= 1)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    single++;
                }
                else
                {
                    importer.spriteImportMode = SpriteImportMode.Multiple;
                    if (WriteGrid(factories, importer, path, size, grid.x, grid.y)) sliced++;
                    else { failed++; Debug.LogWarning("[v2 재슬라이스] 실패: " + path); continue; }
                }

                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }

            Debug.Log("[v2 재슬라이스] 완료 — 슬라이스 " + sliced + " / 단일 " + single + " / 실패 " + failed);
        }

        private static string CategoryOf(string path)
        {
            string rest = path.Substring(ROOT.Length + 1);
            int slash = rest.IndexOf('/');
            return slash < 0 ? rest : rest.Substring(0, slash);
        }

        private static Vector2Int DetectGrid1x(string path, string category, out Vector2Int size)
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
                    if (w % TILE_1X == 0 && h % TILE_1X == 0 && (w > TILE_1X || h > TILE_1X))
                        return new Vector2Int(w / TILE_1X, h / TILE_1X);
                    return Vector2Int.one;
                }

                int rows = h % DIR_ROWS == 0 ? DIR_ROWS : 1;
                int cellH = h / rows;
                if (cellH <= 0) return Vector2Int.one;

                int cellW = PickCellWidth(texture, w, h, cellH);
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

        private static int PickCellWidth(Texture2D texture, int w, int h, int cellH)
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
                    score = 0.5f;
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

                float adjusted = score + (cellW == cellH ? 0.05f : 0f);
                if (adjusted > bestScore)
                {
                    bestScore = adjusted;
                    best = cellW;
                }
            }
            return best;
        }

        /// <summary>1x는 업스케일 블록이 없으므로 모든 행을 본다 (원본 도구는 3픽셀 간격이었다).</summary>
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
    }
}
