using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Luddite.EditorTools
{
    /// <summary>
    /// 과목 탄막 스프라이트 절차 생성기 (D7).
    /// Franuka 아이콘 팩에 없는 5개 테마(펜/컴퓨터/전기/공/음표)를 16×16 픽셀 아트로
    /// 코드 생성한다 — 팀 저작물 (외부 소스·AI 이미지 모델 미사용, Placeholder·SFX 절차 생성 선례).
    /// 고정 마스크 + 고정 팔레트라 재실행 시 동일 파일이 나온다 (결정론).
    /// 규격은 Sprites/Icons/ 의 Franuka 아이콘과 동일: 16×16 / PPU 32 / Point / 무압축.
    /// </summary>
    public static class SubjectBulletSpriteTools
    {
        private const string OUTPUT_DIR = "Assets/_Project/Sprites/Icons/Procedural";
        private const int SIZE = 16;
        private const float PIXELS_PER_UNIT = 32f; // Icons 카테고리와 동일 (Icon_092_Coin.meta 실측)

        private static readonly Color32 OUTLINE = new Color32(0x40, 0x26, 0x2B, 0xFF); // Franuka풍 암갈색 윤곽

        // ── 마스크 문법: '.' = 투명, 그 외 문자는 팔레트 키. 윤곽선은 자동 생성(8방향) ──

        private static readonly string[] MASK_PENCIL =
        {
            "................",
            "................",
            "............ee..",
            "...........eee..",
            "..........mee...",
            ".........bmm....",
            "........bbm.....",
            ".......bBb......",
            "......bBb.......",
            ".....bBb........",
            "....wBb.........",
            "...wwb..........",
            "..gww...........",
            "..gw............",
            "..g.............",
            "................",
        };

        private static readonly string[] MASK_MONITOR =
        {
            "................",
            "................",
            "..ffffffffffff..",
            "..fssssssssssf..",
            "..fsttttttsssf..",
            "..fssssssssssf..",
            "..fstttssssssf..",
            "..fssssssssssf..",
            "..ffffffffffff..",
            "................",
            ".......FF.......",
            ".......FF.......",
            ".....FFFFFF.....",
            "................",
            "................",
            "................",
        };

        private static readonly string[] MASK_BOLT =
        {
            "................",
            "................",
            ".......yy.......",
            "......yyy.......",
            ".....yyy........",
            "....yyy.........",
            "...yyyyyy.......",
            "....YYyyy.......",
            ".....Yyy........",
            ".....yy.........",
            "....yy..........",
            "....y...........",
            "...y............",
            "...y............",
            "................",
            "................",
        };

        private static readonly string[] MASK_BALL =
        {
            "................",
            ".....bbblbb.....",
            "...bbbbblbbbb...",
            "..bbbbbblbbbhb..",
            "..bbbbbblbbbhb..",
            ".bbbbbbblbbbbhb.",
            ".bbbbbbblbbbbbb.",
            ".bbbbbbblbbbbbb.",
            ".llllllllllllll.",
            ".Bbbbbbblbbbbbb.",
            ".Bbbbbbblbbbbbb.",
            "..Bbbbbblbbbbb..",
            "..BBbbbblbbbbb..",
            "...BBbbblbbbb...",
            ".....BBblbb.....",
            "................",
        };

        private static readonly string[] MASK_NOTE =
        {
            "................",
            "........nn......",
            "........nnn.....",
            "........n.nn....",
            "........n..nn...",
            "........n...n...",
            "........n..nn...",
            "........n.......",
            "........n.......",
            "........n.......",
            ".....nnnn.......",
            "...nnnnnn.......",
            "..nnnnnnn.......",
            "..nNNnnn........",
            "...NNnn.........",
            "................",
        };

        private static readonly (string name, string[] mask, Dictionary<char, Color32> palette)[] ICONS =
        {
            ("Proc_Pencil", MASK_PENCIL, new Dictionary<char, Color32>
            {
                ['e'] = new Color32(0xC9, 0x6A, 0x5F, 0xFF), // 지우개 (탁한 적색 — 무채색~주황 범위)
                ['m'] = new Color32(0x9C, 0x95, 0x8D, 0xFF), // 금속 링
                ['b'] = new Color32(0xE0, 0x91, 0x3F, 0xFF), // 몸통 주황
                ['B'] = new Color32(0xB0, 0x6B, 0x2E, 0xFF), // 몸통 음영
                ['w'] = new Color32(0xE6, 0xC8, 0x96, 0xFF), // 깎인 나무
                ['g'] = new Color32(0x45, 0x37, 0x33, 0xFF), // 흑연 심
            }),
            ("Proc_Monitor", MASK_MONITOR, new Dictionary<char, Color32>
            {
                ['f'] = new Color32(0x8F, 0x8B, 0x84, 0xFF), // 프레임 회색
                ['F'] = new Color32(0x6B, 0x66, 0x60, 0xFF), // 스탠드
                ['s'] = new Color32(0x33, 0x31, 0x3A, 0xFF), // 화면 암색
                ['t'] = new Color32(0xE8, 0xA3, 0x3D, 0xFF), // 앰버 터미널 텍스트
            }),
            ("Proc_Bolt", MASK_BOLT, new Dictionary<char, Color32>
            {
                ['y'] = new Color32(0xEB, 0x9B, 0x2D, 0xFF), // 번개 주황 (전공색 노랑 회피)
                ['Y'] = new Color32(0xC9, 0x74, 0x1F, 0xFF), // 번개 음영
            }),
            ("Proc_Ball", MASK_BALL, new Dictionary<char, Color32>
            {
                ['b'] = new Color32(0xDD, 0x7A, 0x35, 0xFF), // 농구공 주황
                ['B'] = new Color32(0xB2, 0x5A, 0x24, 0xFF), // 음영
                ['l'] = new Color32(0x4A, 0x2D, 0x2B, 0xFF), // 솔기
                ['h'] = new Color32(0xF0, 0xA0, 0x5C, 0xFF), // 하이라이트
            }),
            ("Proc_Note", MASK_NOTE, new Dictionary<char, Color32>
            {
                ['n'] = new Color32(0xEC, 0xE7, 0xD9, 0xFF), // 음표 크림색 (어두운 던전 바닥 대비)
                ['N'] = new Color32(0xCF, 0xC8, 0xB8, 0xFF), // 머리 음영
            }),
        };

        [MenuItem("Luddite/Setup/과목 탄막 스프라이트 생성 (5종)")]
        public static void Generate()
        {
            if (!Directory.Exists(OUTPUT_DIR)) Directory.CreateDirectory(OUTPUT_DIR);

            int written = 0;
            foreach (var (name, mask, palette) in ICONS)
            {
                Texture2D texture = BuildTexture(name, mask, palette);
                string path = $"{OUTPUT_DIR}/{name}.png";
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                ConfigureImporter(path);
                written++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[SubjectBulletSpriteTools] 과목 탄막 스프라이트 {written}종 생성 완료 → {OUTPUT_DIR}");
        }

        private static Texture2D BuildTexture(string name, string[] mask, Dictionary<char, Color32> palette)
        {
            if (mask.Length != SIZE)
                throw new System.InvalidOperationException($"{name}: 마스크 행 수 {mask.Length} ≠ {SIZE}");

            var pixels = new Color32[SIZE * SIZE];
            var filled = new bool[SIZE, SIZE];

            for (int row = 0; row < SIZE; row++)
            {
                if (mask[row].Length != SIZE)
                    throw new System.InvalidOperationException($"{name}: {row}행 길이 {mask[row].Length} ≠ {SIZE}");
                for (int col = 0; col < SIZE; col++)
                {
                    char key = mask[row][col];
                    if (key == '.') continue;
                    if (!palette.TryGetValue(key, out Color32 color))
                        throw new System.InvalidOperationException($"{name}: 팔레트에 없는 문자 '{key}' ({row},{col})");
                    // 마스크는 위에서 아래로 쓰였고 텍스처 y는 아래가 0이므로 뒤집는다
                    pixels[(SIZE - 1 - row) * SIZE + col] = color;
                    filled[row, col] = true;
                }
            }

            // 자동 윤곽선: 채움 픽셀과 8방향으로 접한 투명 픽셀을 암갈색으로
            for (int row = 0; row < SIZE; row++)
            for (int col = 0; col < SIZE; col++)
            {
                if (filled[row, col]) continue;
                bool touches = false;
                for (int dr = -1; dr <= 1 && !touches; dr++)
                for (int dc = -1; dc <= 1 && !touches; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    int nr = row + dr, nc = col + dc;
                    if (nr >= 0 && nr < SIZE && nc >= 0 && nc < SIZE && filled[nr, nc]) touches = true;
                }
                if (touches) pixels[(SIZE - 1 - row) * SIZE + col] = OUTLINE;
            }

            var texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static void ConfigureImporter(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PIXELS_PER_UNIT;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }
}
