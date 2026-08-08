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

        private Room _currentRoom;
        private bool _active;

        /// <summary>던전 모드가 실제로 켜져 있는가. 폴백 판정의 단일 기준.</summary>
        public bool Active => _active;

        public Room CurrentRoom => _currentRoom;

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
                if (_rooms[i].Chest != null)
                {
                    _rooms[i].Chest.Configure(_config.AutoOpenChest, _config.ChestInteractRadius, _player);
                    _rooms[i].Chest.Opened += OnChestOpened;
                }
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
            }
        }

        private void OnPlayerEnteredRoom(Room room)
        {
            if (!_active || room == null) return;
            _currentRoom = room;
            MoveCameraTo(room);

            if (!room.IsCombatRoom) return;

            // 방 락인 (🔴 전멸형 종료 계약의 방 단위 번역)
            if (room.EntryDoor != null) room.EntryDoor.Lock();
            if (room.ExitDoor != null) room.ExitDoor.Lock();

            // 스폰 링을 이 방 중심으로 옮긴 뒤 웨이브 시작
            _waveManager.SetSpawnOrigin(room.Center);
            _waveManager.BeginWaveNow(room.WaveNumber);
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
            _cameraFollow.SetRoom(room.Center, _config.RoomHalfExtents);
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
