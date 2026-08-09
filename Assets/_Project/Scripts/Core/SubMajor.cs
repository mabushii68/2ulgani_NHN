namespace Luddite.Core
{
    /// <summary>
    /// 세부전공 9종 (D7 신설). 전공(Major)당 3종이며 첫 WaveInterval에서
    /// 업그레이드 카드 대신 선택한다 — 선택 값은 <see cref="GameManager"/>가 소유.
    /// 현재는 선택·표기만 하고, 세부전공별 탄막 차별화는 후속 작업이다 (사람 지시로 보류).
    /// </summary>
    public enum SubMajor
    {
        /// <summary>미선택 — 첫 인터벌 전의 기본값.</summary>
        None = 0,

        // 문과
        Linguistics,       // 어문계
        Commerce,          // 상경계
        Law,               // 법조계

        // 이과
        NaturalScience,    // 자연과학
        Engineering,       // 공학
        ComputerScience,   // 컴퓨터과학

        // 예체능
        PhysicalEducation, // 체육
        FineArts,          // 미술
        Music,             // 음악
    }

    /// <summary>전공 ↔ 세부전공 매핑과 한국어 표기 (§10.5 인간 세계 = 한국어).</summary>
    public static class SubMajorInfo
    {
        private static readonly SubMajor[] LIBERAL_ARTS =
            { SubMajor.Linguistics, SubMajor.Commerce, SubMajor.Law };
        private static readonly SubMajor[] SCIENCE =
            { SubMajor.NaturalScience, SubMajor.Engineering, SubMajor.ComputerScience };
        private static readonly SubMajor[] ARTS =
            { SubMajor.PhysicalEducation, SubMajor.FineArts, SubMajor.Music };

        /// <summary>해당 전공의 세부전공 3종 (표시 순서 고정).</summary>
        public static SubMajor[] OfMajor(Major major)
        {
            switch (major)
            {
                case Major.Science: return SCIENCE;
                case Major.Arts: return ARTS;
                default: return LIBERAL_ARTS;
            }
        }

        /// <summary>세부전공이 속한 전공. None은 기본 전공(문과)을 돌려준다 — 호출부가 None을 먼저 걸러라.</summary>
        public static Major MajorOf(SubMajor subMajor)
        {
            switch (subMajor)
            {
                case SubMajor.NaturalScience:
                case SubMajor.Engineering:
                case SubMajor.ComputerScience:
                    return Major.Science;
                case SubMajor.PhysicalEducation:
                case SubMajor.FineArts:
                case SubMajor.Music:
                    return Major.Arts;
                default:
                    return Major.LiberalArts;
            }
        }

        public static string DisplayNameKo(SubMajor subMajor)
        {
            switch (subMajor)
            {
                case SubMajor.Linguistics: return "어문계";
                case SubMajor.Commerce: return "상경계";
                case SubMajor.Law: return "법조계";
                case SubMajor.NaturalScience: return "자연과학";
                case SubMajor.Engineering: return "공학";
                case SubMajor.ComputerScience: return "컴퓨터과학";
                case SubMajor.PhysicalEducation: return "체육";
                case SubMajor.FineArts: return "미술";
                case SubMajor.Music: return "음악";
                default: return "미정";
            }
        }
    }
}
