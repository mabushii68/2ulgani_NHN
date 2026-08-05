using UnityEngine;
using UnityEngine.UI;
using Luddite.Core;
using Luddite.Player;

namespace Luddite.UI
{
    /// <summary>
    /// HUD 좌하단 HP 바 + 전공 아이콘 (GDD §10.1). 데이터는 읽기만 한다.
    /// 업그레이드 아이콘열은 D4(업그레이드 시스템)에서 추가.
    /// </summary>
    public class HpBar : MonoBehaviour
    {
        [SerializeField] private PlayerHealth _health;
        [SerializeField] private GameManager _gameManager;

        [Tooltip("체력 비율만큼 가로로 스케일되는 이미지 (pivot X = 0)")]
        [SerializeField] private RectTransform _fill;

        [Tooltip("선택 전공색으로 칠할 아이콘 (§4.1: 문과 파랑 / 이과 초록 / 예체능 노랑)")]
        [SerializeField] private Image _majorIcon;

        [Header("연출값 — 전공색 (§4.1)")]
        [SerializeField] private Color _liberalArtsColor = new Color(0.30f, 0.55f, 1f, 1f);
        [SerializeField] private Color _scienceColor = new Color(0.35f, 0.85f, 0.45f, 1f);
        [SerializeField] private Color _artsColor = new Color(1f, 0.85f, 0.35f, 1f);

        private void Update()
        {
            if (_health != null && _fill != null)
            {
                Vector3 scale = _fill.localScale;
                scale.x = _health.HpRatio;
                _fill.localScale = scale;
            }

            if (_majorIcon != null && _gameManager != null)
                _majorIcon.color = MajorColor(_gameManager.SelectedMajor);
        }

        private Color MajorColor(Major major)
        {
            switch (major)
            {
                case Major.Science: return _scienceColor;
                case Major.Arts: return _artsColor;
                default: return _liberalArtsColor;
            }
        }
    }
}
