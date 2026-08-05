using UnityEngine;
using TMPro;
using Luddite.Core;

namespace Luddite.UI
{
    /// <summary>HUD 좌상단 "WAVE n/7" (GDD §10.1). 읽기 전용.</summary>
    public class WaveLabel : MonoBehaviour
    {
        [SerializeField] private WaveManager _waveManager;
        [SerializeField] private TMP_Text _label;

        private void Update()
        {
            if (_waveManager == null || _label == null) return;
            _label.text = $"WAVE {_waveManager.CurrentWaveNumber}/{_waveManager.TotalWaves}";
        }
    }
}
