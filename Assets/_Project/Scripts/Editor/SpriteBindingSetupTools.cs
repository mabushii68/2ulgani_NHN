using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Luddite.Core;
using Luddite.Enemies;
using Luddite.Player;

namespace Luddite.EditorTools
{
    /// <summary>
    /// D3 반입 픽셀 아트를 프리팹·씬에 배선하는 빌더 (멱등 — 몇 번 실행해도 같은 결과).
    ///
    /// <para>
    /// ⚠️ <b>실행 순서 주의</b>: <c>EliteSetupTools</c>·<c>BossSetupTools</c>·<c>PainterBotSetupTools</c>·
    /// <c>CoderBotSetupTools</c>는 프리팹을 <b>처음부터 다시 만들면서</b> 플레이스홀더 도형을 붙인다.
    /// 그 빌더들을 재실행했다면 <b>이 빌더를 마지막에 다시 실행</b>해야 아트가 되돌아오지 않는다
    /// (폰트의 <c>FontSetupTools</c>와 같은 관계다).
    /// </para>
    ///
    /// <para>
    /// 배선 대상은 <b>뷰 계층뿐</b>이다 — 콜라이더 반지름·속도·데미지 같은 밸런스 수치는 SO 소관이라
    /// 건드리지 않는다(CLAUDE.md 규칙 2). 적 FSM 코드도 수정하지 않는다: idle/walk 전환은
    /// <see cref="DirectionalSpriteAnimator"/>가 Rigidbody2D 속도만 보고 스스로 판단한다.
    /// </para>
    /// </summary>
    public static class SpriteBindingSetupTools
    {
        private const string SPRITES = "Assets/_Project/Sprites/";
        private const string PREFABS = "Assets/_Project/Prefabs/";
        private const string SCENE_PATH = "Assets/_Project/Scenes/Main.unity";

        /// <summary>4방향 시트의 행 수. 시트 규약이라 상수 (D3 세션 5 확정).</summary>
        private const int ROWS = DirectionalSpriteAnimator.ROW_COUNT;

        private const float IDLE_FPS = 6f;
        private const float WALK_FPS = 10f;

        [MenuItem("Luddite/Setup/픽셀 아트 배선 (프리팹 + 씬)")]
        public static void BindAll()
        {
            int changed = 0;

            // 엘리트만 몸통 틴트를 유지한다 — 마젠타는 🔴 §10.4 계약이고 GDD §5.1이 엘리트 색으로 지정한 것이다.
            changed += BindEnemy(PREFABS + "ChatbotDrone.prefab", "Enemies/Beholder_idle", "Enemies/Beholder_move") ? 1 : 0;
            changed += BindEnemy(PREFABS + "EliteDrone.prefab", "Enemies/Beholder_idle", "Enemies/Beholder_move", keepBodyTint: true) ? 1 : 0;
            changed += BindEnemy(PREFABS + "PainterBot.prefab", "Enemies/Wizard_idle", "Enemies/Wizard_walk") ? 1 : 0;
            changed += BindEnemy(PREFABS + "CoderBot.prefab", "Enemies/Imp_idle", "Enemies/Imp_walk") ? 1 : 0;
            changed += BindEnemy(PREFABS + "BossLLM.prefab", "Enemies/Djinn_idle", "Enemies/Djinn_walk") ? 1 : 0;

            // 투사체는 원본 스프라이트 지름을 함께 갱신해야 한다 — Projectile.Launch가 이 값으로
            // "SO가 지정한 지름"에 맞는 스케일을 역산하기 때문에, 스프라이트만 바꾸면 크기가 어긋난다.
            changed += BindProjectile(PREFABS + "Projectile.prefab", "Projectiles/MagicMissile") ? 1 : 0;
            changed += BindProjectile(PREFABS + "EnemyProjectile.prefab", "Projectiles/EnergyBall") ? 1 : 0;

            changed += BindPredictiveSprite() ? 1 : 0;
            changed += BindPlayer() ? 1 : 0;

            AssetDatabase.SaveAssets();
            Debug.Log($"[SpriteBinding] 배선 완료 — 대상 {changed}건");
        }

        // ────────────────────────────────── 적

