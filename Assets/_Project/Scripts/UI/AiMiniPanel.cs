using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Luddite.AIBrain;
using Luddite.Core;
using Luddite.Enemies;

namespace Luddite.UI
{
    /// <summary>
    /// HUD 우상단 AI 미니 패널 (GDD §10.1): 우세 방향 + 확률 + 신뢰도 1줄.
    /// <b>엘리트 생존 시만</b> 표시 — 예측탄을 쏘는 주체가 화면에 있을 때만 "읽히고 있다"를 알린다.
    /// LOW→HIGH 전환 시 펄스 = "지금부터 예측탄 온다"는 신호. 표본 미달이면 <c>AI MODEL: LEARNING...</c>
    ///
    /// <para>AIBrain 데이터는 <b>읽기만</b> 한다 (규칙 7). 이 컴포넌트는 모델을 절대 만지지 않는다.</para>
    /// <para>TODO(D5): 보스도 예측탄을 쓰므로(§5.1) 보스 생존 시에도 표시하도록 조건 확장.</para>
    /// </summary>
    public class AiMiniPanel : MonoBehaviour
    {
        [SerializeField] private AIBrainRunner _brain;

        [Tooltip("표시/숨김 대상 루트. 이 컴포넌트 자신을 끄면 다시 켤 수 없으므로 자식을 끈다")]
        [SerializeField] private GameObject _content;

        [SerializeField] private TMP_Text _label;

        [Tooltip("패널 배경 — HIGH일 때 강조색으로 바뀐다")]
        [SerializeField] private Image _background;

        [Header("연출값")]
        [Tooltip("HIGH CONFIDENCE 강조색. 마젠타 = AI가 나를 읽는 것 (🔴 §10.4)")]
        [SerializeField] private Color _highAccent = new Color(0.75f, 0.1f, 0.75f, 0.85f);

        [Tooltip("평상시 배경색")]
        [SerializeField] private Color _normalBackground = new Color(0f, 0f, 0f, 0.55f);

        [Tooltip("LOW→HIGH 펄스 시간(초)")]
        [SerializeField] private float _pulseDuration = 0.35f;

        [Tooltip("펄스 시작 배율")]
        [SerializeField] private float _pulseScale = 1.3f;

        private bool _wasHigh;
        private float _pulseRemaining;

        private void Update()
        {
            bool visible = EliteModifier.ActiveCount > 0 && _brain != null && _brain.IsReady;
            if (_content != null && _content.activeSelf != visible) _content.SetActive(visible);

            if (!visible)
            {
                _wasHigh = false;
                _pulseRemaining = 0f;
                return;
            }

            bool isHigh = _brain.IsHighConfidence;
            if (isHigh && !_wasHigh) _pulseRemaining = _pulseDuration; // §10.1: "지금부터 예측탄 온다"
            _wasHigh = isHigh;

            UpdateLabel(isHigh);
            TickPulse();
        }

        private void UpdateLabel(bool isHigh)
        {
            if (_label != null)
            {
                // TODO(D3 폰트): 한글 폰트 반입 후 §10.5 한국어 요약 병기 검토. ● 글리프도 폰트에 따라 교체
                _label.text = _brain.ValidSamples < _brain.RequiredSamples
                    ? "AI MODEL: LEARNING..."
                    : $"AI READS: {DirectionLabel(_brain.DominantDirection)} " +
                      $"{_brain.DominantProbability:P0} [{(isHigh ? "HIGH" : "LOW")}]";
            }

            if (_background != null) _background.color = isHigh ? _highAccent : _normalBackground;
        }

        private void TickPulse()
        {
            if (_pulseRemaining <= 0f)
            {
                transform.localScale = Vector3.one;
                return;
            }

            _pulseRemaining -= Time.unscaledDeltaTime; // 일시정지 직후에도 자연스럽게 끝나도록 unscaled
            float progress = Mathf.Clamp01(1f - _pulseRemaining / Mathf.Max(_pulseDuration, 1e-4f));
            transform.localScale = Vector3.one * Mathf.Lerp(_pulseScale, 1f, progress);
        }

        private static string DirectionLabel(DodgeDirection direction) =>
            direction == DodgeDirection.Left ? "LEFT" : "RIGHT";
    }
}
