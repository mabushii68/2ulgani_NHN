using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Luddite.Core;
using Luddite.Data;

namespace Luddite.UI
{
    /// <summary>
    /// WaveInterval 패널의 업그레이드 3택 카드 (GDD §8, §10.2 하단).
    /// 패널이 켜질 때(인터벌 진입) 추첨하고, 카드 선택 = 적용 + 다음 웨이브 진행.
    /// 후보가 없을 때(전부 상한)만 NEXT WAVE 버튼을 노출한다.
    /// 표기는 SO의 한국어 원문 (§10.5 인간 세계 = 한국어 — D3 반입 한글 폰트 전제, D7 이행).
    /// </summary>
    public class UpgradePanel : MonoBehaviour
    {
        private const int CARD_COUNT = 3;

        [SerializeField] private UpgradeManager _upgradeManager;
        [SerializeField] private GameManager _gameManager;

        [Tooltip("카드 버튼 3개 (순서 고정)")]
        [SerializeField] private Button[] _cardButtons = new Button[CARD_COUNT];

        [SerializeField] private TMP_Text[] _cardNames = new TMP_Text[CARD_COUNT];
        [SerializeField] private TMP_Text[] _cardTooltips = new TMP_Text[CARD_COUNT];

        [Tooltip("후보가 없을 때만 보여 줄 기존 NEXT WAVE 버튼")]
        [SerializeField] private Button _nextWaveButton;

        private readonly List<UpgradeSO> _choices = new List<UpgradeSO>(CARD_COUNT);

        private void Awake()
        {
            for (int i = 0; i < _cardButtons.Length; i++)
            {
                int index = i;   // 클로저 캡처
                if (_cardButtons[i] != null)
                    _cardButtons[i].onClick.AddListener(() => OnCardClicked(index));
            }
        }

        /// <summary>WaveIntervalPanel 활성화 = 인터벌 진입 시점. 여기서 추첨한다.</summary>
        private void OnEnable()
        {
            if (_upgradeManager == null) return;

            _choices.Clear();
            _choices.AddRange(_upgradeManager.DrawChoices(CARD_COUNT));

            for (int i = 0; i < _cardButtons.Length; i++)
            {
                bool hasChoice = i < _choices.Count;
                if (_cardButtons[i] != null) _cardButtons[i].gameObject.SetActive(hasChoice);
                if (!hasChoice) continue;

                UpgradeSO upgrade = _choices[i];
                int stack = _upgradeManager.StackOf(upgrade);
                string stackLabel = upgrade.MaxStacks > 0 ? $"  ({stack}/{upgrade.MaxStacks})" : "";

                if (_cardNames[i] != null) _cardNames[i].text = upgrade.DisplayName + stackLabel;
                if (_cardTooltips[i] != null) _cardTooltips[i].text = upgrade.Tooltip;
            }

            if (_nextWaveButton != null) _nextWaveButton.gameObject.SetActive(_choices.Count == 0);
        }

        private void OnCardClicked(int index)
        {
            if (index >= _choices.Count || _upgradeManager == null || _gameManager == null) return;

            _upgradeManager.Apply(_choices[index]);
            _gameManager.ContinueToNextWave();
        }
    }
}
