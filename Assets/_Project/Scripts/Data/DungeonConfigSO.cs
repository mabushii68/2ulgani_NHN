using UnityEngine;

namespace Luddite.Data
{
    /// <summary>
    /// 던전 체인 설정 (GDD 개정안 v1.1 §2). **🔴 안전판: <see cref="Enabled"/> 토글.**
    ///
    /// <para><b>토글 OFF = 현행 웨이브 아레나로 완전 동작해야 한다.</b> 던전 코드는 전부 이 플래그
    /// 뒤에 있고, OFF일 때 <see cref="Core.WaveManager"/>는 D4까지의 경로를 그대로 탄다.
    /// 이 폴백 경로를 깨는 변경은 금지 — 던전이 미완성이어도 현행 아레나로 제출할 수 있어야 한다.</para>
    ///
    /// <para>방 규격은 🔴 계약이다 (v1.1 계약 #2 + 2026-08-08 사람 지시로 32×18 개정).
    /// 방마다 크기를 바꾸지 않는다 — 스폰 링·교전 거리·DDA 임계 튜닝이 여기에 묶여 있다.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "DungeonConfig", menuName = "Luddite/Dungeon Config")]
    public class DungeonConfigSO : ScriptableObject
    {
        [Header("🔴 안전판")]
        [Tooltip("OFF면 던전 전체가 비활성 — 현행 웨이브 아레나로 동작한다")]
        [SerializeField] private bool _enabled = true;

        [Header("방 규격 (🔴 계약 — 방마다 다르게 하지 않는다)")]
        [Tooltip("전투방 내부 반경(유닛). 32×18 → (16, 9). 화면 26.67×15보다 커야 추적 카메라가 작동한다")]
        [SerializeField] private Vector2 _roomHalfExtents = new Vector2(16f, 9f);

        [Tooltip("벽 두께(유닛)")]
        [SerializeField] private float _wallThickness = 1f;

        [Header("체인 구성")]
        [Tooltip("전투방 수. 웨이브 1:1 대응이며 마지막 전투방 뒤에 보스방이 온다")]
        [SerializeField] private int _combatRoomCount = 6;

        [Tooltip("복도 길이(유닛). 순수 이동 통로 — 인터랙션 없음 (개정안 §2)")]
        [SerializeField] private float _corridorLength = 10f;

        [Tooltip("복도 폭(유닛)")]
        [SerializeField] private float _corridorWidth = 5f;

        [Header("상자 (WaveInterval 대체 — 개정안 §4)")]
        [Tooltip("방 클리어 시 상자를 자동으로 열지 여부. true면 스킵·백트래킹 구현이 통째로 사라진다 (권고)")]
        [SerializeField] private bool _autoOpenChest = true;

        [Tooltip("상자 접촉 인터랙트 반경(유닛). _autoOpenChest가 false일 때만 쓰인다")]
        [SerializeField] private float _chestInteractRadius = 1.2f;

        public bool Enabled => _enabled;
        public Vector2 RoomHalfExtents => _roomHalfExtents;
        public float WallThickness => _wallThickness;
        public int CombatRoomCount => _combatRoomCount;
        public float CorridorLength => _corridorLength;
        public float CorridorWidth => _corridorWidth;
        public bool AutoOpenChest => _autoOpenChest;
        public float ChestInteractRadius => _chestInteractRadius;

        /// <summary>체인 상의 방 간 중심 간격. 방 폭 + 복도 길이.</summary>
        public float RoomSpacing => _roomHalfExtents.x * 2f + _corridorLength;

        /// <summary>
        /// 체인 인덱스 → 방 중심 좌표. 0 = 시작방, 1..N = 전투방, N+1 = 보스방.
        /// 선형 체인(분기 없음)이라 x축으로만 늘어선다.
        /// </summary>
        public Vector2 RoomCenter(int chainIndex)
        {
            return new Vector2(chainIndex * RoomSpacing, 0f);
        }

        /// <summary>시작방 + 전투방 N + 보스방.</summary>
        public int TotalRooms => _combatRoomCount + 2;

        private void OnValidate()
        {
            if (_roomHalfExtents.x < 8f || _roomHalfExtents.y < 5f)
                Debug.LogWarning("[DungeonConfig] 방이 너무 작다 — 스폰 링·교전 거리 튜닝이 깨진다 (사람 승인 필요)", this);
            if (_combatRoomCount < 1) _combatRoomCount = 1;
            if (_corridorWidth > _roomHalfExtents.y * 2f) _corridorWidth = _roomHalfExtents.y * 2f;
        }
    }
}
