using UnityEngine;

namespace Luddite.Core
{
    /// <summary>
    /// 방 클리어 보상 상자 — <b>WaveInterval 정지 화면의 대체 연출</b> (개정안 §4).
    ///
    /// <para><b>내부 시스템은 전부 재사용한다.</b> 상자는 <see cref="GameManager.BeginWaveInterval"/>를
    /// 호출할 뿐이고, 3택 카드·TARGET PROFILE·COUNTER PROTOCOL 패널은 D4의 것을 그대로 쓴다.
    /// 감쇠(×0.8)·프로파일 스냅숏·DDA 판정은 이미 <c>WaveEnded</c> 시점(=방 클리어)에 끝나 있으므로
    /// <b>AIBrain은 이 파일을 몰라도 된다</b> — 개정안 §4의 "AIBrain 무변경" 요구가 여기서 성립한다.</para>
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Chest : MonoBehaviour
    {
        [Tooltip("닫힘 프레임 (Chest_*_0)")]
        [SerializeField] private Sprite _closedSprite;

        [Tooltip("열림 프레임 (Chest_*_3)")]
        [SerializeField] private Sprite _openSprite;

        [Tooltip("인터랙트 가능 반경. DungeonConfig가 덮어쓴다")]
        [SerializeField] private float _interactRadius = 1.2f;

        [Tooltip("false면 반경 안에서 상호작용 키를 눌러야 열린다. DungeonConfig가 덮어쓴다")]
        [SerializeField] private bool _autoOpen = true;

        [Tooltip("상호작용 키. 구 Input Manager API 사용 (환경 규칙)")]
        [SerializeField] private KeyCode _interactKey = KeyCode.E;

        [Tooltip("반경 안에 들어오면 켜지는 '[E] 열기' 안내. 없어도 동작한다")]
        [SerializeField] private GameObject _prompt;

        [Header("연출값 — 열릴 때 살짝 튀는 팝")]
        [Tooltip("팝 지속 시간(초). unscaled — 인터벌 진입으로 timeScale이 0이 되어도 재생된다")]
        [SerializeField] private float _popDuration = 0.22f;

        [Tooltip("팝 최대 배율 (기준 스케일 대비)")]
        [SerializeField] private float _popScale = 1.25f;

        private SpriteRenderer _renderer;
        private Transform _player;
        private bool _opened;
        private bool _armed;
        private Vector3 _baseScale;
        private float _popElapsed = -1f;

        /// <summary>상자를 열면 호출된다. DungeonManager가 인터벌 패널로 연결한다.</summary>
        public event System.Action Opened;

        public bool IsOpened => _opened;

        public void Configure(bool autoOpen, float interactRadius, Transform player)
        {
            _autoOpen = autoOpen;
            _interactRadius = interactRadius;
            _player = player;
        }

        /// <summary>방 클리어 시 DungeonManager가 켠다. 켜지기 전에는 열리지 않는다.</summary>
        public void Arm()
        {
            _armed = true;
            _opened = false;
            _popElapsed = -1f;
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_closedSprite != null) _renderer.sprite = _closedSprite;
            gameObject.SetActive(true);
            if (_baseScale != Vector3.zero) transform.localScale = _baseScale;   // 팝 도중 리셋 대비
            if (_autoOpen) Open();
        }

        /// <summary>재플레이(RunStarted) 리셋.</summary>
        public void Disarm()
        {
            _armed = false;
            _opened = false;
            gameObject.SetActive(false);
        }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
            SetPrompt(false);
        }

        private void Update()
        {
            TickPop();

            if (!_armed || _opened || _autoOpen) { SetPrompt(false); return; }
            if (_player == null) return;

            // timeScale 0(인터벌·일시정지)에서는 Update가 돌아도 이 분기는 이미 _opened로 막힌다
            float sqr = ((Vector2)_player.position - (Vector2)transform.position).sqrMagnitude;
            bool inRange = sqr <= _interactRadius * _interactRadius;
            SetPrompt(inRange);

            // 자동 오픈 폐기 (D7, 사람 요청): 다가가는 것만으로는 열리지 않고 키를 눌러야 한다.
            // 방을 클리어하고도 잠깐 숨을 돌릴 수 있어 인터벌 진입 시점을 플레이어가 쥔다.
            if (inRange && Input.GetKeyDown(_interactKey)) Open();
        }

        private void SetPrompt(bool on)
        {
            if (_prompt != null && _prompt.activeSelf != on) _prompt.SetActive(on);
        }

        /// <summary>
        /// 열릴 때 한 번 튀는 팝. <b>unscaled</b>로 도는 것이 핵심 — <see cref="Open"/>이
        /// 인터벌(timeScale 0)을 띄우므로 scaled 시간으로 만들면 애니메이션이 그 자리에서 멈춘다.
        /// </summary>
        private void TickPop()
        {
            if (_popElapsed < 0f) return;
            _popElapsed += Time.unscaledDeltaTime;
            float t = _popDuration > 0f ? Mathf.Clamp01(_popElapsed / _popDuration) : 1f;
            // 0 → 1 → 0 산 모양으로 부풀었다 돌아온다
            float bulge = Mathf.Sin(t * Mathf.PI);
            transform.localScale = _baseScale * (1f + (_popScale - 1f) * bulge);
            if (t >= 1f)
            {
                transform.localScale = _baseScale;
                _popElapsed = -1f;
            }
        }

        private void Open()
        {
            if (_opened) return;
            _opened = true;
            SetPrompt(false);
            if (_openSprite != null && _renderer != null) _renderer.sprite = _openSprite;
            _popElapsed = 0f;
            AudioDirector.Play(GameSfx.UiButton);   // 씬에 AudioDirector 없으면 무음 no-op
            if (Opened != null) Opened();
        }
    }
}
