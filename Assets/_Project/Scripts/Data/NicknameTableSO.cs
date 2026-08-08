using UnityEngine;

namespace Luddite.Data
{
    /// <summary>플레이 스타일 3축 중 거리 밴드 (§13).</summary>
    public enum DistanceBand
    {
        Near,
        Mid,
        Far,
    }

    /// <summary>
    /// 결과 화면 별명 룰 테이블 (GDD §13 — 거리 × 회피편향 × 무빙샷 3축).
    /// GDD는 예시 1개("원거리+고편향 = 겁쟁이 저격수")만 확정하므로 나머지 항목은
    /// <b>초안이며 기획(이양빈) 검토 대상</b>이다. SO라서 코드 수정 없이 문구를 바꿀 수 있다.
    /// 한국어가 원문(§10.5), 영문은 한글 폰트 반입 전 표시용.
    /// </summary>
    [CreateAssetMenu(fileName = "NicknameTable", menuName = "Luddite/Nickname Table")]
    public class NicknameTableSO : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            [SerializeField] private DistanceBand _distance;
            [SerializeField] private bool _highBias;
            [SerializeField] private bool _highMobility;
            [SerializeField] private string _nameKo;
            [SerializeField] private string _nameEn;
            [SerializeField] private string _commentKo;
            [SerializeField] private string _commentEn;

            public Entry(DistanceBand distance, bool highBias, bool highMobility,
                string nameKo, string nameEn, string commentKo, string commentEn)
            {
                _distance = distance;
                _highBias = highBias;
                _highMobility = highMobility;
                _nameKo = nameKo;
                _nameEn = nameEn;
                _commentKo = commentKo;
                _commentEn = commentEn;
            }

            public bool Matches(DistanceBand distance, bool highBias, bool highMobility) =>
                _distance == distance && _highBias == highBias && _highMobility == highMobility;

            public string NameKo => _nameKo;
            public string NameEn => _nameEn;
            public string CommentKo => _commentKo;
            public string CommentEn => _commentEn;
        }

        [Header("밴드 임계값")]
        [Tooltip("평균 교전 거리가 이 값 초과면 원거리형. DDA(§6.3)와 같은 기준을 기본값으로 쓴다")]
        [SerializeField] private float _farDistance = 6f;

        [Tooltip("이 값 미만이면 근접형")]
        [SerializeField] private float _nearDistance = 3.5f;

        [Tooltip("우세 회피 확률이 이 값 이상이면 고편향")]
        [Range(0.5f, 1f)]
        [SerializeField] private float _highBiasProbability = 0.65f;

        [Tooltip("무빙샷 비율이 이 값 이상이면 기동형")]
        [Range(0f, 1f)]
        [SerializeField] private float _highMobilityRatio = 0.5f;

        [SerializeField] private Entry[] _entries = new Entry[0];

        public DistanceBand BandOf(float averageDistance)
        {
            if (averageDistance > _farDistance) return DistanceBand.Far;
            if (averageDistance < _nearDistance) return DistanceBand.Near;
            return DistanceBand.Mid;
        }

        public bool IsHighBias(float dominantProbability) => dominantProbability >= _highBiasProbability;

        public bool IsHighMobility(float movingShotRatio) => movingShotRatio >= _highMobilityRatio;

        /// <summary>3축에 맞는 항목. 테이블에 없으면 null — 호출자가 기본 문구로 대체한다.</summary>
        public Entry Find(DistanceBand distance, bool highBias, bool highMobility)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i] != null && _entries[i].Matches(distance, highBias, highMobility))
                    return _entries[i];
            }
            return null;
        }

#if UNITY_EDITOR
        /// <summary>빌더 전용 시드 주입 (런타임 호출 금지).</summary>
        public void EditorSetEntries(Entry[] entries) => _entries = entries;
#endif
    }
}
