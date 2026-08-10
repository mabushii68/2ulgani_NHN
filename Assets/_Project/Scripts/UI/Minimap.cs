using UnityEngine;
using UnityEngine.UI;
using Luddite.Core;

namespace Luddite.UI
{
    /// <summary>
    /// 우상단 미니맵 (D7 신규 — 개정안 v1.1 §6에서 컷됐던 항목을 요청자 결정으로 복원).
    ///
    /// <para><b>읽기 전용</b> — <see cref="DungeonManager.CurrentRoom"/>만 본다 (CLAUDE.md 규칙 7).
    /// 아이콘 배치는 <c>HudSetupTools</c>가 방 실좌표에서 결정론으로 굽고, 이 컴포넌트는 색만 바꾼다.</para>
    ///
    /// <para>던전 토글 OFF면 통째로 숨는다 — 폴백 아레나에는 방 개념이 없다.</para>
    ///
    /// <para>🔴 색: 마젠타·핫핑크·고채도 보라(AI 전용), 파랑·초록·노랑(전공색 전용)을 쓰지 않는다
    /// (CLAUDE.md 아트 색 규칙). 무채색 + 주황 범위로만 위계를 만든다.</para>
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        [SerializeField] private DungeonManager _dungeon;

        [Tooltip("체인 순서대로. 인덱스 = Room.ChainIndex")]
        [SerializeField] private Image[] _roomIcons;

        [Tooltip("방 사이 복도 연결선. 인덱스 i = 방 i↔i+1")]
        [SerializeField] private Image[] _corridorIcons;

        [Tooltip("패널 전체 — 던전 OFF일 때 끈다")]
        [SerializeField] private GameObject _content;

        [Header("색 — 무채색~주황만 (예약 색역 회피)")]
        [SerializeField] private Color _current = new Color(1f, 0.78f, 0.42f, 1f);
        [SerializeField] private Color _visited = new Color(0.62f, 0.50f, 0.44f, 1f);
        [SerializeField] private Color _unvisited = new Color(0.32f, 0.28f, 0.30f, 0.75f);
        [SerializeField] private Color _corridor = new Color(0.40f, 0.34f, 0.34f, 0.75f);

        [Tooltip("보스방 강조 — 갈 곳이 어디인지 첫눈에 보이게")]
        [SerializeField] private Color _bossTint = new Color(0.85f, 0.42f, 0.28f, 1f);

        // 답사 여부를 따로 기록하지 않는다. **체인이 선형·전진 전용**이라(개정안에서 백트래킹·
        // 복도 인터랙션이 컷됨) "답사한 방 = 인덱스가 현재보다 작은 방"이 항상 성립한다.
        // ⚠️ 배열로 기록하면 재플레이 때 지워야 하는데, HudPanel은 Combat에서만 켜져(GameScreens)
        //    방↔인터벌마다 OnEnable이 돌기 때문에 "켜질 때 초기화"가 성립하지 않는다.
        //    파생값으로 두면 그 문제가 통째로 사라지고, 재플레이 시 현재 방이 0으로 돌아가며 자동 복구된다.
        private int _shownIndex = -1;
        private int _paintedIndex = -99;

        private void Awake()
        {
            if (_dungeon == null) _dungeon = FindFirstObjectByType<DungeonManager>();
        }

        private void LateUpdate()
        {
            bool active = _dungeon != null && _dungeon.Active;
            if (_content != null && _content.activeSelf != active) _content.SetActive(active);
            if (!active || _roomIcons == null) return;

            Room room = _dungeon.CurrentRoom;
            // 복도 구간에서는 CurrentRoom이 null이 된다 — 직전 방을 유지해 미니맵이 깜빡이지 않게
            if (room != null) _shownIndex = room.ChainIndex;

            if (_shownIndex == _paintedIndex) return;   // 방이 바뀔 때만 다시 칠한다
            _paintedIndex = _shownIndex;
            Repaint(_shownIndex);
        }

        private void Repaint(int currentIndex)
        {
            int last = _roomIcons.Length - 1;
            for (int i = 0; i < _roomIcons.Length; i++)
            {
                if (_roomIcons[i] == null) continue;
                Color c;
                if (i == currentIndex) c = _current;
                else if (i < currentIndex) c = _visited;   // 선형 체인 — 지나온 방
                else c = _unvisited;

                // 보스방은 미답사여도 존재를 알린다 (색조만 섞고 밝기는 위계를 따른다)
                if (i == last && i != currentIndex) c = Color.Lerp(c, _bossTint, 0.55f);
                _roomIcons[i].color = c;
            }

            if (_corridorIcons == null) return;
            for (int i = 0; i < _corridorIcons.Length; i++)
                if (_corridorIcons[i] != null) _corridorIcons[i].color = _corridor;
        }
    }
}
