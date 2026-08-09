using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Luddite.Core;

namespace Luddite.UI
{
    /// <summary>
    /// 보스 P2 전환 연출 (GDD §9): <c>USER MODEL LOADED → COPY COMPLETE → PATTERN: YOU</c>
    /// 3줄이 전환 무적 3초에 맞춰 순차 표기된다. <see cref="PredictionFailedOverlay"/>와 같은 구조 —
    /// 순수 비주얼만, 판단 없음, 전 구간 unscaled 시간.
    ///
    /// <para>문구는 AI 시스템 발화 = 영어 대문자 터미널체 (§10.5). 색은 마젠타 —
    /// "AI가 나를 읽은 결과"의 선언이므로 §10.4의 정확한 용례다.</para>
    /// </summary>
    public class BossPhaseOverlay : MonoBehaviour
    {
        private static readonly string[] LINES = { "USER MODEL LOADED", "COPY COMPLETE", "PATTERN: YOU" };

        [Tooltip("표시/숨김 대상 루트 (자식). 컴포넌트 자신을 끄면 다시 못 켠다")]
        [SerializeField] private GameObject _content;

        [SerializeField] private TMP_Text _mainText;

        [Header("연출값 (전환 무적 3초에 맞춘다)")]
        [Tooltip("줄당 표시 시간(초). 3줄 × 1초 = 전환 무적 3초와 정렬")]
        [SerializeField] private float _lineDuration = 1f;

        [Tooltip("마지막 줄(PATTERN: YOU)을 추가로 유지하는 시간(초)")]
        [SerializeField] private float _finalHold = 0.6f;

        private float _elapsed;
        private bool _playing;

        private void OnEnable() => GameEvents.BossPhaseTwoStarted += OnPhaseTwoStarted;

        private void OnDisable()
        {
            GameEvents.BossPhaseTwoStarted -= OnPhaseTwoStarted;
            StopOverlay(); // HUD가 꺼질 때(상태 이탈) 잔상이 남지 않게
        }

        private void OnPhaseTwoStarted()
        {
            _elapsed = 0f;
            _playing = true;
            if (_content != null) _content.SetActive(true);
            UpdateVisuals();
        }

        private void Update()
        {
            if (!_playing) return;

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= _lineDuration * LINES.Length + _finalHold)
            {
                StopOverlay();
                return;
            }

            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (_mainText == null) return;

            int index = Mathf.Min((int)(_elapsed / Mathf.Max(_lineDuration, 1e-4f)), LINES.Length - 1);
            _mainText.text = LINES[index];

            // 줄 전환 직후 살짝 밝게 — 타자기 없이도 "단계가 넘어간다"가 읽히게
            float lineElapsed = _elapsed - index * _lineDuration;
            float brightness = Mathf.Lerp(1f, 0.75f, Mathf.Clamp01(lineElapsed / 0.3f));
            _mainText.color = new Color(1f, 0.35f * brightness + 0.1f, 1f, 1f);
        }

        private void StopOverlay()
        {
            _playing = false;
            if (_content != null) _content.SetActive(false);
        }
    }
}
