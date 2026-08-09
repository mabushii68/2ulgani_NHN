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
            changed += BindProjectile(PREFABS + "Projectile.prefab", "Projectiles/FireballBig") ? 1 : 0;
            changed += BindProjectile(PREFABS + "EnemyProjectile.prefab", "Projectiles/EnergyBall") ? 1 : 0;

            changed += BindPredictiveSprite() ? 1 : 0;
            changed += BindPlayer() ? 1 : 0;
            changed += BindArena() ? 1 : 0;
            changed += BindUi() ? 1 : 0;

            // 씬 저장은 여기서 한 번. BindPlayer 안에서만 저장하면 그 뒤의 아레나·UI 변경이
            // 메모리에만 남아, 다음 도메인 리로드에서 조용히 사라진다.
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
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

            BindAimIndicator(player);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[SpriteBinding] Player ← Sorcerer_idle / Sorcerer_walk (자동 구동 off)");
            return true;
        }

        /// <summary>
        /// 조준 표식을 화살표 스프라이트로 교체. 회색 박스 시절엔 늘어난 사각형("Barrel")이었다.
        /// 오브젝트 이름·계층은 그대로 둔다 — <c>AimPivot</c>이 조준 방향을 들고 있고
        /// 적 애니메이터가 참조하는 것과 같은 구조라, 이름을 바꾸면 배선 추적이 어려워진다.
        /// </summary>
        private static void BindAimIndicator(GameObject player)
        {
            Transform barrel = player.transform.Find("AimPivot/Barrel");
            if (barrel == null) return;

            SpriteRenderer renderer = barrel.GetComponent<SpriteRenderer>();
            if (renderer == null) return;

            Sprite arrow = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITES + "UI/AimArrow.png");
            if (arrow == null)
            {
                Debug.LogError("[SpriteBinding] AimArrow.png 없음");
                return;
            }

            renderer.sprite = arrow;
            // 0.75배 = 4배 확대 × 0.75 = 화면 3픽셀이라 정수 배율이 유지된다 (0.8 같은 값은 흔들린다)
            barrel.localScale = new Vector3(0.75f, 0.75f, 1f);
            barrel.localPosition = new Vector3(0.6f, 0f, 0f);
        }

        // ────────────────────────────────── 아레나

        /// <summary>
        /// 바닥·벽을 타일 스프라이트로. GDD §11 "배경: 어두운 단색 + 미세 그리드"를
        /// 단색 사각형 대신 <b>타일링된 돌바닥</b>으로 만든다 — 격자가 그려져 있어 거리감이 생긴다.
        ///
        /// <para>
        /// ⚠️ 벽은 <c>BoxCollider2D</c>가 <c>transform.scale</c>로 늘어나 있었다. 타일 렌더링은
        /// scale이 아니라 <c>SpriteRenderer.size</c>로 크기를 잡아야 하므로 scale을 1로 되돌리는데,
        /// <b>그대로 두면 벽 충돌이 사라진다</b>. 그래서 콜라이더 크기에 기존 scale을 곱해 옮겨 담아
        /// 월드 기준 충돌 범위가 정확히 같게 유지한다 (밸런스·물리 무변경).
        /// </para>
        /// </summary>
        private static bool BindArena()
        {
            Sprite floor = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITES + "Arena/Floors1.png");
            Sprite block = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITES + "Arena/BlockTile1.png");
            if (floor == null || block == null)
            {
                Debug.LogError("[SpriteBinding] 아레나 타일 스프라이트 없음");
                return false;
            }

            GameObject background = GameObject.Find("Arena/Background");
            if (background == null)
            {
                Debug.LogError("[SpriteBinding] Arena/Background 없음");
                return false;
            }

            SpriteRenderer bg = background.GetComponent<SpriteRenderer>();
            Vector2 arenaSize = new Vector2(
                background.transform.localScale.x,
                background.transform.localScale.y);
            if (bg.drawMode == SpriteDrawMode.Tiled) arenaSize = bg.size;   // 재실행 시 이미 옮겨 담긴 상태

            bg.sprite = floor;
            bg.drawMode = SpriteDrawMode.Tiled;
            bg.tileMode = SpriteTileMode.Continuous;
            bg.size = arenaSize;
            bg.color = FLOOR_TINT;
            background.transform.localScale = Vector3.one;   // 벽과 같은 이유로 마지막에 (아래 주석 참조)

            GameObject wallsRoot = GameObject.Find("Arena/Walls");
            int wallCount = 0;
            if (wallsRoot != null)
            {
                for (int i = 0; i < wallsRoot.transform.childCount; i++)
                {
                    Transform wall = wallsRoot.transform.GetChild(i);
                    SpriteRenderer renderer = wall.GetComponent<SpriteRenderer>();
                    BoxCollider2D collider = wall.GetComponent<BoxCollider2D>();
                    if (renderer == null) continue;

                    // 재실행 판정: 이미 타일 모드면 콜라이더가 월드 크기를 들고 있다.
                    // 이 가드가 없으면 실행할 때마다 scale이 한 번 더 곱해져 벽이 26배씩 커진다.
                    bool alreadyMigrated = renderer.drawMode == SpriteDrawMode.Tiled;
                    Vector2 worldSize;
                    if (collider == null) worldSize = new Vector2(wall.localScale.x, wall.localScale.y);
                    else if (alreadyMigrated) worldSize = collider.size;
                    else worldSize = new Vector2(collider.size.x * wall.localScale.x, collider.size.y * wall.localScale.y);

                    if (collider != null) collider.size = worldSize;

                    renderer.sprite = block;
                    renderer.drawMode = SpriteDrawMode.Tiled;
                    renderer.tileMode = SpriteTileMode.Continuous;
                    renderer.size = worldSize;
                    renderer.color = WALL_TINT;

                    // scale 초기화는 반드시 마지막에. drawMode를 Tiled로 바꾸는 순간 Unity가
                    // 스프라이트 원본 크기(16px/PPU)를 트랜스폼 스케일에 얹어 버린다 — 먼저 1로 만들어 두면 덮어써진다.
                    wall.localScale = Vector3.one;
                    wallCount++;
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[SpriteBinding] 아레나 ← Floors1 타일 {arenaSize.x}×{arenaSize.y}u, 벽 {wallCount}면 ← BlockTile1");
            return true;
        }

        // ────────────────────────────────── UI

        /// <summary>
        /// HUD·패널·버튼에 UI 팩 프레임을 입힌다. 스프라이트는 회색조로 반입했고 색은 여기서 준다 —
        /// 원본이 갈색 나무 판타지 UI라 "AI 터미널" 정체성과 싸우기 때문이다. 무채색 위에 틴트를 얹으면
        /// 팔레트를 코드 한 곳에서 바꿀 수 있고, 나무 질감 그대로가 좋으면 원본 재복사만 하면 된다.
        /// 전면 패널의 어두운 배경은 손대지 않는다 — 전체 화면에 나무 상자를 까는 건 터미널 미학과 반대다.
        /// </summary>
        private static bool BindUi()
        {
            Sprite box = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITES + "UI/BGbox_01A.png");
            Sprite button = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITES + "UI/Button_01A_Normal.png");
            Sprite sliderBox = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITES + "UI/Slider01_Box.png");
            if (box == null || button == null || sliderBox == null)
            {
                Debug.LogError("[SpriteBinding] UI 스프라이트 없음");
                return false;
            }

            GameObject canvas = GameObject.Find("GameScreensCanvas");
            if (canvas == null)
            {
                Debug.LogError("[SpriteBinding] GameScreensCanvas 없음");
                return false;
            }

            int skinned = 0;
            skinned += SkinImage(canvas, "TitlePanel/StartButton", button, BUTTON_TINT, BUTTON_SLICE);
            skinned += SkinImage(canvas, "MajorSelectPanel/LiberalArtsButton", box, MAJOR_LIBERAL_ARTS, BOX_SLICE);
            skinned += SkinImage(canvas, "MajorSelectPanel/ScienceButton", box, MAJOR_SCIENCE, BOX_SLICE);
            skinned += SkinImage(canvas, "MajorSelectPanel/ArtsButton", box, MAJOR_ARTS, BOX_SLICE);
            skinned += SkinImage(canvas, "WaveIntervalPanel/NextWaveButton", button, BUTTON_TINT, BUTTON_SLICE);
            skinned += SkinImage(canvas, "WaveIntervalPanel/UpgradeCard0", box, CARD_TINT, BOX_SLICE);
            skinned += SkinImage(canvas, "WaveIntervalPanel/UpgradeCard1", box, CARD_TINT, BOX_SLICE);
            skinned += SkinImage(canvas, "WaveIntervalPanel/UpgradeCard2", box, CARD_TINT, BOX_SLICE);
            skinned += SkinImage(canvas, "WaveIntervalPanel/SubMajorCard0", box, CARD_TINT, BOX_SLICE);
            skinned += SkinImage(canvas, "WaveIntervalPanel/SubMajorCard1", box, CARD_TINT, BOX_SLICE);
            skinned += SkinImage(canvas, "WaveIntervalPanel/SubMajorCard2", box, CARD_TINT, BOX_SLICE);
            skinned += SkinImage(canvas, "ResultPanel/ToTitleButton", button, BUTTON_TINT, BUTTON_SLICE);
            skinned += SkinImage(canvas, "PausePanel/ResumeButton", button, BUTTON_TINT, BUTTON_SLICE);
            skinned += SkinImage(canvas, "PausePanel/ToTitleButton", button, BUTTON_TINT, BUTTON_SLICE);
            skinned += SkinImage(canvas, "HudPanel/AiMiniPanel/Content", box, PANEL_TINT, THIN_SLICE);
            skinned += SkinImage(canvas, "HudPanel/HpBar/Background", sliderBox, HPBAR_FRAME_TINT, THIN_SLICE);

            // HP 채움은 스프라이트를 쓰지 않는다. 팩의 Slider01_Bar01~08은 "채움 레벨"이 아니라
            // 48×16 캔버스 안에 2px짜리 막대만 그려진 <b>바 스타일 변형</b>이라, 세로로 늘려도
            // 막대는 얇게 남아 체력이 안 보인다. 프레임만 픽셀 아트로 두고 안쪽은 단색으로 채우는 편이
            // 터미널 미학에도 맞는다. 프레임 테두리(약 7px)를 덮지 않도록 여백도 다시 잡는다.
            RectTransform fill = canvas.transform.Find("HudPanel/HpBar/Fill") as RectTransform;
            if (fill != null)
            {
                var fillImage = fill.GetComponent<UnityEngine.UI.Image>();
                if (fillImage != null)
                {
                    fillImage.sprite = null;
                    fillImage.type = UnityEngine.UI.Image.Type.Simple;
                    fill.sizeDelta = new Vector2(-16f, -14f);
                    fill.anchoredPosition = new Vector2(8f, 0f);
                    skinned++;
                }
            }

            skinned += BindMajorIcons(canvas);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[SpriteBinding] UI 스킨 {skinned}건");
            return true;
        }

        /// <summary>전공 아이콘 3종을 HpBar에 물린다. 아이콘은 컬러 원본이라 전공색 틴트를 걷어낸다.</summary>
        private static int BindMajorIcons(GameObject canvas)
        {
            Transform hpBar = canvas.transform.Find("HudPanel/HpBar");
            if (hpBar == null) return 0;

            var bar = hpBar.GetComponent<Luddite.UI.HpBar>();
            if (bar == null) return 0;

            SerializedObject so = new SerializedObject(bar);
            so.FindProperty("_liberalArtsIcon").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(SPRITES + "Icons/Icon_248_Scroll.png");
            so.FindProperty("_scienceIcon").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(SPRITES + "Icons/Icon_235_Equal.png");
            so.FindProperty("_artsIcon").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(SPRITES + "Icons/Icon_261_Brush.png");
            so.ApplyModifiedPropertiesWithoutUndo();

            // HpBar는 플레이 모드에서만 아이콘을 갈아 끼운다. 에디터에서 흰 사각형으로 보이면
            // 배선이 안 된 것처럼 읽히므로, 기본값(문과)을 미리 넣어 둔다.
            Transform icon = hpBar.Find("MajorIcon");
            if (icon != null)
            {
                var iconImage = icon.GetComponent<UnityEngine.UI.Image>();
                if (iconImage != null && iconImage.sprite == null)
                {
                    iconImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITES + "Icons/Icon_248_Scroll.png");
                    iconImage.color = Color.white;
                }
            }
            return 1;
        }

        /// <summary>
        /// 9슬라이스로 스킨을 입힌다.
        ///
        /// <para>
        /// <paramref name="pixelsPerUnitMultiplier"/>는 <b>클수록 테두리가 얇아진다</b> —
        /// 화면상 테두리 두께 ≈ <c>(테두리px ÷ (스프라이트PPU × 배수)) × 캔버스 referencePixelsPerUnit</c>.
        /// UI 스프라이트를 PPU 16으로 반입했고 캔버스 기준이 100이므로,
        /// 16px 테두리 · 배수 2.5 → 화면 40px가 된다. 여기를 작게 주면 테두리가 위젯보다 커져
        /// 9슬라이스가 무너지고 박스가 찌그러진 팔각형처럼 뭉친다.
        /// </para>
        /// </summary>
        private static int SkinImage(GameObject root, string path, Sprite sprite, Color tint, float pixelsPerUnitMultiplier)
        {
            Transform target = root.transform.Find(path);
            if (target == null)
            {
                Debug.LogWarning($"[SpriteBinding] UI 경로 없음 — {path}");
                return 0;
            }

            var image = target.GetComponent<UnityEngine.UI.Image>();
            if (image == null) return 0;

            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            image.color = tint;
            return 1;
        }

        // ────────────────────────────────── 연출 팔레트 (한 곳에서 조정)

        // 9슬라이스 테두리 배수 — 클수록 얇다 (SkinImage 주석의 계산식 참조)
        private const float BOX_SLICE = 2.5f;      // 큰 박스: 화면 40px 테두리
        private const float BUTTON_SLICE = 1.5f;   // 버튼: 가로 33px / 세로 17px
        private const float THIN_SLICE = 3.5f;     // HP 바처럼 높이 28px밖에 안 되는 위젯

        private static readonly Color FLOOR_TINT = new Color(0.26f, 0.28f, 0.34f, 1f);
        private static readonly Color WALL_TINT = new Color(0.42f, 0.44f, 0.52f, 1f);
        private static readonly Color PANEL_TINT = new Color(0.16f, 0.17f, 0.22f, 0.94f);
        private static readonly Color HPBAR_FRAME_TINT = new Color(0.58f, 0.60f, 0.68f, 1f);
        private static readonly Color CARD_TINT = new Color(0.22f, 0.23f, 0.30f, 1f);
        private static readonly Color BUTTON_TINT = new Color(0.34f, 0.36f, 0.44f, 1f);
        private static readonly Color MAJOR_LIBERAL_ARTS = new Color(0.30f, 0.48f, 0.90f, 1f);
        private static readonly Color MAJOR_SCIENCE = new Color(0.32f, 0.72f, 0.42f, 1f);
        private static readonly Color MAJOR_ARTS = new Color(0.88f, 0.74f, 0.32f, 1f);

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
