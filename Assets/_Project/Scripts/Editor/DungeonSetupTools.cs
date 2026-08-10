using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Luddite.Core;
using Luddite.Data;

namespace Luddite.EditorTools
{
    /// <summary>
    /// 던전 체인을 코드로 결정론 생성하는 빌더 (개정안 §2 / MAP_SPEC).
    ///
    /// <para><b>멱등:</b> `Dungeon` 루트를 통째로 지우고 다시 짓는다. 이 루트 아래는 전부 생성물이므로
    /// 손으로 편집하지 말 것. <b>기존 `Arena`(폴백 아레나)는 건드리지 않는다.</b></para>
    ///
    /// <para><b>🔴 계약 준수:</b> 전투방 내부는 전원 동일 규격이고 <b>충돌 지형은 벽뿐이다.</b>
    /// 기둥·장식은 전부 <b>비충돌</b>이다 — 적 FSM에 장애물 회피가 없어 코딩봇 돌진(10u/s 직진)이
    /// 박히고, 탄이 막히면 위기 이벤트가 안 생겨 학습 데이터 공급이 오염된다 (MAP_SPEC §7).
    /// 실제 충돌 기둥이 필요하면 계약 변경 + 이양빈 승인이 선행되어야 한다.</para>
    /// </summary>
    public static class DungeonSetupTools
    {
        private const float ChainOriginY = -200f;
        private const string RootName = "Dungeon";

        private const string ConfigPath = "Assets/_Project/SO/DungeonConfig_Default.asset";
        private const string TilesetPath = "Assets/_Project/Sprites/Dungeon/DungeonTileset.png";
        private const string DoorPath = "Assets/_Project/Sprites/Dungeon/Door_Side.png";
        private const string ChestPath = "Assets/_Project/Sprites/Dungeon/Chest_Small.png";
        private const string DecorDir = "Assets/_Project/Sprites/Dungeon/Decor";

        // 바닥 베이스는 완전 균일 타일(113). 실측: 평균 (0.24,0.16,0.21) / 표준편차 0.000.
        // ⚠️ D7 이전에는 96을 썼는데 이 타일은 y6~y11에 얼룩이 있어 32×18 방에서 576번 반복되며
        //    격자 무늬로 보였다 (사람 지적 "타일링이 눈에 거슬린다"). 베이스는 반드시 무늬 없는 타일로 둔다.
        private const string FloorSprite = "DungeonTileset_113";

        // 바닥 디테일 — 베이스 위에 성기게 얹는다. 전부 같은 바닥색이라 얼룩만 더해진다 (95~100 6종).
        // 배열 중복 = 가중치. 잉크량 실측(베이스와 다른 픽셀 비율): 96=10% 99=11% 97=19% 100=21% 95=33% 98=38%.
        // 진한 타일(95·98)이 인접해 뭉치면 그물 무늬로 보여 되레 "타일링" 인상을 준다 → 옅은 것 위주로 뽑는다.
        private static readonly string[] FloorDetailSprites =
        {
            "DungeonTileset_96", "DungeonTileset_96", "DungeonTileset_96",
            "DungeonTileset_99", "DungeonTileset_99", "DungeonTileset_99",
            "DungeonTileset_97", "DungeonTileset_100",
            "DungeonTileset_95", "DungeonTileset_98",
        };

        private const string WallSprite = "DungeonTileset_32";
        private const string WallTopSprite = "DungeonTileset_4";     // 벽 윗면 캡 (§4 높이 착시)

        // 세로(좌/우) 벽 전용 — 32의 주황 띠(y1~y4)를 벽돌로 덮은 파생 타일.
        // 32를 그대로 세로로 반복하면 띠가 1유닛마다 나와 사다리처럼 보인다 (D7 수정 대상 결함).
        private const string GeneratedDir = "Assets/_Project/Sprites/Dungeon/Generated";
        private const string WallSidePath = GeneratedDir + "/Wall_Side.png";

        // MAP_SPEC §3 Sorting Layer
        private const string LGround = "Ground", LDecor = "Decor", LUnits = "Units", LWalls = "Walls", LWallTops = "WallTops";

