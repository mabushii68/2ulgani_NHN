using System;
using UnityEngine;

namespace Luddite.Core
{
    /// <summary>
    /// 4방향 스프라이트 시트 애니메이터 (뷰 전용 — 게임플레이 상태를 읽기만 하고 절대 쓰지 않는다).
    ///
    /// <para>
    /// 반입한 Franuka 시트는 세로 4행이 곧 4방향이고, 행 순서는 D3 세션 5에 대조 시트로 확정했다:
    /// <c>row0 = 아래 / row1 = 왼쪽 / row2 = 오른쪽 / row3 = 위</c>. 이 규약은 모든 캐릭터·적 시트에 공통이다.
    /// </para>
    ///
    /// <para>
    /// Unity Animator를 쓰지 않는 이유: 4방향 × 클립 수만큼 상태·블렌드 트리를 손으로 엮어야 하는데,
    /// 실제로 필요한 것은 "행 선택 + 프레임 넘기기"뿐이다. 코드로 결정론적으로 재생성할 수 있고
    /// (<c>SpriteBindingSetupTools</c>), WebGL 빌드에 Animator 상태 머신을 싣지 않아도 된다.
    /// </para>
    ///
    /// <para>
    /// <see cref="_autoDriveFromBody"/>가 켜져 있으면 Rigidbody2D 속도만 보고 방향과 idle/walk를 스스로 정한다 —
    /// 적 5종은 이것만으로 배선이 끝나고 FSM 코드를 건드릴 필요가 없다.
    /// 조준 방향으로 돌아야 하는 플레이어는 이 모드를 끄고 <see cref="Player.PlayerSpriteView"/>가 몰아준다.
    /// </para>
    /// </summary>
    public class DirectionalSpriteAnimator : MonoBehaviour
    {
        public const int ROW_DOWN = 0;
        public const int ROW_LEFT = 1;
        public const int ROW_RIGHT = 2;
        public const int ROW_UP = 3;
        public const int ROW_COUNT = 4;

        /// <summary>방향 1개분 프레임 열. 배열의 배열은 Unity가 직렬화하지 못하므로 한 겹 감싼다.</summary>
        [Serializable]
        public class DirectionRow
        {
            [SerializeField] private Sprite[] _frames;

            public Sprite[] Frames => _frames;

            public void SetFrames(Sprite[] frames) => _frames = frames;
        }

        /// <summary>이름으로 재생하는 클립 1개. <see cref="Rows"/>는 항상 4칸(방향)이다.</summary>
        [Serializable]
        public class Clip
        {
            [SerializeField] private string _name;
            [SerializeField] private DirectionRow[] _rows;
            [SerializeField] private float _fps = 8f;
            [SerializeField] private bool _loop = true;

            public string Name => _name;
            public DirectionRow[] Rows => _rows;
            public float Fps => _fps;
            public bool Loop => _loop;

            public void Set(string name, DirectionRow[] rows, float fps, bool loop)
            {
                _name = name;
                _rows = rows;
                _fps = fps;
                _loop = loop;
            }
        }

        [Tooltip("그릴 대상. 비워 두면 자식에서 찾는다")]
        [SerializeField] private SpriteRenderer _renderer;

        [SerializeField] private Clip[] _clips;

        [Tooltip("시작 시 재생할 클립 이름")]
        [SerializeField] private string _defaultClip = CLIP_IDLE;

        [Header("자동 구동 (적 5종이 쓰는 모드)")]
        [Tooltip("켜면 Rigidbody2D 속도로 방향과 idle/walk를 스스로 정한다. 플레이어는 끄고 PlayerSpriteView가 몰아준다")]
        [SerializeField] private bool _autoDriveFromBody = true;

        [Tooltip("이 속도(유닛/초) 미만이면 정지로 보고 idle을 재생한다. 연출값")]
        [SerializeField] private float _moveThreshold = 0.05f;

        [Tooltip("멈춰 있을 때 이 트랜스폼의 +X 방향을 본다. 적의 AimPivot을 물리면 몸이 조준 방향으로 돈다. 비워도 무방")]
        [SerializeField] private Transform _facingSource;

        public const string CLIP_IDLE = "idle";
        public const string CLIP_WALK = "walk";

        private Rigidbody2D _body;
        private Clip _current;
        private int _row = ROW_DOWN;
        private int _frame;
        private float _timer;

        /// <summary>현재 재생 중인 클립 이름. 스모크 테스트·디버그용.</summary>
        public string CurrentClip => _current != null ? _current.Name : string.Empty;

        /// <summary>현재 방향 행. <c>ROW_*</c> 상수와 대응한다.</summary>
        public int CurrentRow => _row;

        /// <summary>루프가 아닌 클립이 마지막 프레임에 도달했는지. 루프 클립은 항상 false.</summary>
        public bool IsFinished { get; private set; }

