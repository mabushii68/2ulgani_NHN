using UnityEngine;
using Luddite.Combat;
using Luddite.Core;
using Luddite.Data;

namespace Luddite.Player
{
    /// <summary>
    /// 플레이어 이동·조준·발사 (GDD §3.1). 입력은 구 Input Manager를 사용한다.
    /// 공격 로직은 <see cref="IWeapon"/> 구현 컴포넌트에 위임 — 강결합 금지 (CLAUDE.md 규칙 6).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        private const string AXIS_HORIZONTAL = "Horizontal";
        private const string AXIS_VERTICAL = "Vertical";
        private const int MOUSE_BUTTON_FIRE = 0;

        /// <summary>커서가 플레이어와 거의 겹칠 때 조준 방향이 튀는 것을 막는 최소 거리(유닛)</summary>
        private const float MIN_AIM_DISTANCE = 0.05f;

        [SerializeField] private PlayerStatsSO _stats;

        [Tooltip("조준 방향으로 회전시킬 표식(총구 등). 비워 두면 무시한다")]
        [SerializeField] private Transform _aimPivot;

        private Rigidbody2D _body;
        private Camera _camera;
        private IWeapon _weapon;
        private PlayerUpgrades _upgrades;

        private Vector2 _moveInput;
        private Vector2 _aimDirection = Vector2.right;
        private bool _fireHeld;

        /// <summary>
        /// Combat 상태에서만 입력을 받는다 — 타이틀 화면 클릭이 발사가 되면 안 된다.
        /// 기본 true인 이유: GameManager가 없는 테스트 씬에서도 단독 동작해야 하므로,
        /// 첫 상태 통지가 오기 전까지는 기존 동작을 유지한다.
        /// </summary>
        private bool _controlEnabled = true;

        /// <summary>현재 조준 방향(정규화). HUD·AIBrain이 읽는다.</summary>
        public Vector2 AimDirection => _aimDirection;

        /// <summary>이번 프레임 이동 입력(정규화). 무빙샷 비율 집계 입력 (GDD §6.4).</summary>
        public Vector2 MoveInput => _moveInput;

        /// <summary>발사 입력이 눌려 있는지. 무빙샷 비율 집계 입력 (GDD §6.4).</summary>
        public bool IsFiring => _fireHeld;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.gravityScale = 0f;
            _body.freezeRotation = true;

            _weapon = GetComponentInChildren<IWeapon>();
            _upgrades = GetComponent<PlayerUpgrades>();

            if (_stats == null) Debug.LogError("[PlayerController] PlayerStatsSO 미지정", this);
            if (_weapon == null) Debug.LogError("[PlayerController] IWeapon 구현 컴포넌트를 찾지 못함", this);
        }

        private void OnEnable() => GameEvents.GameStateChanged += OnGameStateChanged;

        private void OnDisable() => GameEvents.GameStateChanged -= OnGameStateChanged;

        private void Start()
        {
            _camera = Camera.main;
            if (_camera == null) Debug.LogError("[PlayerController] MainCamera 태그 카메라 없음 — 마우스 조준 불가", this);
        }

        private void OnGameStateChanged(GameState previous, GameState next)
        {
            _controlEnabled = next == GameState.Combat;

            // 잔류 입력 제거 — 일시정지 순간의 이동·발사가 재개 프레임에 이어지지 않게
            if (_controlEnabled) return;
            _moveInput = Vector2.zero;
            _fireHeld = false;
        }

        private void Update()
        {
            if (_controlEnabled)
            {
                ReadInput();
                UpdateAim();
            }

            if (_weapon == null) return;

            _weapon.Tick(Time.deltaTime);
            if (_fireHeld && _weapon.CanFire) _weapon.Fire(_body.position, _aimDirection);
        }

        private void FixedUpdate()
        {
            float speed = _stats != null ? _stats.MoveSpeed : 0f;
            if (_upgrades != null) speed *= _upgrades.MoveSpeedMultiplier;   // §8 #3 수강신청 올클
            _body.linearVelocity = _moveInput * speed;
        }

        private void ReadInput()
        {
            // GetAxisRaw = 스무딩 없는 즉시 반응. 관성이 끼면 회피 변위 판정(GDD §7.1)이 오염된다
            _moveInput = new Vector2(
                Input.GetAxisRaw(AXIS_HORIZONTAL),
                Input.GetAxisRaw(AXIS_VERTICAL));

            // 대각 이동이 빨라지지 않도록 정규화 (8방향 등속)
            if (_moveInput.sqrMagnitude > 1f) _moveInput.Normalize();

            _fireHeld = Input.GetMouseButton(MOUSE_BUTTON_FIRE);   // 단발 클릭 아님 — 홀드 자동 연사
        }

        private void UpdateAim()
        {
            if (_camera == null) return;

            Vector2 cursorWorld = _camera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 toCursor = cursorWorld - _body.position;
            if (toCursor.sqrMagnitude >= MIN_AIM_DISTANCE * MIN_AIM_DISTANCE) _aimDirection = toCursor.normalized;

            if (_aimPivot != null) _aimPivot.right = _aimDirection;
        }
    }
}
