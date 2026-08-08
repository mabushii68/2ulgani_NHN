using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Luddite.Core;
using Luddite.Data;

namespace Luddite.EditorTools
{
    /// <summary>
    /// 던전 체인을 코드로 결정론 생성하는 빌더 (개정안 §2).
    ///
    /// <para><b>멱등:</b> `Dungeon` 루트를 통째로 지우고 다시 짓는다. 이 루트 아래는 전부 생성물이므로
    /// 손으로 편집하지 말 것 — 재실행하면 날아간다. <b>기존 `Arena`(폴백 아레나)는 건드리지 않는다.</b>
    /// 던전은 y = <see cref="ChainOriginY"/> 에 따로 지어 두 경로가 공간적으로 겹치지 않게 한다.</para>
    ///
    /// <para>방 7 + 복도 6의 좌표를 손으로 놓으면 재현이 불가능하므로 빌더가 필수다
    /// (CLAUDE.md "v1.1 던전 파일 배치").</para>
    /// </summary>
    public static class DungeonSetupTools
    {
        private const float ChainOriginY = -200f;   // 폴백 아레나(원점)와 분리
        private const string RootName = "Dungeon";

        private const string ConfigPath = "Assets/_Project/SO/DungeonConfig_Default.asset";
        private const string TilesetPath = "Assets/_Project/Sprites/Dungeon/DungeonTileset.png";
        private const string DoorPath = "Assets/_Project/Sprites/Dungeon/Door_Side.png";
        private const string ChestPath = "Assets/_Project/Sprites/Dungeon/Chest_Small.png";

        private const string FloorSprite = "DungeonTileset_96";
        private const string WallSprite = "DungeonTileset_32";

        [MenuItem("Luddite/Setup/던전 체인 생성 (멱등)")]
        public static void Build()
        {
            DungeonConfigSO config = AssetDatabase.LoadAssetAtPath<DungeonConfigSO>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<DungeonConfigSO>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[던전 빌더] DungeonConfig_Default 생성");
            }

            Sprite floor = FindSprite(TilesetPath, FloorSprite);
            Sprite wall = FindSprite(TilesetPath, WallSprite);
            Sprite doorClosed = FindSprite(DoorPath, "Door_Side_0");
            Sprite doorOpen = FindSprite(DoorPath, "Door_Side_3");
            Sprite chestClosed = FindSprite(ChestPath, "Chest_Small_0");
            Sprite chestOpen = FindSprite(ChestPath, "Chest_Small_3");
            if (floor == null || wall == null)
            {
                Debug.LogError("[던전 빌더] 타일 스프라이트 없음 — DungeonTileset 슬라이스 확인 필요");
                return;
            }

            GameObject old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            GameObject root = new GameObject(RootName);
            root.transform.position = new Vector3(0f, ChainOriginY, 0f);

            float hx = config.RoomHalfExtents.x, hy = config.RoomHalfExtents.y;
            float t = config.WallThickness, cw = config.CorridorWidth, cl = config.CorridorLength;
            float spacing = config.RoomSpacing;
            int total = config.TotalRooms;          // 시작방 + 전투방 N + 보스방
            int last = total - 1;

            // 암반 배경은 깔지 않는다 — MAP_SPEC §5-1 "맵 바깥엔 타일을 아예 안 깐다 → 자동으로 어둠".
            // 벽 바깥의 완전한 어둠이 건전풍 레이아웃 문법(§1-②)의 핵심이고, 카메라 Background
            // #0A0A0F가 그 어둠을 담당한다. D5에 잠시 깔았다가 이 스펙에 맞춰 철회했다.