        private void Awake()
        {
            if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>();
            _body = GetComponent<Rigidbody2D>();

            if (_renderer == null) Debug.LogError($"[DirectionalSpriteAnimator] SpriteRenderer를 찾지 못함 — {name}", this);
        }

        private void Start() => Play(_defaultClip, restart: true);

        private void Update()
        {
            if (_autoDriveFromBody) DriveFromBody();
            Advance(Time.deltaTime);
        }

        /// <summary>
        /// 진행 방향으로 방향 행을 정하고, 움직이면 walk / 멈추면 idle.
        ///
        /// <para>
        /// 멈춰 있을 때 <see cref="_facingSource"/>(적의 AimPivot)를 보는 것이 중요하다 —
        /// 챗봇·그림봇은 사거리에 들어오면 멈춰서 쏘는데, 그때 몸이 마지막 이동 방향을 보고 있으면
        /// <b>어디로 쏘는지가 화면에서 사라진다</b>. FSM은 이미 AimPivot을 표적 쪽으로 돌리고 있으므로
        /// 그 방향을 빌려 오면 적 코드를 건드리지 않고 조준 텔레그래프가 생긴다.
        /// </para>
        /// </summary>
        private void DriveFromBody()
        {
            if (_body == null) return;

            Vector2 velocity = _body.linearVelocity;
            bool moving = velocity.magnitude >= _moveThreshold;

            if (moving) SetFacing(velocity);
            else if (_facingSource != null) SetFacing(_facingSource.right);

            Play(moving ? CLIP_WALK : CLIP_IDLE);
        }

        /// <summary>
        /// 방향 벡터를 4방향 행으로 스냅한다. 가로 성분이 더 크면 좌우, 아니면 상하 —
        /// 대각선에서 좌우를 우선하는 이유는 스프라이트의 좌우 실루엣이 상하보다 구분이 잘 되기 때문이다.
        /// </summary>
        public void SetFacing(Vector2 direction)
        {
            if (direction.sqrMagnitude < 1e-6f) return;

            int row = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
                ? (direction.x >= 0f ? ROW_RIGHT : ROW_LEFT)
                : (direction.y >= 0f ? ROW_UP : ROW_DOWN);

            SetRow(row);
        }

        /// <summary>방향 행을 직접 지정. 클립 재생 위치(프레임)는 유지된다 — 돌아설 때 애니메이션이 튀지 않는다.</summary>
        public void SetRow(int row)
        {
            if (row < 0 || row >= ROW_COUNT || row == _row) return;

            _row = row;
            ApplyFrame();
        }

        /// <summary>
        /// 클립 재생. 이미 같은 클립을 재생 중이면 아무것도 하지 않는다(<paramref name="restart"/>가 참이면 처음으로).
        /// 없는 이름을 넘기면 현재 클립을 유지한다 — 배선이 덜 된 프리팹이 갑자기 안 보이게 되는 것보다 낫다.
        /// </summary>
        public void Play(string clipName, bool restart = false)
        {
            if (string.IsNullOrEmpty(clipName)) return;
            if (_current != null && _current.Name == clipName && !restart) return;

            Clip found = FindClip(clipName);
            if (found == null) return;

            _current = found;
            _frame = 0;
            _timer = 0f;
            IsFinished = false;
            ApplyFrame();
        }

        private Clip FindClip(string clipName)
        {
            if (_clips == null) return null;

            for (int i = 0; i < _clips.Length; i++)
            {
                if (_clips[i] != null && _clips[i].Name == clipName) return _clips[i];
            }
            return null;
        }

        private void Advance(float deltaTime)
        {
            if (_current == null || IsFinished) return;

            Sprite[] frames = CurrentFrames();
            if (frames == null || frames.Length <= 1) return;

            float fps = Mathf.Max(_current.Fps, 0.01f);
            _timer += deltaTime;

            float frameDuration = 1f / fps;
            while (_timer >= frameDuration)
            {
                _timer -= frameDuration;
                _frame++;

                if (_frame < frames.Length) continue;

                if (_current.Loop)
                {
                    _frame = 0;
                    continue;
                }

                _frame = frames.Length - 1;
                IsFinished = true;
                break;
            }

            ApplyFrame();
        }

        private Sprite[] CurrentFrames()
        {
            if (_current == null) return null;

            DirectionRow[] rows = _current.Rows;
            if (rows == null || _row >= rows.Length || rows[_row] == null) return null;

            return rows[_row].Frames;
        }

        private void ApplyFrame()
        {
            if (_renderer == null) return;

            Sprite[] frames = CurrentFrames();
            if (frames == null || frames.Length == 0) return;

            _renderer.sprite = frames[Mathf.Clamp(_frame, 0, frames.Length - 1)];
        }
    }
}
