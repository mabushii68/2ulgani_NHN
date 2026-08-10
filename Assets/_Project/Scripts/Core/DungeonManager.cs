using UnityEngine;
using Luddite.Data;

namespace Luddite.Core
{
    /// <summary>
    /// 던전 체인 진행의 단독 소유자 (개정안 §2/§4).
    ///
    /// <para><b>🔴 안전판:</b> <see cref="DungeonConfigSO.Enabled"/>가 false거나 설정이 비어 있으면
    /// 이 컴포넌트는 <b>아무것도 하지 않는다</b> — WaveManager가 D4까지의 경로(Combat 진입 = 웨이브 시작,
    /// 전멸 = 인터벌)를 그대로 탄다. 던전 코드가 폴백을 오염시키지 않는 유일한 보증이 이 조기 반환이다.</para>
    ///
    /// <para>루프: 방 진입 → 문 잠김 + 웨이브 시작 → 전멸(WaveManager) → 나가는 문 개방 + 상자 →
    /// 상자 오픈 → 기존 인터벌 패널 → 다음 방. 감쇠·프로파일·DDA는 WaveManager가 이미
    /// <c>WaveEnded</c>에서 끝내므로 여기서는 손대지 않는다 (AIBrain 무변경).</para>
    /// </summary>
    public class DungeonManager : MonoBehaviour
    {
        [SerializeField] private DungeonConfigSO _config;
        [SerializeField] private WaveManager _waveManager;
        [SerializeField] private GameManager _gameManager;
        [SerializeField] private CameraFollow _cameraFollow;
        [SerializeField] private Transform _player;

        [Tooltip("체인 순서대로. 0 = 시작방, 마지막 = 보스방")]
        [SerializeField] private Room[] _rooms;

        [Tooltip("문이 잠긴 뒤 스폰까지의 지연(초). MAP_SPEC §6 = 0.5. 연출 타이밍이라 SO 아닌 인스펙터 노출")]
        [SerializeField] private float _lockInDelay = 0.5f;

        private Room _currentRoom;
        private bool _active;
        private AIBrainRunner _brainRunner;

        /// <summary>던전 모드가 실제로 켜져 있는가. 폴백 판정의 단일 기준.</summary>
        public bool Active => _active;

        public Room CurrentRoom => _currentRoom;

        /// <summary>방 내부 반폭. 보스 P2 구역 장판이 4분할 좌표 계산에 읽는다.</summary>
        public Vector2 RoomHalfExtents => _config != null ? _config.RoomHalfExtents : Vector2.zero;

        private void Awake()
        {
            _active = _config != null && _config.Enabled && _rooms != null && _rooms.Length > 0;
            if (!_active)
            {
                // 🔴 폴백: 던전 비활성 — WaveManager는 기존 경로 그대로
                Debug.Log("[DungeonManager] 던전 모드 OFF — 현행 웨이브 아레나로 동작");
                enabled = false;
                return;
            }

            if (_waveManager == null) _waveManager = FindFirstObjectByType<WaveManager>();
            if (_gameManager == null) _gameManager = FindFirstObjectByType<GameManager>();
            if (_cameraFollow == null) _cameraFollow = FindFirstObjectByType<CameraFollow>();
            _brainRunner = FindFirstObjectByType<AIBrainRunner>();   // 프로필 4분할 원점 보정용 (없으면 무시)
            if (_waveManager == null || _gameManager == null)
            {
                Debug.LogError("[DungeonManager] WaveManager/GameManager 없음 — 던전 비활성화", this);
                _active = false; enabled = false; return;
            }

            // 웨이브 시작 권한을 던전이 가져온다 (Combat 진입 자동 시작 억제)
            _waveManager.SetExternalWaveControl(true);
            _waveManager.RoomCleared += OnRoomCleared;

            for (int i = 0; i < _rooms.Length; i++)
            {
                if (_rooms[i] == null) continue;
                _rooms[i].PlayerEntered += OnPlayerEnteredRoom;
                _rooms[i].PlayerExited += OnPlayerExitedRoom;
                if (_rooms[i].Chest != null)
                {
                    _rooms[i].Chest.Configure(_config.AutoOpenChest, _config.ChestInteractRadius, _player);
                    _rooms[i].Chest.Opened += OnChestOpened;
                }
            }
            // 시작 시점부터 시작방에 세운다.
            // ⚠️ 이전에는 RunStarted에서만 옮겨서, 타이틀 화면 배경에 **폴백 아레나**(0,0)가 보였다 —
            //    던전은 y=-200에 있어 화면에 없었고, 심사자가 처음 보는 그림이 안 쓰는 맵이었다 (D7 수정).
            //    토글 OFF면 이 경로 자체를 타지 않으므로 폴백 동작은 그대로다.
            Room startRoom = FindRoom(0);
            if (startRoom != null)
            {
                if (_player != null)
                    _player.position = new Vector3(startRoom.Center.x, startRoom.Center.y, _player.position.z);
                MoveCameraTo(startRoom);
                if (_cameraFollow != null) _cameraFollow.SnapToTarget();
            }

            Debug.Log("[DungeonManager] 던전 모드 ON — 방 " + _rooms.Length + "개 (전투방 " + _config.CombatRoomCount + ")");
        }