            var rooms = new Room[total];
            for (int i = 0; i < total; i++)
            {
                Vector2 c = new Vector2(i * spacing, 0f);
                // 보스방도 웨이브를 돌린다 (웨이브 7 = 보스 웨이브). 시작방만 예외 —
                // 여기서 보스방을 빼면 웨이브 7이 영영 시작되지 않는다
                bool isCombat = i >= 1;
                bool isBoss = i == last;
                string label = i == 0 ? "Room_00_Start" : (isBoss ? "Room_" + i.ToString("00") + "_Boss"
                                                                  : "Room_" + i.ToString("00") + "_Combat");
                GameObject go = new GameObject(label);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = c;

                MakeTiled(go.transform, "Floor", floor, Vector2.zero, new Vector2(hx * 2f, hy * 2f), -10, false);
                // 상하 벽 — 전체 폭
                MakeTiled(go.transform, "Wall_Top", wall, new Vector2(0f, hy + t * 0.5f), new Vector2(hx * 2f + t * 2f, t), 0, true);
                MakeTiled(go.transform, "Wall_Bottom", wall, new Vector2(0f, -(hy + t * 0.5f)), new Vector2(hx * 2f + t * 2f, t), 0, true);
                // 좌우 벽 — 복도 쪽은 문 구멍을 남기고 2조각으로 자른다
                MakeSideWall(go.transform, "Wall_Left", wall, -(hx + t * 0.5f), hy, t, cw, i > 0);
                MakeSideWall(go.transform, "Wall_Right", wall, (hx + t * 0.5f), hy, t, cw, i < last);

                // 방 진입 트리거 — 문을 지나 방 안으로 들어와야 발화하도록 안쪽으로 넉넉히 인셋
                var trig = go.AddComponent<BoxCollider2D>();
                trig.isTrigger = true;
                trig.size = new Vector2(hx * 2f - 3f, hy * 2f - 3f);

                Room room = go.AddComponent<Room>();
                var so = new SerializedObject(room);
                so.FindProperty("_chainIndex").intValue = i;
                so.FindProperty("_isCombatRoom").boolValue = isCombat;

                if (i > 0)
                {
                    Door entry = MakeDoor(go.transform, "Door_Entry", doorClosed, doorOpen,
                        new Vector2(-(hx + t * 0.5f), 0f), new Vector2(t, cw));
                    so.FindProperty("_entryDoor").objectReferenceValue = entry;
                }
                if (i < last)
                {
                    Door exit = MakeDoor(go.transform, "Door_Exit", doorClosed, doorOpen,
                        new Vector2((hx + t * 0.5f), 0f), new Vector2(t, cw));
                    so.FindProperty("_exitDoor").objectReferenceValue = exit;
                }
                if (isCombat && !isBoss && chestClosed != null)   // 보스방은 상자 없음 (격파 = 즉시 승리)
                {
                    Chest chest = MakeChest(go.transform, chestClosed, chestOpen);
                    so.FindProperty("_chest").objectReferenceValue = chest;
                }
                so.ApplyModifiedProperties();
                rooms[i] = room;

                // 복도 (방 i → i+1)
                if (i < last)
                {
                    GameObject cor = new GameObject("Corridor_" + i.ToString("00"));
                    cor.transform.SetParent(root.transform, false);
                    cor.transform.localPosition = new Vector2(c.x + hx + cl * 0.5f, 0f);
                    MakeTiled(cor.transform, "Floor", floor, Vector2.zero, new Vector2(cl, cw), -10, false);
                    MakeTiled(cor.transform, "Wall_Top", wall, new Vector2(0f, cw * 0.5f + t * 0.5f), new Vector2(cl, t), 0, true);
                    MakeTiled(cor.transform, "Wall_Bottom", wall, new Vector2(0f, -(cw * 0.5f + t * 0.5f)), new Vector2(cl, t), 0, true);

                    // 복도 위·아래의 방 사이 빈 공간을 벽으로 메운다. 안 메우면 복도를 지나는 동안
                    // 화면 위아래에 검은 구멍이 뚫려 보인다 (카메라가 복도 구간에서 넓게 잡기 때문)
                    float fillH = (hy + t) - (cw * 0.5f + t);          // 복도 바깥벽 ~ 방 바깥벽까지
                    if (fillH > 0f)
                    {
                        float fillC = cw * 0.5f + t + fillH * 0.5f;
                        MakeTiled(cor.transform, "Fill_Upper", wall, new Vector2(0f, fillC), new Vector2(cl, fillH), -1, false);
                        MakeTiled(cor.transform, "Fill_Lower", wall, new Vector2(0f, -fillC), new Vector2(cl, fillH), -1, false);
                    }
                }
            }

            // DungeonManager 배선
            GameObject mgrGo = GameObject.Find("DungeonManager");
            if (mgrGo == null) { mgrGo = new GameObject("DungeonManager"); }
            var mgr = mgrGo.GetComponent<DungeonManager>();
            if (mgr == null) mgr = mgrGo.AddComponent<DungeonManager>();
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
            Debug.Log("[던전 빌더] 완료 — 방 " + total + "개(전투 " + config.CombatRoomCount + ") + 복도 " + last +
                      "개, 체인 원점 y=" + ChainOriginY + ", 방 간격 " + spacing);
        }

        /// <summary>좌/우 벽. 복도가 뚫린 쪽은 문 구멍(폭 gap)을 남기고 위·아래 2조각으로 만든다.</summary>
        private static void MakeSideWall(Transform parent, string name, Sprite sprite,
            float x, float hy, float t, float gap, bool hasGap)
        {
            if (!hasGap)
            {
                MakeTiled(parent, name, sprite, new Vector2(x, 0f), new Vector2(t, hy * 2f), 0, true);
                return;
            }
            float segTop = hy - gap * 0.5f;                       // 구멍 위쪽 조각 길이
            float segCenter = gap * 0.5f + segTop * 0.5f;
            MakeTiled(parent, name + "_Upper", sprite, new Vector2(x, segCenter), new Vector2(t, segTop), 0, true);
            MakeTiled(parent, name + "_Lower", sprite, new Vector2(x, -segCenter), new Vector2(t, segTop), 0, true);
        }

        private static GameObject MakeTiled(Transform parent, string name, Sprite sprite,
            Vector2 localPos, Vector2 size, int sortingOrder, bool solid)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;      // 대입 순서 주의: drawMode 뒤에 size·scale (localScale 덮어쓰기 함정)
            sr.size = size;
            sr.sortingOrder = sortingOrder;
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
            Vector2 localPos, Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = closed;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;
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
            sr.sortingOrder = 2;
            go.transform.localScale = Vector3.one * 2f;   // 16px 타일이라 그대로면 너무 작다
            Chest chest = go.AddComponent<Chest>();
            var so = new SerializedObject(chest);
            so.FindProperty("_closedSprite").objectReferenceValue = closed;
            so.FindProperty("_openSprite").objectReferenceValue = open;
            so.ApplyModifiedProperties();
            go.SetActive(false);                          // Arm() 전까지 숨김
            return chest;
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
