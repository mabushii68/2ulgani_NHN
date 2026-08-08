using UnityEngine;
using TMPro;
using Luddite.Core;
using Luddite.Data;

namespace Luddite.UI
{
    /// <summary>
    /// WaveInterval의 COUNTER PROTOCOL 블록 (GDD §10.2).
    /// 3원칙 "읽을 수 있다" — AI가 다음 웨이브에 무엇을 바꾸는지 반드시 표기한다 (§6.3).
    /// 패널이 켜질 때(인터벌 진입) 한 번 구성하면 된다 — 인터벌 중에는 상태가 바뀌지 않는다.
    /// </summary>
    public class CounterProtocolLabel : MonoBehaviour
    {
        [SerializeField] private WaveManager _waveManager;
        [SerializeField] private AIBrainRunner _brain;
        [SerializeField] private TMP_Text _label;

        private void OnEnable()
        {
            if (_label == null) return;

            string lines = "";

            if (_brain != null && _brain.IsHighConfidence)
                lines += "→ PREDICTIVE FIRE ENABLED\n";

            if (_waveManager != null)
            {
                int percent = Mathf.RoundToInt(_waveManager.DdaRatio * 100f);
                switch (_waveManager.PlannedAdjustment)
                {
                    case DdaDecision.MoreRushUnits:
                        lines += $"→ RUSH UNITS +{percent}%\n";
                        break;
                    case DdaDecision.MoreRangedUnits:
                        lines += $"→ RANGED UNITS +{percent}%\n";
                        break;
                }
            }

            if (lines.Length == 0) lines = "→ NO ACTIVE PROTOCOL\n";

            _label.text = "COUNTER PROTOCOL\n" + lines.TrimEnd('\n');
        }
    }
}