        private static bool BindEnemy(string prefabPath, string idleSheet, string walkSheet, bool keepBodyTint = false)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogError($"[SpriteBinding] 프리팹을 열지 못함 — {prefabPath}");
                return false;
            }

            try
            {
                SpriteRenderer body = FindBodyRenderer(root);
                if (body == null)
                {
                    Debug.LogError($"[SpriteBinding] Body SpriteRenderer 없음 — {prefabPath}");
                    return false;
                }

                DirectionalSpriteAnimator animator = root.GetComponent<DirectionalSpriteAnimator>();
                if (animator == null) animator = root.AddComponent<DirectionalSpriteAnimator>();

                var clips = new List<(string name, string sheet, float fps)>
                {
                    (DirectionalSpriteAnimator.CLIP_IDLE, idleSheet, IDLE_FPS),
                    (DirectionalSpriteAnimator.CLIP_WALK, walkSheet, WALK_FPS),
                };

                // 멈춰서 쏠 때 몸이 조준 방향을 보게 한다 (AimPivot은 FSM이 이미 표적 쪽으로 돌린다)
                Transform aimPivot = root.transform.Find("AimPivot");
                if (!WriteAnimator(animator, body, clips, autoDrive: true, facingSource: aimPivot)) return false;

                // 애니메이터가 돌기 전(에디터 미리보기·첫 프레임)에도 제 모습이 보이게 정면 1프레임을 심어 둔다
                body.sprite = LoadFrame(idleSheet, DirectionalSpriteAnimator.ROW_DOWN, 0);

                // 무채색 틴트를 걷어낸다: 스프라이트를 반입할 때 이미 휘도 회색조로 변환했으므로
                // (🔴 §10.4 "적 무채색"은 그 단계에서 성립한다) 틴트가 한 번 더 곱해질 이유가 없다.
                // 남겨 두면 원래 어두운 스프라이트가 이중으로 어두워진다 — 코딩봇(Imp)이 거의 안 보였다.
                if (!keepBodyTint) body.color = Color.white;

                HideMuzzlePlaceholder(root);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[SpriteBinding] {System.IO.Path.GetFileNameWithoutExtension(prefabPath)} ← {idleSheet} / {walkSheet}");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ────────────────────────────────── 투사체

        private static bool BindProjectile(string prefabPath, string spritePath)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITES + spritePath + ".png");
            if (sprite == null)
            {
                Debug.LogError($"[SpriteBinding] 스프라이트 없음 — {spritePath}");
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) return false;

