using UnityEngine;

namespace Luddite.Core
{
    /// <summary>
    /// 던전 체인의 방 1개 (개정안 §2). <b>로직은 갖지 않고 구성 요소와 진입 신호만 들고 있다</b> —
    /// 진행 판단은 <see cref="DungeonManager"/>가 단독 소유한다 (GameManager 책임 집중 금지와 같은 이유).
    ///
    /// <para>전투방은 기존 웨이브 N과 1:1이다. <see cref="ChainIndex"/> 0 = 시작방,
    /// 1..N = 전투방(웨이브 1..N), N+1 = 보스방.</para>
    /// </summary>
    public class Room : MonoBehaviour
    {
        [Tooltip("체인 위치. 0 = 시작방, 1..N = 전투방, 마지막 = 보스방")]
        [SerializeField] private int _chainIndex;

        [Tooltip("전투방만 웨이브를 돌린다. 시작방은 false")]
        [SerializeField] private bool _isCombatRoom = true;

        [Tooltip("들어온 쪽 문 (체인 앞쪽). 시작방은 비워 둔다")]
        [SerializeField] private Door _entryDoor;

        [Tooltip("나가는 쪽 문 (체인 뒤쪽). 보스방은 비워 둔다")]
        [SerializeField] private Door _exitDoor;

        [Tooltip("클리어 보상 상자. 시작방·보스방은 비워 둔다")]
        [SerializeField] private Chest _chest;

        [Header("카메라 바운드 (빌더가 채운다)")]
        [Tooltip("방 실루엣 바운딩 박스의 중심 — 방 로컬 오프셋. 비대칭 확장이 있는 방은 방 중심과 다르다")]
        [SerializeField] private Vector2 _camLocalCenter;

        [Tooltip("방 실루엣 바운딩 박스의 반경. (0,0)이면 DungeonConfig의 방 규격으로 폴백한다")]
        [SerializeField] private Vector2 _camHalfExtents;

        private bool _entered;

        /// <summary>플레이어가 이 방에 처음 들어왔을 때 1회. 인자는 체인 인덱스.</summary>
        public event System.Action<Room> PlayerEntered;

        /// <summary>
        /// 플레이어가 방 밖(=복도)으로 나갔을 때. 매번 발행한다 —
        /// 카메라 바운드를 복도 구간으로 넓히는 신호다 (방에 고정하면 복도에서 플레이어를 놓친다).
        /// </summary>
        public event System.Action<Room> PlayerExited;

        public int ChainIndex => _chainIndex;
        public bool IsCombatRoom => _isCombatRoom;
        public Door EntryDoor => _entryDoor;
        public Door ExitDoor => _exitDoor;
        public Chest Chest => _chest;
        public Vector2 Center => transform.position;

        /// <summary>
        /// 카메라가 클램프할 사각형의 중심. 방 실루엣이 비대칭이면 <see cref="Center"/>와 다르다 —
        /// 방 중심에 대칭 반경을 쓰면 확장이 없는 쪽으로 카메라가 넘어가 벽 밖 어둠을 비춘다.
        /// </summary>
        public Vector2 CameraCenter => (Vector2)transform.position + _camLocalCenter;

        /// <summary>카메라 클램프 반경. (0,0)이면 빌더가 안 채운 것이므로 호출부가 설정값으로 폴백한다.</summary>
        public Vector2 CameraHalfExtents => _camHalfExtents;

        /// <summary>이 방이 카메라 바운드를 자기 값으로 갖고 있는가 (아니면 DungeonConfig 폴백).</summary>
        public bool HasCameraBounds => _camHalfExtents.x > 0.01f && _camHalfExtents.y > 0.01f;

        /// <summary>웨이브 번호 = 체인 인덱스 (전투방 1 → 웨이브 1).</summary>
        public int WaveNumber => _chainIndex;

        /// <summary>
        /// 재플레이 리셋 — 문 상태 복구 + 상자 회수. 이게 없으면 2회차가 깨진다.
        ///
        /// <para><b>입구 문은 열어 둔다.</b> 락인은 "들어온 뒤에" 걸리는 것이지 처음부터 잠가 두는 게 아니다 —
        /// 초기 상태로 잠그면 플레이어가 방에 들어갈 수조차 없다. 나가는 문만 잠근다(전투방에 한해).
        /// 시작방처럼 전투가 없는 방은 나가는 문도 열어 둔다.</para>
        /// </summary>
        public void ResetRoom()
        {
            _entered = false;
            if (_entryDoor != null) _entryDoor.Unlock();
            if (_exitDoor != null)
            {
                if (_isCombatRoom) _exitDoor.Lock();
                else _exitDoor.Unlock();
            }
            if (_chest != null) _chest.Disarm();
        }

        /// <summary>배정 적 전멸 시 (🔴 계약). 나가는 문을 열고 상자를 놓는다.</summary>
        public void OnCleared()
        {
            if (_exitDoor != null) _exitDoor.Unlock();
            if (_chest != null) _chest.Arm();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_entered) return;
            if (other.GetComponentInParent<Luddite.Player.PlayerController>() == null) return;
            _entered = true;
            if (PlayerEntered != null) PlayerEntered(this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            // _entered는 되돌리지 않는다 — 재진입으로 웨이브가 다시 시작되면 안 되기 때문.
            // 이 이벤트는 카메라 전환 전용이다
            if (other.GetComponentInParent<Luddite.Player.PlayerController>() == null) return;
            if (PlayerExited != null) PlayerExited(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isCombatRoom ? Color.yellow : Color.cyan;
            var box = GetComponent<BoxCollider2D>();
            if (box != null) Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, box.size);
        }
    }
}
