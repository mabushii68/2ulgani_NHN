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

        private bool _entered;

        /// <summary>플레이어가 이 방에 처음 들어왔을 때 1회. 인자는 체인 인덱스.</summary>
        public event System.Action<Room> PlayerEntered;

        public int ChainIndex => _chainIndex;
        public bool IsCombatRoom => _isCombatRoom;
        public Door EntryDoor => _entryDoor;
        public Door ExitDoor => _exitDoor;
        public Chest Chest => _chest;
        public Vector2 Center => transform.position;

        /// <summary>웨이브 번호 = 체인 인덱스 (전투방 1 → 웨이브 1).</summary>
        public int WaveNumber => _chainIndex;

        /// <summary>재플레이 리셋 — 문 잠금 복구 + 상자 회수. 이게 없으면 2회차가 깨진다.</summary>
        public void ResetRoom()
        {
            _entered = false;
            if (_entryDoor != null) _entryDoor.Lock();
            if (_exitDoor != null) _exitDoor.Lock();
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

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isCombatRoom ? Color.yellow : Color.cyan;
            var box = GetComponent<BoxCollider2D>();
            if (box != null) Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, box.size);
        }
    }
}