            try
            {
                SpriteRenderer renderer = root.GetComponentInChildren<SpriteRenderer>();
                if (renderer == null)
                {
                    Debug.LogError($"[SpriteBinding] SpriteRenderer 없음 — {prefabPath}");
                    return false;
                }
                renderer.sprite = sprite;

                // 진행 방향으로 눕는 탄이라 "지름"의 기준은 가로 길이다 (Projectile이 transform.right를 방향에 맞춘다)
                float baseDiameter = sprite.bounds.size.x;

                var projectile = root.GetComponent<Luddite.Combat.Projectile>();
                if (projectile != null)
                {
                    SerializedObject so = new SerializedObject(projectile);
                    so.FindProperty("_spriteBaseDiameter").floatValue = baseDiameter;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[SpriteBinding] {System.IO.Path.GetFileNameWithoutExtension(prefabPath)} ← {spritePath} (원본 지름 {baseDiameter:0.###}u)");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>🔴 §10.4 — 예측탄만 실루엣까지 다르게. 색(마젠타)은 EliteModifier가 이미 갖고 있다.</summary>
        private static bool BindPredictiveSprite()
        {
            Sprite darkOrb = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITES + "Projectiles/DarkOrb.png");
            if (darkOrb == null)
            {
                Debug.LogError("[SpriteBinding] DarkOrb.png 없음");
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PREFABS + "EliteDrone.prefab");
            if (root == null) return false;

            try
            {
                EliteModifier elite = root.GetComponent<EliteModifier>();
                if (elite == null)
                {
                    Debug.LogError("[SpriteBinding] EliteDrone에 EliteModifier 없음");
                    return false;
                }

                SerializedObject so = new SerializedObject(elite);
                so.FindProperty("_predictiveSprite").objectReferenceValue = darkOrb;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PREFABS + "EliteDrone.prefab");
                Debug.Log("[SpriteBinding] 예측탄 스프라이트 ← DarkOrb");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ────────────────────────────────── 플레이어 (씬)

        /// <summary>
        /// 플레이어는 프리팹이 없고 씬에 직접 있다. 적과 달리 자동 구동을 끄는 이유는
        /// <see cref="PlayerSpriteView"/> 주석 참조 (조준 방향 ≠ 이동 방향).
        ///
        /// TODO(전공별 외형): 지금은 3전공 모두 Sorcerer를 쓴다. Gladiator·Swashbuckler 시트도 반입돼 있으나,
        /// "전공별 다른 캐릭터"로 갈지 "한 캐릭터 + 전공색 틴트"(GDD §10.4 색 위계)로 갈지는 기획 결정이 필요하다.
        /// </summary>
        private static bool BindPlayer()
        {
            if (EditorSceneManager.GetActiveScene().path != SCENE_PATH)
                EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[SpriteBinding] 씬에 Player 태그 오브젝트 없음");
                return false;
            }

            SpriteRenderer body = FindBodyRenderer(player);
            if (body == null)
            {
                Debug.LogError("[SpriteBinding] Player/Body SpriteRenderer 없음");
                return false;
            }

            DirectionalSpriteAnimator animator = player.GetComponent<DirectionalSpriteAnimator>();
            if (animator == null) animator = player.AddComponent<DirectionalSpriteAnimator>();

            var clips = new List<(string name, string sheet, float fps)>
            {
                (DirectionalSpriteAnimator.CLIP_IDLE, "Characters/Sorcerer_idle", IDLE_FPS),
                (DirectionalSpriteAnimator.CLIP_WALK, "Characters/Sorcerer_walk", WALK_FPS),
            };

            if (!WriteAnimator(animator, body, clips, autoDrive: false)) return false;

            body.sprite = LoadFrame("Characters/Sorcerer_idle", DirectionalSpriteAnimator.ROW_DOWN, 0);

            // 회색 박스 시절의 잔재 2개를 걷어낸다.
            // ① 파란 전공색 틴트: 플레이스홀더는 흰 원이라 틴트가 곧 색이었지만, 이제는 색을 가진
            //    스프라이트에 곱해져 탁해진다. 전공색은 투사체·총열·HUD 아이콘이 이미 들고 있다.
            // ② 스케일 0.8: 1유닛 원을 0.8유닛 히트박스에 맞추려던 값. 지금은 스프라이트가 이미
            //    제 크기(몸통 0.78u)라 이중 축소가 되고, 무엇보다 4배 확대 × 0.8 = 3.2배라
            //    텍스처 1픽셀이 화면 정수 픽셀에 떨어지지 않아 픽셀이 흔들린다.
            body.color = Color.white;
            body.transform.localScale = Vector3.one;

            if (player.GetComponent<PlayerSpriteView>() == null) player.AddComponent<PlayerSpriteView>();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[SpriteBinding] Player ← Sorcerer_idle / Sorcerer_walk (자동 구동 off)");
            return true;
        }

        // ────────────────────────────────── 공통

        /// <summary>
        /// 회색 박스 시절의 총구 막대를 끈다. 지우지 않고 렌더러만 끄는 이유는
        /// <c>AimPivot/Muzzle</c> 트랜스폼이 조준 방향을 들고 있고, 그 방향을 애니메이터가
        /// <c>_facingSource</c>로 빌려 쓰기 때문이다 — 오브젝트를 지우면 배선이 끊긴다.
        /// 흰 막대가 픽셀 스프라이트를 관통해 보이던 문제만 없앤다.
        /// </summary>
        private static void HideMuzzlePlaceholder(GameObject root)
        {
            Transform muzzle = root.transform.Find("AimPivot/Muzzle");
            if (muzzle == null) return;

            SpriteRenderer renderer = muzzle.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.enabled = false;
        }

        /// <summary>Body라는 이름의 자식을 우선 찾고, 없으면 첫 SpriteRenderer로 물러선다.</summary>
        private static SpriteRenderer FindBodyRenderer(GameObject root)
        {
            Transform body = root.transform.Find("Body");
            if (body != null)
            {
                SpriteRenderer renderer = body.GetComponent<SpriteRenderer>();
                if (renderer != null) return renderer;
            }
            return root.GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// 애니메이터의 직렬화 필드를 통째로 다시 쓴다. 런타임 클래스에 세터를 뚫지 않고
        /// <see cref="SerializedObject"/>로 접근하는 이유는, 이 배선이 에디터 전용 관심사이기 때문이다.
        /// </summary>
        private static bool WriteAnimator(
            DirectionalSpriteAnimator animator,
            SpriteRenderer renderer,
            List<(string name, string sheet, float fps)> clips,
            bool autoDrive,
            Transform facingSource = null)
        {
            SerializedObject so = new SerializedObject(animator);
            so.FindProperty("_renderer").objectReferenceValue = renderer;
            so.FindProperty("_defaultClip").stringValue = DirectionalSpriteAnimator.CLIP_IDLE;
            so.FindProperty("_autoDriveFromBody").boolValue = autoDrive;
            so.FindProperty("_facingSource").objectReferenceValue = facingSource;

            SerializedProperty clipsProp = so.FindProperty("_clips");
            clipsProp.arraySize = clips.Count;

            for (int i = 0; i < clips.Count; i++)
            {
                (string name, string sheet, float fps) = clips[i];

                List<Sprite> frames = LoadSheet(sheet);
                if (frames == null) return false;

                if (frames.Count % ROWS != 0)
                {
                    Debug.LogError($"[SpriteBinding] {sheet}: 프레임 {frames.Count}개가 {ROWS}행으로 나눠떨어지지 않음");
                    return false;
                }
                int cols = frames.Count / ROWS;

                SerializedProperty clipProp = clipsProp.GetArrayElementAtIndex(i);
                clipProp.FindPropertyRelative("_name").stringValue = name;
                clipProp.FindPropertyRelative("_fps").floatValue = fps;
                clipProp.FindPropertyRelative("_loop").boolValue = true;

                SerializedProperty rowsProp = clipProp.FindPropertyRelative("_rows");
                rowsProp.arraySize = ROWS;

                for (int r = 0; r < ROWS; r++)
                {
                    SerializedProperty framesProp = rowsProp.GetArrayElementAtIndex(r).FindPropertyRelative("_frames");
                    framesProp.arraySize = cols;
                    for (int c = 0; c < cols; c++)
                        framesProp.GetArrayElementAtIndex(c).objectReferenceValue = frames[r * cols + c];
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>
        /// 슬라이스된 시트의 서브 애셋을 <c>{파일명}_{인덱스}</c> 순서대로 모은다.
        /// <c>LoadAllAssetsAtPath</c>의 반환 순서는 보장되지 않으므로 이름으로 정렬한다 —
        /// 여기서 순서가 흔들리면 방향과 프레임이 통째로 뒤섞인다.
        /// </summary>
        private static List<Sprite> LoadSheet(string relativePath)
        {
            string path = SPRITES + relativePath + ".png";
            string baseName = System.IO.Path.GetFileNameWithoutExtension(path);

            var byName = new Dictionary<string, Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite) byName[sprite.name] = sprite;
            }

            if (byName.Count == 0)
            {
                Debug.LogError($"[SpriteBinding] 스프라이트를 찾지 못함 — {path} (슬라이스 안 된 상태?)");
                return null;
            }

            var ordered = new List<Sprite>(byName.Count);
            for (int i = 0; i < byName.Count; i++)
            {
                string key = baseName + "_" + i;
                if (!byName.TryGetValue(key, out Sprite sprite))
                {
                    Debug.LogError($"[SpriteBinding] {path}: '{key}' 없음 — 슬라이스 이름 규약 불일치");
                    return null;
                }
                ordered.Add(sprite);
            }
            return ordered;
        }

        private static Sprite LoadFrame(string relativePath, int row, int column)
        {
            List<Sprite> frames = LoadSheet(relativePath);
            if (frames == null) return null;

            int cols = frames.Count / ROWS;
            int index = row * cols + column;
            return index >= 0 && index < frames.Count ? frames[index] : null;
        }
    }
}