        private void OnEnable()
        {
            GameEvents.RunStarted += OnRunStarted;
        }

        private void OnDisable()
        {
            GameEvents.RunStarted -= OnRunStarted;
            if (_waveManager != null) _waveManager.RoomCleared -= OnRoomCleared;
            if (_rooms != null)
            {
                for (int i = 0; i < _rooms.Length; i++)
                {
                    if (_rooms[i] == null) continue;
                    _rooms[i].PlayerEntered -= OnPlayerEnteredRoom;
                    _rooms[i].PlayerExited -= OnPlayerExitedRoom;
                }
            }
        }

        /// <summary>재플레이 리셋 — 문 잠금 복구·상자 회수·플레이어를 시작방으로.</summary>
        private void OnRunStarted()
        {
            if (!_active) return;
            for (int i = 0; i < _rooms.Length; i++) if (_rooms[i] != null) _rooms[i].ResetRoom();
            _currentRoom = null;

            Room start = FindRoom(0);
            if (start != null)
            {
                if (_player != null) _player.position = new Vector3(start.Center.x, start.Center.y, _player.position.z);
                MoveCameraTo(start);
                if (_cameraFollow != null) _cameraFollow.SnapToTarget();
                // 시작방은 전투가 없으므로 나가는 문을 바로 연다
                if (start.ExitDoor != null) start.ExitDoor.Unlock();
                _currentRoom = start;
                if (_brainRunner != null) _brainRunner.SetProfileOrigin(start.Center);
            }
        }

        private void OnPlayerEnteredRoom(Room room)
        {
            if (!_active || room == null) return;
            _currentRoom = room;
            MoveCameraTo(room);

            // §6.4 4분할은 "방 중심 기준"이어야 한다 — 던전은 y=−200이라 월드 원점 기준이면 전부 남쪽으로 오염
            if (_brainRunner != null) _brainRunner.SetProfileOrigin(room.Center);

            if (!room.IsCombatRoom) return;

            // 방 락인 (🔴 전멸형 종료 계약의 방 단위 번역)
            if (room.EntryDoor != null) room.EntryDoor.Lock();
            if (room.ExitDoor != null) room.ExitDoor.Lock();

            // MAP_SPEC §6: 문이 닫히는 것을 볼 시간을 준 뒤 스폰한다.
            // 즉시 스폰하면 "갇혔다"는 인지보다 적이 먼저 와서 락인 연출이 죽는다
            StartCoroutine(BeginWaveAfterLockIn(room));
        }