        /// <summary>체인 한 구간: 다음 방으로 가는 방향 + 복도 규격. 좁고 넓은 구간을 섞는다.</summary>
        private struct Step
        {
            public Vector2 Dir; public float Length; public float Width;
            public Step(Vector2 d, float len, float w) { Dir = d; Length = len; Width = w; }
        }

        // 시작방 → 전투방1..6 → 보스방 (방 8개 = 구간 7개).
        // 🔴 분기 없음(선형)은 유지하되 꺾어서 "일자"를 깬다 — 꺾임 금지는 계약이 아니라 MAP_SPEC 초안 문구다.
        private static readonly Step[] Steps = new Step[]
        {
            new Step(Vector2.right, 10f, 5f),    // 0→1  표준
            new Step(Vector2.right, 16f, 3f),    // 1→2  길고 좁은 통로
            new Step(Vector2.up,    10f, 6f),    // 2→3  ⤴ 꺾임
            new Step(Vector2.right, 12f, 4f),    // 3→4
            new Step(Vector2.down,  14f, 7f),    // 4→5  ⤵ 꺾임, 넓은 홀
            new Step(Vector2.right,  8f, 4f),    // 5→6  짧고 좁게
            new Step(Vector2.right, 18f, 6f),    // 6→7  보스방 앞 긴 진입로
        };

        /// <summary>
        /// 세로 벽 전용 파생 타일을 만든다 (멱등 — 이미 있으면 건너뜀).
        /// 원본 타일 32는 상단 y1~y4가 주황 띠(벽의 밝은 앞면)라, 가로 벽에서는 방을 향한 면으로 읽히지만
        /// <b>세로 벽에서 세로로 반복하면 1유닛마다 띠가 나와 사다리처럼 보인다.</b>
        /// 띠 구간을 아래쪽 벽돌 영역으로 덮어 "띠 없는 벽돌" 타일을 만든다.
        /// </summary>
        [MenuItem("Luddite/Setup/던전 파생 타일 생성 (멱등)")]
        public static void EnsureDerivedTiles()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(WallSidePath) != null)
            {
                Debug.Log("[던전 빌더] 파생 타일 이미 존재 — 건너뜀 (멱등)");
                return;
            }
            if (!System.IO.Directory.Exists(GeneratedDir)) System.IO.Directory.CreateDirectory(GeneratedDir);

            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            UnityEngine.ImageConversion.LoadImage(src, System.IO.File.ReadAllBytes(TilesetPath));

            const int T = 16, BAND_TOP = 0, BAND_BOTTOM = 5;   // 실측: 주황 띠 = y0~y4
            int c = 32 % 28, r = 32 / 28;                       // 타일 32 = (4,1)
            var outTex = new Texture2D(T, T, TextureFormat.RGBA32, false);
            for (int y = 0; y < T; y++)
            {
                // 띠 구간은 벽돌 영역(y6~y10)에서 끌어와 덮는다 — 이음매가 자연스럽도록 같은 벽돌 행을 쓴다
                int srcY = (y >= BAND_TOP && y < BAND_BOTTOM) ? 6 + y : y;
                for (int x = 0; x < T; x++)
                    outTex.SetPixel(x, T - 1 - y, src.GetPixel(c * T + x, src.height - 1 - (r * T + srcY)));
            }
            outTex.Apply();
            System.IO.File.WriteAllBytes(WallSidePath, UnityEngine.ImageConversion.EncodeToPNG(outTex));
            AssetDatabase.ImportAsset(WallSidePath, ImportAssetOptions.ForceUpdate);

            var imp = (TextureImporter)AssetImporter.GetAtPath(WallSidePath);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 16f;                 // 타일 카테고리 PPU (CLAUDE.md)
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.wrapMode = TextureWrapMode.Clamp;
            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;   // Tiled drawMode 필수
            imp.SetTextureSettings(st);
            imp.SaveAndReimport();

