using UnityEngine;
using TMPro;
using Luddite.Core;

namespace Luddite.UI
{
    /// <summary>
    /// HUD 좌상단 진행 표기 (GDD §10.1). 읽기 전용.
    /// v1.1: 던전 모드에서는 <b>표기만</b> ROOM으로 바꾼다 — 내부 상태·API는 Wave 계열 그대로 (계약).
    /// 폴백(토글 OFF)에서는 기존 WAVE 표기 유지.
    /// </summary>
    public class WaveLabel : MonoBehaviour
    {
        [SerializeField] private WaveManager _waveManager;
        [SerializeField] private TMP_Text _label;

        private string _prefix = "WAVE";

        private void Start()
        {
            DungeonManager dungeon = FindFirstObjectByType<DungeonManager>();
            if (dungeon != null && dungeon.Active) _prefix = "ROOM";
        }

        private void Update()
        {
            if (_waveManager == null || _label == null) return;
            _label.text = $"{_prefix} {_waveManager.CurrentWaveNumber}/{_waveManager.TotalWaves}";
        }
    }
}
