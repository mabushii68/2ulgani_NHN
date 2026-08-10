using UnityEngine;

namespace Luddite.UI
{
    /// <summary>
    /// 패널이 켜질 때 살짝 튀며 들어오는 팝 (D7 신규 — 상자를 열었을 때 인터벌 패널용).
    ///
    /// <para><b>반드시 unscaled 시간으로 돈다.</b> 인터벌·일시정지는 <c>timeScale = 0</c>이라
    /// scaled 시간으로 만들면 애니메이션이 첫 프레임에서 멈춘다 —
    /// 이 프로젝트에서 연출을 timeScale 0 구간에 넣을 때 반복해서 걸리는 함정이다.</para>
    ///
    /// <para>레이아웃에 영향을 주지 않도록 <see cref="RectTransform.localScale"/>만 만진다.
    /// 앵커·크기는 건드리지 않는다.</para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class PanelPopIn : MonoBehaviour
    {
        [Tooltip("팝 지속 시간(초, unscaled)")]
        [SerializeField] private float _duration = 0.18f;

        [Tooltip("시작 배율. 1보다 작으면 커지며 들어온다")]
        [SerializeField] private float _fromScale = 0.86f;

        [Tooltip("되튐 세기 — 목표를 살짝 넘었다가 돌아온다. 0이면 부드럽게만 커진다")]
        [Range(0f, 0.5f)] [SerializeField] private float _overshoot = 0.06f;

        private RectTransform _rect;
        private float _elapsed = -1f;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            if (_rect == null) _rect = GetComponent<RectTransform>();
            _rect.localScale = Vector3.one * _fromScale;
        }

        private void OnDisable()
        {
            // 다음에 켜질 때 이전 상태가 남지 않게 원복
            if (_rect != null) _rect.localScale = Vector3.one;
            _elapsed = -1f;
        }

        private void Update()
        {
            if (_elapsed < 0f) return;

            _elapsed += Time.unscaledDeltaTime;
            float t = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;

            // ease-out + 오버슛: 목표 배율을 살짝 넘겼다가 1로 수렴
            float eased = 1f - (1f - t) * (1f - t);
            float scale = Mathf.Lerp(_fromScale, 1f, eased) + _overshoot * Mathf.Sin(t * Mathf.PI);
            _rect.localScale = Vector3.one * scale;

            if (t >= 1f)
            {
                _rect.localScale = Vector3.one;
                _elapsed = -1f;
            }
        }
    }
}
