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

        [Tooltip("접촉 인터랙트 반경. DungeonConfig가 덮어쓴다")]
        [SerializeField] private float _interactRadius = 1.2f;

        [Tooltip("false면 접촉으로 열린다. DungeonConfig가 덮어쓴다")]
        [SerializeField] private bool _autoOpen = true;

        private SpriteRenderer _renderer;
        private Transform _player;
        private bool _opened;
        private bool _armed;

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
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_closedSprite != null) _renderer.sprite = _closedSprite;
            gameObject.SetActive(true);
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
        }

        private void Update()
        {
            if (!_armed || _opened || _autoOpen) return;
            if (_player == null) return;
            // timeScale 0(인터벌·일시정지)에서는 Update가 돌아도 이 분기는 이미 _opened로 막힌다
            float sqr = ((Vector2)_player.position - (Vector2)transform.position).sqrMagnitude;
            if (sqr <= _interactRadius * _interactRadius) Open();
        }

        private void Open()
        {
            if (_opened) return;
            _opened = true;
            if (_openSprite != null && _renderer != null) _renderer.sprite = _openSprite;
            if (Opened != null) Opened();
        }
    }
}
