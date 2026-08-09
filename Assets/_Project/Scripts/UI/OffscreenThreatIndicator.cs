using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Luddite.Enemies;

namespace Luddite.UI
{
    /// <summary>
    /// 화면 밖 위협 보정 ① — 가장자리 방향 화살표 (개정안 §3, 🔴 추적 카메라의 조건부 동반 계약).
    /// 화면 밖에 살아 있는 적이 있으면 화면 가장자리에 그 방향 화살표를 띄운다.
    /// <b>엘리트·보스(EliteModifier 보유) = 마젠타, 일반 적 = 무채색</b> — §10.4 색 위계 그대로.
    ///
    /// <para>보정 ②(조준선 전체 렌더)는 LineRenderer가 월드 스페이스라 이미 충족 (개정안 §3).
    /// 방(32×18)이 화면(26.67×15u)보다 커진 D5 이후 실제로 유효하다.</para>
    ///
    /// <para>HUD 패널 자식이라 Combat에서만 보인다. 화살표 풀은 Awake에서 생성 — 프레임 할당 0.</para>
    /// </summary>
    public class OffscreenThreatIndicator : MonoBehaviour
    {
        [Tooltip("화살표 스프라이트 (오른쪽을 가리키는 기준 — 조준 표식과 같은 AimArrow 재사용)")]
        [SerializeField] private Sprite _arrowSprite;

        [Tooltip("동시 표시 상한. 동시 생존 상한(10)보다 약간 크게")]
        [SerializeField] private int _maxArrows = 12;

        [Tooltip("화면 가장자리에서 안쪽으로 띄우는 여백(px, 기준 해상도)")]
        [SerializeField] private float _edgeMargin = 48f;

        [SerializeField] private Vector2 _arrowSize = new Vector2(36f, 36f);

        [Tooltip("일반 적 — 무채색 (§10.4)")]
        [SerializeField] private Color _normalColor = new Color(0.75f, 0.75f, 0.78f, 0.8f);

        [Tooltip("엘리트·보스 — 마젠타 = AI 위협 (🔴 §10.4)")]
        [SerializeField] private Color _aiThreatColor = new Color(1f, 0.1f, 1f, 0.95f);

        private readonly List<Image> _pool = new List<Image>(12);
        private RectTransform _rect;
        private Camera _camera;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();

            for (int i = 0; i < _maxArrows; i++)
            {
                GameObject arrow = new GameObject($"Arrow_{i}", typeof(RectTransform));
                arrow.transform.SetParent(transform, false);
                Image image = arrow.AddComponent<Image>();
                image.sprite = _arrowSprite;
                image.raycastTarget = false;   // HUD가 조준 클릭을 삼키면 안 된다 (기존 HUD 규칙)
                RectTransform rect = (RectTransform)arrow.transform;
                rect.sizeDelta = _arrowSize;
                arrow.SetActive(false);
                _pool.Add(image);
            }
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null || _rect == null) { HideFrom(0); return; }

            Vector2 half = _rect.rect.size * 0.5f;
            int used = 0;

            IReadOnlyList<EnemyBase> enemies = EnemyBase.Active;
            for (int i = 0; i < enemies.Count && used < _pool.Count; i++)
            {
                EnemyBase enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;

                Vector3 viewport = _camera.WorldToViewportPoint(enemy.transform.position);
                bool onScreen = viewport.z > 0f
                    && viewport.x >= 0f && viewport.x <= 1f
                    && viewport.y >= 0f && viewport.y <= 1f;
                if (onScreen) continue;

                // 화면 중심 기준 방향 (뒤쪽 z<0이면 투영이 반전되므로 뒤집어 준다)
                Vector2 centered = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
                if (viewport.z < 0f) centered = -centered;
                if (centered.sqrMagnitude < 1e-8f) continue;

                // 가장자리 사각형(여백 포함)에 클램프 — 방향 유지한 채 가장 가까운 변 위 점
                Vector2 limit = new Vector2(half.x - _edgeMargin, half.y - _edgeMargin);
                Vector2 position = centered * Mathf.Min(
                    limit.x / Mathf.Max(Mathf.Abs(centered.x), 1e-5f),
                    limit.y / Mathf.Max(Mathf.Abs(centered.y), 1e-5f));

                Image arrow = _pool[used++];
                if (!arrow.gameObject.activeSelf) arrow.gameObject.SetActive(true);

                RectTransform rect = (RectTransform)arrow.transform;
                rect.anchoredPosition = position;
                rect.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(centered.y, centered.x) * Mathf.Rad2Deg);

                bool aiThreat = enemy is BossLLM || enemy.GetComponent<EliteModifier>() != null;
                arrow.color = aiThreat ? _aiThreatColor : _normalColor;
            }

            HideFrom(used);
        }

        private void HideFrom(int index)
        {
            for (int i = index; i < _pool.Count; i++)
            {
                if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
            }
        }
    }
}