        private System.Collections.IEnumerator BeginWaveAfterLockIn(Room room)
        {
            yield return new WaitForSeconds(_lockInDelay);
            if (!_active || room == null) yield break;

            // 스폰 영역을 이 방(중심 + 방 규격)으로 옮긴 뒤 웨이브 시작 — 링 크기는 방의 속성이다.
            // SO의 링 값은 폴백 아레나(12×7) 기준으로 남는다 (토글 OFF 소프트락 정정)
            _waveManager.SetSpawnArea(room.Center, _config.RoomHalfExtents);
            _waveManager.BeginWaveNow(room.WaveNumber);
        }

        /// <summary>
        /// 방 → 복도로 나갔다. 카메라 바운드를 <b>이 방 ~ 다음 방 전체</b>로 넓혀 복도를 지나는 동안에도
        /// 플레이어를 따라가게 한다. 방 바운드에 묶어 두면 카메라가 방 경계에서 멈추고
        /// 플레이어만 화면 밖으로 걸어 나가 "복도를 지나는 중"이라는 게 안 보인다.
        ///
        /// <para>넓힌 바운드는 좌우 양쪽 방 내부를 함께 비추므로 복도 바깥이 검게 뚫려 보이지 않는다.
        /// 다음 방 트리거에 닿는 순간 다시 그 방으로 좁혀진다.</para>
        /// </summary>
        private void OnPlayerExitedRoom(Room room)
        {
            if (!_active || room == null || _cameraFollow == null || _config == null) return;

            Room next = FindRoom(room.ChainIndex + 1);
            Room prev = FindRoom(room.ChainIndex - 1);
            Room other = next != null ? next : prev;      // 마지막 방이면 뒤쪽 복도로 넓힌다
            if (other == null) return;

            // 두 방의 카메라 사각형을 통째로 감싼다. 이전 구현은 x축 간격만 봤는데,
            // 체인에 세로 구간(⤴⤵)이 있어 y로 떨어진 두 방에서는 바운드가 복도를 못 덮었다.
            Bounds b = CameraBox(room);
            b.Encapsulate(CameraBox(other));
            _cameraFollow.SetRoom(b.center, b.extents);
        }

        /// <summary>방의 카메라 클램프 사각형. 빌더가 실루엣을 안 채웠으면 설정값 규격으로 폴백한다.</summary>
        private Bounds CameraBox(Room room)
        {
            Vector2 c = room.HasCameraBounds ? room.CameraCenter : room.Center;
            Vector2 e = room.HasCameraBounds ? room.CameraHalfExtents : _config.RoomHalfExtents;
            return new Bounds(c, e * 2f);
        }

        private void OnRoomCleared(int waveNumber)
        {
            if (!_active) return;
            Room room = _currentRoom;
            if (room == null) room = FindRoomByWave(waveNumber);
            if (room == null) { Debug.LogWarning("[DungeonManager] 클리어된 방을 못 찾음 — 웨이브 " + waveNumber); return; }
            Debug.Log("[DungeonManager] 방 " + room.ChainIndex + " 클리어 — 문 개방 + 상자");
            room.OnCleared();
        }

        /// <summary>상자 오픈 = 기존 WaveInterval 패널 호출. 내부 시스템은 D4의 것을 그대로 쓴다.</summary>
        private void OnChestOpened()
        {
            if (!_active) return;
            _gameManager.BeginWaveInterval();
        }

        private void MoveCameraTo(Room room)
        {
            if (_cameraFollow == null || _config == null) return;
            Bounds b = CameraBox(room);
            _cameraFollow.SetRoom(b.center, b.extents);
        }

        private Room FindRoom(int chainIndex)
        {
            for (int i = 0; i < _rooms.Length; i++)
                if (_rooms[i] != null && _rooms[i].ChainIndex == chainIndex) return _rooms[i];
            return null;
        }

        private Room FindRoomByWave(int waveNumber)
        {
            for (int i = 0; i < _rooms.Length; i++)
                if (_rooms[i] != null && _rooms[i].IsCombatRoom && _rooms[i].WaveNumber == waveNumber) return _rooms[i];
            return null;
        }
    }
}
