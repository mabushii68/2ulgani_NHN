using UnityEngine;
using UnityEditor;
using Luddite.Core;
using Luddite.Data;

namespace Luddite.EditorTools
{
    /// <summary>
    /// 던전 루프 구조 스모크 (에디터 전용).
    ///
    /// <para>MCP로 키보드·마우스를 주입할 수 없으므로 <b>코드 경로를 직접 호출해</b> 검증한다
    /// (CLAUDE.md 세션 종료 조건 2). 플레이 모드 없이 확인 가능한 것만 다룬다 —
    /// 실제 이동·충돌로 트리거가 발화하는지는 사람 확인이 필요하다.</para>
    /// </summary>
    public static class DungeonSmokeTest
    {
        [MenuItem("Luddite/Dev/던전 루프 스모크")]
        public static void Run()
        {
            var log = new System.Text.StringBuilder();
            int pass = 0, fail = 0;

            var cfg = AssetDatabase.LoadAssetAtPath<DungeonConfigSO>("Assets/_Project/SO/DungeonConfig_Default.asset");
            Check(log, ref pass, ref fail, "DungeonConfig 존재", cfg != null);
            if (cfg == null) { Dump(log, pass, fail); return; }
            Check(log, ref pass, ref fail, "던전 토글 ON", cfg.Enabled);

            // 방이 화면보다 커야 추적 카메라가 작동한다
            var cam = Camera.main;
            float vh = cam != null ? cam.orthographicSize : 0f;
            float vw = cam != null ? vh * cam.aspect : 0f;
            Check(log, ref pass, ref fail,
                "방이 화면보다 큼 (X " + cfg.RoomHalfExtents.x + " > " + vw.ToString("F2") + ")", cfg.RoomHalfExtents.x > vw);
            Check(log, ref pass, ref fail,
                "방이 화면보다 큼 (Y " + cfg.RoomHalfExtents.y + " > " + vh.ToString("F2") + ")", cfg.RoomHalfExtents.y > vh);

            var mgr = Object.FindFirstObjectByType<DungeonManager>();
            Check(log, ref pass, ref fail, "DungeonManager 씬에 존재", mgr != null);
            var wm = Object.FindFirstObjectByType<WaveManager>();
            Check(log, ref pass, ref fail, "WaveManager 씬에 존재", wm != null);
            Check(log, ref pass, ref fail, "CameraFollow 씬에 존재", Object.FindFirstObjectByType<CameraFollow>() != null);

            var rooms = Object.FindObjectsByType<Room>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Array.Sort(rooms, delegate (Room a, Room b) { return a.ChainIndex.CompareTo(b.ChainIndex); });
            Check(log, ref pass, ref fail, "방 수 = " + cfg.TotalRooms, rooms.Length == cfg.TotalRooms);

            // 체인 배선 — 시작방은 입구문 없음, 보스방은 출구문·상자 없음, 나머지 전부 있음
            for (int i = 0; i < rooms.Length; i++)
            {
                Room r = rooms[i];
                bool isStart = r.ChainIndex == 0;
                bool isBoss = r.ChainIndex == rooms.Length - 1;
                Check(log, ref pass, ref fail, "방" + r.ChainIndex + " 전투 플래그", r.IsCombatRoom == !isStart);
                Check(log, ref pass, ref fail, "방" + r.ChainIndex + " 입구문", (r.EntryDoor != null) == !isStart);
                Check(log, ref pass, ref fail, "방" + r.ChainIndex + " 출구문", (r.ExitDoor != null) == !isBoss);
                Check(log, ref pass, ref fail, "방" + r.ChainIndex + " 상자", (r.Chest != null) == (!isStart && !isBoss));
            }

            // 문 잠금/해제가 콜라이더를 실제로 토글하는가
            Room combat = null;
            for (int i = 0; i < rooms.Length; i++) if (rooms[i].ChainIndex == 1) combat = rooms[i];
            if (combat != null && combat.ExitDoor != null)
            {
                var door = combat.ExitDoor;
                var bc = door.GetComponent<BoxCollider2D>();
                door.Lock();
                Check(log, ref pass, ref fail, "문 Lock → 통행 차단", door.IsLocked && bc != null && bc.enabled);
                door.Unlock();
                Check(log, ref pass, ref fail, "문 Unlock → 통행 허용", !door.IsLocked && bc != null && !bc.enabled);
                door.Lock();   // 원상 복구
            }

            // 방 클리어 → 출구 개방 + 상자 무장 + (자동오픈이면) 오픈 이벤트까지
            if (combat != null)
            {
                combat.ResetRoom();
                bool openedFired = false;
                System.Action handler = delegate () { openedFired = true; };
                if (combat.Chest != null) combat.Chest.Opened += handler;

                Check(log, ref pass, ref fail, "리셋 후 출구문 잠김", combat.ExitDoor != null && combat.ExitDoor.IsLocked);
                // 락인은 '들어온 뒤'에 걸린다. 입구를 처음부터 잠그면 방에 들어갈 수조차 없다 (D5에 실제로 낸 버그)
                Check(log, ref pass, ref fail, "리셋 후 입구문 열림 (락인은 진입 후)", combat.EntryDoor != null && !combat.EntryDoor.IsLocked);
                Check(log, ref pass, ref fail, "리셋 후 상자 비활성", combat.Chest != null && !combat.Chest.gameObject.activeSelf);

                combat.OnCleared();
                Check(log, ref pass, ref fail, "전멸 → 출구문 개방", combat.ExitDoor != null && !combat.ExitDoor.IsLocked);
                Check(log, ref pass, ref fail, "전멸 → 상자 등장", combat.Chest != null && combat.Chest.gameObject.activeSelf);
                if (cfg.AutoOpenChest)
                {
                    Check(log, ref pass, ref fail, "상자 자동 오픈 → 인터벌 신호 발행", openedFired && combat.Chest.IsOpened);
                }
                else
                {
                    // D7 수동 오픈 경로. 키 입력은 주입할 수 없으므로 **입력 이외의 전부**를 검사한다:
                    // ① 다가가는 것만으로는 안 열린다 ② 안내가 있고 처음엔 꺼져 있다 ③ Open()이 이벤트를 발행한다
                    Check(log, ref pass, ref fail, "수동 오픈 — 무장만으로는 안 열림",
                        combat.Chest != null && !combat.Chest.IsOpened && !openedFired);

                    Transform prompt = combat.Chest != null ? combat.Chest.transform.Find("Prompt") : null;
                    Check(log, ref pass, ref fail, "수동 오픈 — '[E] 열기' 안내 존재", prompt != null);
                    Check(log, ref pass, ref fail, "수동 오픈 — 안내는 기본 비활성(범위 밖)",
                        prompt != null && !prompt.gameObject.activeSelf);

                    // Open()은 private — 실제 키 입력이 도달하는 지점을 그대로 호출해 이벤트 배선을 확인한다
                    var openMethod = typeof(Luddite.Core.Chest).GetMethod("Open",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (openMethod != null && combat.Chest != null) openMethod.Invoke(combat.Chest, null);
                    Check(log, ref pass, ref fail, "수동 오픈 — Open() → 인터벌 신호 발행",
                        openedFired && combat.Chest != null && combat.Chest.IsOpened);
                }

                if (combat.Chest != null) combat.Chest.Opened -= handler;
                combat.ResetRoom();
            }

            // 통행 프로브 — 시작방 중심 → 전투방1 중심까지 실제로 걸어갈 수 있는가.
            // 문 상태 버그는 배선 검사로는 안 잡히고 이 프로브에서만 드러난다
            if (rooms.Length >= 2)
            {
                for (int i = 0; i < rooms.Length; i++) rooms[i].ResetRoom();
                Vector2 from = rooms[0].Center, to = rooms[1].Center;
                string blockers = "";
                int steps = Mathf.CeilToInt(Mathf.Abs(to.x - from.x) / 0.5f);
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 p = Vector2.Lerp(from, to, steps == 0 ? 0f : (float)s / steps);
                    var hits = Physics2D.OverlapCircleAll(p, 0.4f);   // 플레이어 반지름 0.4
                    for (int k = 0; k < hits.Length; k++)
                    {
                        if (hits[k].isTrigger) continue;
                        if (hits[k].GetComponentInParent<Luddite.Player.PlayerController>() != null) continue;
                        string who = (hits[k].transform.parent != null ? hits[k].transform.parent.name + "/" : "") + hits[k].name;
                        if (!blockers.Contains(who)) blockers += who + " ";
                    }
                }
                Check(log, ref pass, ref fail,
                    "시작방 → 전투방1 통행 가능" + (blockers == "" ? "" : " (막는 것: " + blockers + ")"), blockers == "");
            }

            // 🔴 폴백 경로: 토글 OFF에서 기존 아레나가 그대로 있는가
            var arena = GameObject.Find("Arena");
            Check(log, ref pass, ref fail, "폴백 아레나(Arena) 보존됨", arena != null);
            Check(log, ref pass, ref fail, "WaveManager 외부 제어 API 존재",
                wm != null && wm.GetType().GetMethod("BeginWaveNow") != null);

            Dump(log, pass, fail);
        }

        private static void Check(System.Text.StringBuilder log, ref int pass, ref int fail, string name, bool ok)
        {
            if (ok) { pass++; log.AppendLine("  ✅ " + name); }
            else { fail++; log.AppendLine("  ❌ " + name); }
        }

        private static void Dump(System.Text.StringBuilder log, int pass, int fail)
        {
            string head = "[던전 스모크] 통과 " + pass + " / 실패 " + fail;
            if (fail == 0) Debug.Log(head + "\n" + log);
            else Debug.LogError(head + "\n" + log);
        }
    }
}