            Debug.Log("[던전 빌더] 파생 타일 생성: " + WallSidePath + " (타일 32의 주황 띠 제거)");
        }

        [MenuItem("Luddite/Setup/던전 체인 생성 (멱등)")]
        public static void Build()
        {
            EnsureDerivedTiles();

            DungeonConfigSO config = AssetDatabase.LoadAssetAtPath<DungeonConfigSO>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<DungeonConfigSO>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
            }

            Sprite floor = FindSprite(TilesetPath, FloorSprite);
            Sprite wall = FindSprite(TilesetPath, WallSprite);
            Sprite wallTop = FindSprite(TilesetPath, WallTopSprite) ?? wall;
            Sprite doorClosed = FindSprite(DoorPath, "Door_Side_0");
            Sprite doorOpen = FindSprite(DoorPath, "Door_Side_3");
            Sprite chestClosed = FindSprite(ChestPath, "Chest_Small_0");
            Sprite chestOpen = FindSprite(ChestPath, "Chest_Small_3");
            if (floor == null || wall == null) { Debug.LogError("[던전 빌더] 타일 스프라이트 없음"); return; }

            Sprite wallSide = AssetDatabase.LoadAssetAtPath<Sprite>(WallSidePath) ?? wall;

            var details = new System.Collections.Generic.List<Sprite>();
            foreach (string n in FloorDetailSprites)
            {
                Sprite s = FindSprite(TilesetPath, n);
                if (s != null) details.Add(s);
            }
            Sprite[] floorDetails = details.ToArray();

            // 🔴 기둥은 Column_3 하나로 통일한다 (실측 평균 0.59,0.36,0.34 — R/B 1.74로 명확한 난색).
            //    · Column_5·6 = 한색(B>R) → 예약 색역(파랑=전공색) 침범. CLAUDE.md 아트 색 규칙 위반
            //    · Column_1·4 = 저채도 자주 → "고채도 보라" 예약에 닿을 소지
            //    · Column_2 = R/B 1.27로 수치상 난색이나 화면에서는 창백한 청백색으로 읽힌다 (D7 실제 캡처로 확인)
            //    방 간 차별화는 계약상 "타일·조명 색 테마로만"이므로 기둥을 통일하는 편이 규격에 맞다.
            Sprite[] pillars = LoadDecor("Column_3");
            Sprite[] props = LoadDecor("Bones_", "Crate", "Vase_", "Chains");

            GameObject old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            GameObject root = new GameObject(RootName);
            root.transform.position = new Vector3(0f, ChainOriginY, 0f);

            float hx = config.RoomHalfExtents.x, hy = config.RoomHalfExtents.y, t = config.WallThickness;
            int total = Steps.Length + 1;
            int last = total - 1;

            // 1) 방 중심 좌표 — 구간 방향·길이를 누적한다
            var centers = new Vector2[total];
            centers[0] = Vector2.zero;
            for (int i = 0; i < Steps.Length; i++)
            {
                float alongHalf = Mathf.Abs(Steps[i].Dir.x) > 0.5f ? hx : hy;
                centers[i + 1] = centers[i] + Steps[i].Dir * (alongHalf * 2f + Steps[i].Length);
            }

            // 배치는 전부 결정론(방 인덱스·격자 해시) — 난수 시드를 쓰지 않는다.
            // 이전에는 System.Random을 돌려 소품이 흩어졌고, 호출 순서가 바뀌면 배치도 흔들렸다 (D7 수정)
            var rooms = new Room[total];

            for (int i = 0; i < total; i++)
            {
                Vector2 c = centers[i];
                bool isStart = i == 0, isBoss = i == last;
                string label = isStart ? "Room_00_Start" : (isBoss ? "Room_" + i.ToString("00") + "_Boss"
                                                                   : "Room_" + i.ToString("00") + "_Combat");
                GameObject go = new GameObject(label);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = c;

                // 이 방에서 뚫려야 하는 변: 들어온 쪽(이전 구간의 반대) + 나가는 쪽
                Vector2 entryDir = i > 0 ? -Steps[i - 1].Dir : Vector2.zero;
                Vector2 exitDir = i < last ? Steps[i].Dir : Vector2.zero;
                float entryGap = i > 0 ? Steps[i - 1].Width : 0f;
                float exitGap = i < last ? Steps[i].Width : 0f;

                MakeTiled(go.transform, "Floor", floor, Vector2.zero, new Vector2(hx * 2f, hy * 2f), LGround, 0, false);
                ScatterFloorDetail(go.transform, hx, hy, floorDetails, i);

                // 벽 4면 — 뚫린 변만 구멍을 남긴다.
                // 좌/우는 띠 없는 파생 타일(wallSide), 상/하는 원본(wall). 위 상수 주석 참조
                BuildWall(go.transform, Vector2.left,  hx, hy, t, GapOn(Vector2.left,  entryDir, exitDir, entryGap, exitGap), wallSide, wallTop);
                BuildWall(go.transform, Vector2.right, hx, hy, t, GapOn(Vector2.right, entryDir, exitDir, entryGap, exitGap), wallSide, wallTop);
                BuildWall(go.transform, Vector2.up,    hx, hy, t, GapOn(Vector2.up,    entryDir, exitDir, entryGap, exitGap), wall, wallTop);
                BuildWall(go.transform, Vector2.down,  hx, hy, t, GapOn(Vector2.down,  entryDir, exitDir, entryGap, exitGap), wall, wallTop);

                var trig = go.AddComponent<BoxCollider2D>();
                trig.isTrigger = true;
                trig.size = new Vector2(hx * 2f - 3f, hy * 2f - 3f);

                Room room = go.AddComponent<Room>();
                var so = new SerializedObject(room);
                so.FindProperty("_chainIndex").intValue = i;
                so.FindProperty("_isCombatRoom").boolValue = !isStart;   // 보스방도 웨이브(7)를 돌린다

                if (i > 0)
                    so.FindProperty("_entryDoor").objectReferenceValue =
                        MakeDoor(go.transform, "Door_Entry", doorClosed, doorOpen, entryDir, hx, hy, t, entryGap);
                if (i < last)
                    so.FindProperty("_exitDoor").objectReferenceValue =
                        MakeDoor(go.transform, "Door_Exit", doorClosed, doorOpen, exitDir, hx, hy, t, exitGap);
                if (!isStart && !isBoss && chestClosed != null)
                    so.FindProperty("_chest").objectReferenceValue = MakeChest(go.transform, chestClosed, chestOpen);
                so.ApplyModifiedProperties();
                rooms[i] = room;

                ScatterDecor(go.transform, hx, hy, pillars, props, i, isBoss);
            }

            // 2) 복도
            for (int i = 0; i < Steps.Length; i++)
            {
                Vector2 d = Steps[i].Dir;
                bool horiz = Mathf.Abs(d.x) > 0.5f;
                float alongHalf = horiz ? hx : hy;
                Vector2 mid = centers[i] + d * (alongHalf + Steps[i].Length * 0.5f);
                float len = Steps[i].Length, w = Steps[i].Width;

                GameObject cor = new GameObject("Corridor_" + i.ToString("00") + (horiz ? "_H" : "_V"));
                cor.transform.SetParent(root.transform, false);
                cor.transform.localPosition = mid;
                Vector2 floorSize = horiz ? new Vector2(len, w) : new Vector2(w, len);
                MakeTiled(cor.transform, "Floor", floor, Vector2.zero, floorSize, LGround, 0, false);
                ScatterFloorDetail(cor.transform, floorSize.x * 0.5f, floorSize.y * 0.5f, floorDetails, 100 + i);

                if (horiz)
                {
                    MakeWallWithTop(cor.transform, "Wall_Top", wall, wallTop, new Vector2(0f, w * 0.5f + t * 0.5f), new Vector2(len, t));
                    MakeWallWithTop(cor.transform, "Wall_Bottom", wall, wallTop, new Vector2(0f, -(w * 0.5f + t * 0.5f)), new Vector2(len, t));
                }
                else
                {
                    // 세로 복도의 좌/우 벽도 띠 없는 파생 타일 — 방과 같은 이유
                    MakeWallWithTop(cor.transform, "Wall_Left", wallSide, wallTop, new Vector2(-(w * 0.5f + t * 0.5f), 0f), new Vector2(t, len));
                    MakeWallWithTop(cor.transform, "Wall_Right", wallSide, wallTop, new Vector2((w * 0.5f + t * 0.5f), 0f), new Vector2(t, len));
                }
            }

            // 3) DungeonManager 배선
            GameObject mgrGo = GameObject.Find("DungeonManager") ?? new GameObject("DungeonManager");
            var mgr = mgrGo.GetComponent<DungeonManager>() ?? mgrGo.AddComponent<DungeonManager>();
            var mso = new SerializedObject(mgr);
            mso.FindProperty("_config").objectReferenceValue = config;
            mso.FindProperty("_waveManager").objectReferenceValue = Object.FindFirstObjectByType<WaveManager>();
            mso.FindProperty("_gameManager").objectReferenceValue = Object.FindFirstObjectByType<GameManager>();
            mso.FindProperty("_cameraFollow").objectReferenceValue = Object.FindFirstObjectByType<CameraFollow>();
            var pc = Object.FindFirstObjectByType<Luddite.Player.PlayerController>();
            mso.FindProperty("_player").objectReferenceValue = pc != null ? pc.transform : null;
            var arr = mso.FindProperty("_rooms");
            arr.arraySize = total;
            for (int i = 0; i < total; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = rooms[i];
            mso.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            var bounds = new Bounds(centers[0], Vector3.zero);
            for (int i = 1; i < total; i++) bounds.Encapsulate(centers[i]);
            Debug.Log("[던전 빌더] 완료 — 방 " + total + "개 + 복도 " + Steps.Length +
                      "개 (꺾임 " + CountTurns() + "회, 복도 폭 3~7 변주), 체인 범위 " + bounds.size);
        }

        private static int CountTurns()
        {
            int n = 0;
            for (int i = 1; i < Steps.Length; i++) if (Steps[i].Dir != Steps[i - 1].Dir) n++;
            return n;
        }

        /// <summary>이 변에 구멍이 있으면 폭을, 없으면 0을 준다.</summary>
        private static float GapOn(Vector2 side, Vector2 entryDir, Vector2 exitDir, float entryGap, float exitGap)
        {
            if (side == entryDir) return entryGap;
            if (side == exitDir) return exitGap;
            return 0f;
        }

        /// <summary>한 변의 벽. 구멍이 있으면 양쪽 2조각으로 자른다. 전부 WallTops 캡을 동반한다.</summary>
        private static void BuildWall(Transform parent, Vector2 side, float hx, float hy, float t, float gap,
            Sprite wall, Sprite wallTop)
        {
            bool vertical = Mathf.Abs(side.x) > 0.5f;      // 좌/우 벽 = 세로로 긴 벽
            string name = vertical ? (side.x < 0 ? "Wall_Left" : "Wall_Right") : (side.y > 0 ? "Wall_Top" : "Wall_Bottom");
            float pos = vertical ? (hx + t * 0.5f) * side.x : (hy + t * 0.5f) * side.y;
            float half = vertical ? hy : hx;
            float full = half * 2f + (vertical ? 0f : t * 2f);   // 상하 벽은 모서리를 덮어 감싼다

            if (gap <= 0f)
            {
                Vector2 p = vertical ? new Vector2(pos, 0f) : new Vector2(0f, pos);
                Vector2 s = vertical ? new Vector2(t, full) : new Vector2(full, t);
                MakeWallWithTop(parent, name, wall, wallTop, p, s);
                return;
            }
            float seg = half - gap * 0.5f;
            if (seg <= 0.01f) return;
            float segCenter = gap * 0.5f + seg * 0.5f;
            for (int k = 0; k < 2; k++)
            {
                float off = k == 0 ? segCenter : -segCenter;
                Vector2 p = vertical ? new Vector2(pos, off) : new Vector2(off, pos);
                Vector2 s = vertical ? new Vector2(t, seg) : new Vector2(seg, t);
                MakeWallWithTop(parent, name + (k == 0 ? "_A" : "_B"), wall, wallTop, p, s);
            }
        }

        /// <summary>
        /// 벽 1개 = 충돌 앞면(Walls) + 윗면 캡(WallTops). MAP_SPEC §4 높이 착시 —
        /// 캡이 유닛보다 위 레이어라 벽에 붙은 캐릭터가 살짝 가려져 벽이 서 있어 보인다.
        /// </summary>
        private static void MakeWallWithTop(Transform parent, string name, Sprite wall, Sprite wallTop,
            Vector2 localPos, Vector2 size)
        {
            MakeTiled(parent, name, wall, localPos, size, LWalls, 0, true);
            bool horizontalWall = size.x >= size.y;
            Vector2 capPos = horizontalWall ? localPos + new Vector2(0f, size.y * 0.5f + 0.25f)
                                            : localPos + new Vector2(0f, size.y * 0.5f - 0.25f);
            Vector2 capSize = horizontalWall ? new Vector2(size.x, 0.5f) : new Vector2(size.x, 0.5f);
            MakeTiled(parent, name + "_Cap", wallTop, capPos, capSize, LWallTops, 0, false);
        }

        /// <summary>
        /// 바닥 얼룩. 베이스(균일 타일) 위에 <b>성기게</b> 얹어 "같은 무늬 반복" 인상을 없앤다.
        /// 좌표는 타일 격자에 스냅하고 해시로 결정론 배치 — 빌더를 다시 돌려도 같은 그림이 나온다.
        /// </summary>
        private static void ScatterFloorDetail(Transform parent, float hx, float hy, Sprite[] details, int seed)
        {
            if (details.Length == 0) return;
            var holder = new GameObject("FloorDetail");
            holder.transform.SetParent(parent, false);

            int nx = Mathf.FloorToInt(hx * 2f), ny = Mathf.FloorToInt(hy * 2f);
            for (int gy = 0; gy < ny; gy++)
            {
                for (int gx = 0; gx < nx; gx++)
                {
                    // 결정론 해시 (System.Random을 쓰면 호출 순서에 얽혀 다른 배치가 흔들린다)
                    int h = ((gx * 73856093) ^ (gy * 19349663) ^ (seed * 83492791)) & 0x7fffffff;
                    if (h % 100 >= 10) continue;                  // 밀도 10% — 과밀하면 다시 무늬로 보인다

                    // 종류는 위치와 다른 해시로 뽑는다 — 같은 h를 나눠 쓰면 종류가 위치에 상관돼 줄무늬가 생긴다
                    int h2 = ((gx * 19349663) ^ (gy * 83492791) ^ (seed * 73856093)) & 0x7fffffff;
                    Sprite s = details[h2 % details.Length];
                    float x = -hx + 0.5f + gx;
                    float y = -hy + 0.5f + gy;
                    MakeSprite(holder.transform, "FloorDetail", s, new Vector2(x, y), LGround, 1);
                }
            }
        }

        /// <summary>
        /// 방 장식. <b>전부 비충돌</b> (🔴 계약 — 위 클래스 주석 참조).
        /// 기둥은 Decor 레이어라 플레이어가 앞을 지나간다. 스폰 링·중앙 상자 자리를 피해 배치.
        ///
        /// <para>⚠️ D7에 <b>난수 흩뿌리기를 걷어냈다.</b> 이전에는 위치·종류를 전부 <c>rand</c>로 뽑아
        /// 소품이 허공에 흩어져 "난잡하다"는 지적을 받았다. 지금은 <b>벽을 따라 난 고정 슬롯</b>에
        /// 좌우 대칭으로 놓는다 — 배치에 논리가 보이고, 방마다 종류만 바뀌어 일관성이 유지된다.</para>
        /// </summary>
        private static void ScatterDecor(Transform room, float hx, float hy, Sprite[] pillars, Sprite[] props,
            int roomIndex, bool isBoss)
        {
            var holder = new GameObject("Decor");
            holder.transform.SetParent(room, false);

            // 기둥 — 네 모서리 안쪽 대칭 (보스방은 패턴 가독성 위해 생략)
            if (!isBoss && pillars.Length > 0)
            {
                Sprite p = pillars[roomIndex % pillars.Length];
                float px = hx - 4.5f, py = hy - 3.5f;
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        MakeSprite(holder.transform, "Pillar", p, new Vector2(px * sx, py * sy), LDecor, 1);
            }

            if (props.Length == 0) return;

            // 소품 — 벽을 따라 난 고정 슬롯. 상/하 벽 각 3개 + 좌/우 벽 각 1개 = 8개
            // (MAP_SPEC §3 "방당 5~10개만 — 과밀 금지, 탄 가독성")
            float inset = 1.6f;                       // 벽 안쪽으로 들여놓는 거리
            var slots = new System.Collections.Generic.List<Vector2>();
            float[] xs = { -hx * 0.55f, 0f, hx * 0.55f };
            foreach (float x in xs)
            {
                slots.Add(new Vector2(x, hy - inset));
                slots.Add(new Vector2(x, -hy + inset));
            }
            slots.Add(new Vector2(-hx + inset, 0f));
            slots.Add(new Vector2(hx - inset, 0f));

            for (int i = 0; i < slots.Count; i++)
            {
                // 방 중앙 상자 자리는 위 슬롯이 애초에 비켜 있다 (x=0 슬롯도 y가 벽 쪽)
                Sprite s = props[(roomIndex * 3 + i) % props.Length];
                MakeSprite(holder.transform, "Prop", s, slots[i], LDecor, 0);
            }
        }

        private static GameObject MakeSprite(Transform parent, string name, Sprite sprite, Vector2 localPos,
            string sortingLayer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = order;
            return go;
        }

        private static GameObject MakeTiled(Transform parent, string name, Sprite sprite, Vector2 localPos,
            Vector2 size, string sortingLayer, int order, bool solid)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = order;
            go.transform.localScale = Vector3.one;
            if (solid)
            {
                var bc = go.AddComponent<BoxCollider2D>();
                bc.size = size;
                go.AddComponent<Luddite.Combat.ProjectileBlocker>();
            }
            return go;
        }

        private static Door MakeDoor(Transform parent, string name, Sprite closed, Sprite open,
            Vector2 side, float hx, float hy, float t, float gap)
        {
            bool vertical = Mathf.Abs(side.x) > 0.5f;
            Vector2 pos = vertical ? new Vector2((hx + t * 0.5f) * side.x, 0f) : new Vector2(0f, (hy + t * 0.5f) * side.y);
            Vector2 size = vertical ? new Vector2(t, gap) : new Vector2(gap, t);

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = closed;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;
            sr.sortingLayerName = LWalls;
            sr.sortingOrder = 1;
            go.transform.localScale = Vector3.one;
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = size;
            go.AddComponent<Luddite.Combat.ProjectileBlocker>();
            Door door = go.AddComponent<Door>();
            var so = new SerializedObject(door);
            so.FindProperty("_blocker").objectReferenceValue = bc;
            so.FindProperty("_closedSprite").objectReferenceValue = closed;
            so.FindProperty("_openSprite").objectReferenceValue = open;
            so.ApplyModifiedProperties();
            return door;
        }

        private static Chest MakeChest(Transform parent, Sprite closed, Sprite open)
        {
            GameObject go = new GameObject("Chest");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector2.zero;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = closed;
            sr.sortingLayerName = LUnits;
            sr.sortingOrder = 0;
            go.transform.localScale = Vector3.one * 2f;
            Chest chest = go.AddComponent<Chest>();
            var so = new SerializedObject(chest);
            so.FindProperty("_closedSprite").objectReferenceValue = closed;
            so.FindProperty("_openSprite").objectReferenceValue = open;
            so.ApplyModifiedProperties();
            go.SetActive(false);
            return chest;
        }

        private static Sprite[] LoadDecor(params string[] prefixes)
        {
            var list = new System.Collections.Generic.List<Sprite>();
            var guids = AssetDatabase.FindAssets("t:Sprite", new string[] { DecorDir });
            for (int i = 0; i < guids.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                string n = System.IO.Path.GetFileNameWithoutExtension(p);
                for (int k = 0; k < prefixes.Length; k++)
                {
                    if (!n.StartsWith(prefixes[k])) continue;
                    var s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                    if (s != null) list.Add(s);
                    break;
                }
            }
            return list.ToArray();
        }

        private static Sprite FindSprite(string assetPath, string spriteName)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i] as Sprite;
                if (s != null && s.name == spriteName) return s;
            }
            return null;
        }
    }
}
