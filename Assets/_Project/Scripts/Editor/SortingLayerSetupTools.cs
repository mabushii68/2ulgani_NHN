using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Luddite.EditorTools
{
    /// <summary>
    /// MAP_SPEC §3 Sorting Layer 배정 (멱등).
    ///
    /// <para><b>왜 필요한가:</b> `Default`는 새로 만든 레이어들보다 <b>아래</b>다. 던전 바닥을 `Ground`로
    /// 올리는 순간 `Default`에 남아 있는 플레이어·적·탄이 전부 <b>바닥 밑으로 깔린다.</b>
    /// 던전 빌더를 돌린 뒤에는 이 도구를 반드시 함께 실행할 것
    /// (`FontSetupTools`를 마지막에 재실행하는 것과 같은 관계).</para>
    ///
    /// <para>순서(아래→위): Ground &lt; Decor &lt; Shadow &lt; Units &lt; Walls &lt; WallTops &lt; Projectiles &lt; VFX &lt; UI.
    /// <b>탄·조준선이 벽보다 위</b>인 것이 핵심 — 마젠타 조준선이 벽에 가리면 심리전 정보가 손실된다.</para>
    /// </summary>
    public static class SortingLayerSetupTools
    {
        private const string LGround = "Ground", LUnits = "Units", LWalls = "Walls", LProjectiles = "Projectiles";

        [MenuItem("Luddite/Setup/Sorting Layer 배정 (멱등)")]
        public static void Apply()
        {
            int n = 0;
            var log = new System.Text.StringBuilder();

            // 1) 씬의 플레이어 — 자식(스프라이트·조준 표식) 전부 Units
            var pc = Object.FindFirstObjectByType<Luddite.Player.PlayerController>();
            if (pc != null) n += SetAll(pc.gameObject, LUnits, log, "Player");

            // 2) 폴백 아레나 — 바닥은 Ground, 벽은 Walls
            var arena = GameObject.Find("Arena");
            if (arena != null)
            {
                var srs = arena.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < srs.Length; i++)
                {
                    string layer = srs[i].name == "Background" ? LGround : LWalls;
                    if (srs[i].sortingLayerName == layer) continue;
                    Undo.RecordObject(srs[i], "Sorting Layer");
                    srs[i].sortingLayerName = layer;
                    EditorUtility.SetDirty(srs[i]); n++;
                }
                log.AppendLine("  Arena → 바닥 Ground / 벽 Walls");
            }

            // 3) 적·보스 프리팹 → Units, 조준선·마커 → Projectiles (벽 위에 그려져야 한다)
            string[] enemyPrefabs = { "ChatbotDrone", "PainterBot", "CoderBot", "BossLLM", "EliteChatbot" };
            for (int i = 0; i < enemyPrefabs.Length; i++)
            {
                string path = "Assets/_Project/Prefabs/" + enemyPrefabs[i] + ".prefab";
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (pf == null) continue;
                int before = n;
                n += SetAll(pf, LUnits, log, enemyPrefabs[i]);
                // 예측탄 조준선·마커는 벽 위로
                var lrs = pf.GetComponentsInChildren<LineRenderer>(true);
                for (int k = 0; k < lrs.Length; k++)
                {
                    if (lrs[k].sortingLayerName == LProjectiles) continue;
                    lrs[k].sortingLayerName = LProjectiles; lrs[k].sortingOrder = 1; n++;
                }
                var em = pf.GetComponent<Luddite.Enemies.EliteModifier>();
                if (em != null)
                {
                    var marker = pf.transform.Find("TargetMarker");
                    if (marker != null)
                    {
                        var msr = marker.GetComponent<SpriteRenderer>();
                        if (msr != null) { msr.sortingLayerName = LProjectiles; msr.sortingOrder = 1; n++; }
                    }
                }
                if (n != before) EditorUtility.SetDirty(pf);
            }

            // 4) 투사체 → Projectiles
            var projPf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Projectile.prefab");
            if (projPf != null) { n += SetAll(projPf, LProjectiles, log, "Projectile"); EditorUtility.SetDirty(projPf); }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Sorting 배정] " + n + "건 변경\n" + log);
        }

        private static int SetAll(GameObject go, string layer, System.Text.StringBuilder log, string label)
        {
            var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
            int n = 0;
            for (int i = 0; i < srs.Length; i++)
            {
                if (srs[i].sortingLayerName == layer) continue;
                Undo.RecordObject(srs[i], "Sorting Layer");
                srs[i].sortingLayerName = layer;
                EditorUtility.SetDirty(srs[i]); n++;
            }
            if (n > 0) log.AppendLine("  " + label + " → " + layer + " (" + n + "개)");
            return n;
        }
    }
}
