using UnityEngine;
using TMPro;
using Luddite.AIBrain;
using Luddite.Core;
using Luddite.Data;

namespace Luddite.UI
{
    /// <summary>
    /// 결과 화면 "AI가 학습한 나" 프로필 (GDD §13 — 절대 컷 금지).
    /// 표시: 별명(3축 룰 테이블) / 평균 교전 거리 / 무빙샷 비율 / 선호 구역 /
    /// 회피 편향 / AI 예측 적중률 / 역카운터 성공률 / 8방향 이동 히스토그램.
    /// 데이터는 <see cref="AIBrainRunner"/>를 읽기만 한다 (규칙 7).
    /// Result 패널이 켜질 때 1회 구성 — 런은 이미 끝났으므로 갱신이 필요 없다.
    /// 별명·코멘트는 SO 한국어 원문(§13 목업), 통계 블록은 영어 시스템 발화 (§10.5 — D7 이행).
    /// </summary>
    public class ResultProfile : MonoBehaviour
    {
        /// <summary>히스토그램 인덱스(0=E 반시계, PlayStyleProfiler 규약)의 표기 라벨.</summary>
        private static readonly string[] DIRECTION_LABELS = { "E ", "NE", "N ", "NW", "W ", "SW", "S ", "SE" };

        [SerializeField] private AIBrainRunner _brain;
        [SerializeField] private NicknameTableSO _nicknameTable;

        [SerializeField] private TMP_Text _nickname;
        [SerializeField] private TMP_Text _summaryLine;
        [SerializeField] private TMP_Text _statsBlock;
        [SerializeField] private TMP_Text _histogramBlock;
        [SerializeField] private TMP_Text _comment;

        private void OnEnable()
        {
            if (_brain == null || !_brain.IsReady) return;

            float distance = _brain.AverageEngageDistance;
            float bias = _brain.DominantProbability;
            float mobility = _brain.MovingShotRatio;
            string direction = _brain.DominantDirection == DodgeDirection.Left ? "LEFT" : "RIGHT";

            NicknameTableSO.Entry entry = null;
            if (_nicknameTable != null)
            {
                entry = _nicknameTable.Find(
                    _nicknameTable.BandOf(distance),
                    _nicknameTable.IsHighBias(bias),
                    _nicknameTable.IsHighMobility(mobility));
            }

            if (_nickname != null)
                _nickname.text = entry != null ? $"[ {entry.NameKo} ]" : "[ 피험자 #001 ]";

            if (_summaryLine != null)
                _summaryLine.text =
                    // 구분자는 '|' — 가운뎃점(U+00B7)이 한글 폰트에 없어 □로 렌더링됐다 (D7 세션 8).
                    // 폴백 폰트(LiberationSans SDF - Fallback)는 글리프가 1개뿐이라 구제되지 않는다
                    $"AVG DISTANCE {distance:F1}  |  DODGE {direction} {bias:P0}  |  ZONE {_brain.FavoriteQuadrant}";

            if (_statsBlock != null)
                _statsBlock.text =
                    "AI ANALYSIS\n" +
                    $"AVG ENGAGE DISTANCE   {distance:F1}u\n" +
                    $"MOVING SHOT RATIO     {mobility:P0}\n" +
                    $"FAVORITE ZONE         {_brain.FavoriteQuadrant}\n" +
                    $"SAMPLES LEARNED       {_brain.LearnedSampleCount}\n" +
                    $"AI PREDICTION HIT     {_brain.PredictionAccuracy:P0} ({_brain.PredictiveHits}/{_brain.PredictiveAttempts})\n" +
                    $"COUNTER DODGE         {_brain.CounterDodgeRate:P0} ({_brain.CounterDodgeCount}/{_brain.PredictiveAttempts})";

            if (_histogramBlock != null) _histogramBlock.text = BuildHistogram();

            if (_comment != null)
                _comment.text = entry != null ? $"\"{entry.CommentKo}\"" : "";
        }

        /// <summary>8방향 이동 히스토그램 텍스트 바. TODO(아트): 회피 히트맵 시각화로 교체 (§13).</summary>
        private string BuildHistogram()
        {
            var builder = new System.Text.StringBuilder("MOVEMENT HISTOGRAM\n");
            for (int i = 0; i < 8; i++)
            {
                float ratio = _brain.DirectionRatio(i);
                int bars = Mathf.RoundToInt(ratio * 20f);
                builder.Append(DIRECTION_LABELS[i]).Append(' ')
                       .Append(new string('|', bars).PadRight(20, '.'))
                       .Append($" {ratio:P0}\n");
            }
            return builder.ToString().TrimEnd('\n');
        }
    }
}
