using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Luddite.Core;

namespace Luddite.UI
{
    /// <summary>
    /// 첫 WaveInterval에서 업그레이드 카드 대신 나오는 세부전공 3택 (D7).
    /// 문과 = 어문계/상경계/법조계, 이과 = 자연과학/공학/컴퓨터과학, 예체능 = 체육/미술/음악.
    /// 선택 = 저장(GameManager 전용 API) + 다음 전투 진행 — UpgradePanel과 같은 흐름.
    /// 세부전공이 이미 정해진 인터벌에서는 스스로 숨고 안내문을 원문으로 되돌린다.
    /// 세부전공별 탄막 차별화는 후속 작업 (사람 지시로 보류 — 지금은 선택 저장만).
    /// </summary>
    public class SubMajorPanel : MonoBehaviour
    {
        private const int CARD_COUNT = 3;

        [SerializeField] private GameManager _gameManager;

        [Tooltip("세부전공 카드 버튼 3개 (순서 고정 — SubMajorInfo.OfMajor 순서와 1:1)")]
        [SerializeField] private Button[] _cardButtons = new Button[CARD_COUNT];
        [SerializeField] private TMP_Text[] _cardNames = new TMP_Text[CARD_COUNT];

        [Tooltip("인터벌 안내문(BodyText) — 세부전공 선택 화면일 때만 문구를 바꾼다")]
        [SerializeField] private TMP_Text _bodyText;

        [Tooltip("세부전공 선택 화면의 안내문 (§10.5 인간 세계 = 한국어)")]
        [SerializeField] private string _prompt = "세부전공을 선택하세요. 선택하면 다음 전투가 시작됩니다.";

        private SubMajor[] _choices;
        private string _defaultBody;

        private void Awake()
        {
            if (_bodyText != null) _defaultBody = _bodyText.text;

            for (int i = 0; i < _cardButtons.Length; i++)
            {
                int index = i;   // 클로저 캡처
                if (_cardButtons[i] != null)
                    _cardButtons[i].onClick.AddListener(() => OnCardClicked(index));
            }
        }

        /// <summary>WaveIntervalPanel 활성화 = 인터벌 진입. 세부전공 미선택일 때만 나온다.</summary>
        private void OnEnable()
        {
            bool pending = _gameManager != null && _gameManager.SelectedSubMajor == SubMajor.None;

            if (!pending)
            {
                SetCardsActive(false);
                if (_bodyText != null && !string.IsNullOrEmpty(_defaultBody)) _bodyText.text = _defaultBody;
                return;
            }

            _choices = SubMajorInfo.OfMajor(_gameManager.SelectedMajor);

            for (int i = 0; i < _cardButtons.Length; i++)
            {
                bool hasChoice = _choices != null && i < _choices.Length;
                if (_cardButtons[i] != null) _cardButtons[i].gameObject.SetActive(hasChoice);
                if (hasChoice && _cardNames[i] != null)
                    _cardNames[i].text = SubMajorInfo.DisplayNameKo(_choices[i]);
            }

            if (_bodyText != null) _bodyText.text = _prompt;
        }

        private void OnCardClicked(int index)
        {
            if (_choices == null || index >= _choices.Length || _gameManager == null) return;

            _gameManager.SelectSubMajor(_choices[index]);
            _gameManager.ContinueToNextWave();
        }

        private void SetCardsActive(bool active)
        {
            foreach (Button button in _cardButtons)
                if (button != null) button.gameObject.SetActive(active);
        }
    }
}
