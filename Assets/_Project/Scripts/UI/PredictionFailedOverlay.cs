using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Luddite.AIBrain;
using Luddite.Core;

namespace Luddite.UI
{
    /// <summary>
    /// <c>PREDICTION FAILED</c> — 게임에서 가장 중요한 1초 (GDD §10.3).
    /// 화면 중앙 플래시 + 신뢰도 하락(<c>LEFT 82% → 64%</c>) + <c>MODEL UPDATING...</c>.
    /// 히트스톱은 timeScale 소유자인 <see cref="GameManager"/>가 같은 이벤트로 처리한다 —
    /// 이 컴포넌트는 순수 비주얼만. 전 구간 unscaled 시간 (히트스톱 중에도 흘러야 한다).
    ///
    /// <para>§10.3: 1초 이내 종료, 긴 컷신 금지. 보상 없음 (MVP — 연출+통계만).</para>
    /// <para>TODO(D6 오디오): 글리치 효과음. TODO(D3 폰트): 한글 병기 검토.</para>
    /// </summary>
    public class PredictionFailedOverlay : MonoBehaviour
    {
        [Tooltip("표시/숨김 대상 루트 (자식). 컴포넌트 자신을 끄면 다시 못 켠다")]
        [SerializeField] private GameObject _content;

        [Tooltip("화면 전체 플래시 이미지 — 알파만 조작한다")]
        [SerializeField] private Image _flash;

        [SerializeField] private TMP_Text _mainText;

        [Tooltip("신뢰도 하락 → MODEL UPDATING... 순서로 바뀌는 보조 줄")]
        [SerializeField] private TMP_Text _subText;

        [Header("연출값 (§10.3: 전체 1초 이내)")]
        [SerializeField] private float _duration = 0.9f;
        [SerializeField] private float _flashDuration = 0.18f;

        [Tooltip("신뢰도 하락 표시가 MODEL UPDATING...으로 바뀌는 시점(초)")]
        [SerializeField] private float _probabilityLineDuration = 0.55f;

        [SerializeField] private Color _flashColor = new Color(1f, 1f, 1f, 0.35f);

        private float _elapsed;
        private bool _playing;
        private PredictionFailedReport _report;

        private void OnEnable() => GameEvents.PredictionFailed += OnPredictionFailed;

        private void OnDisable()
        {
            GameEvents.PredictionFailed -= OnPredictionFailed;
            StopOverlay(); // HUD가 꺼질 때(상태 이탈) 잔상이 남지 않게
        }

        private void OnPredictionFailed(PredictionFailedReport report)
        {
            _report = report;
            _elapsed = 0f;
            _playing = true;

            if (_content != null) _content.SetActive(true);
            if (_mainText != null) _mainText.text = "PREDICTION FAILED";
            UpdateVisuals();
        }

        private void Update()
        {
            if (!_playing) return;

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= _duration)
            {
                StopOverlay();
                return;
            }

            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (_flash != null)
            {
                float flashProgress = _flashDuration > 0f ? Mathf.Clamp01(_elapsed / _flashDuration) : 1f;
                Color c = _flashColor;
                c.a = Mathf.Lerp(_flashColor.a, 0f, flashProgress);
                _flash.color = c;
            }

            if (_subText != null)
            {
                _subText.text = _elapsed < _probabilityLineDuration
                    ? $"{DirectionLabel(_report.DominantBefore)} {_report.ProbabilityBefore:P0} → {_report.ProbabilityAfter:P0}"
                    : "MODEL UPDATING...";
            }
        }

        private void StopOverlay()
        {
            _playing = false;
            if (_content != null) _content.SetActive(false);
        }

        private static string DirectionLabel(DodgeDirection direction) =>
            direction == DodgeDirection.Left ? "LEFT" : "RIGHT";
    }
}
