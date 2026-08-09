using UnityEngine;
using Luddite.Core;

namespace Luddite.Data
{
    /// <summary>
    /// 세부전공별 플레이어 탄막 스프라이트 매핑 (D7). 어문계=펜 / 상경계=돈 / 법조계=책 /
    /// 자연과학=숫자 / 공학=기계(번개) / 컴퓨터과학=컴퓨터 / 체육=공 / 미술=붓 / 음악=음표.
    /// <see cref="Luddite.Combat.BasicWeapon"/>이 발사 시점에 읽는다 — None(미선택)이면
    /// null을 돌려주고 무기는 기본 탄(FireballBig + 전공색 틴트)을 유지한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Luddite/SubMajor Bullet Set", fileName = "SubMajorBulletSet")]
    public class SubMajorBulletSetSO : ScriptableObject
    {
        [Header("문과")]
        [SerializeField] private Sprite _linguistics;       // 어문계 — 펜/연필
        [SerializeField] private Sprite _commerce;          // 상경계 — 돈
        [SerializeField] private Sprite _law;               // 법조계 — 책/두루마리

        [Header("이과")]
        [SerializeField] private Sprite _naturalScience;    // 자연과학 — 숫자/수학 기호
        [SerializeField] private Sprite _engineering;       // 공학 — 기계/번개
        [SerializeField] private Sprite _computerScience;   // 컴퓨터과학 — 컴퓨터

        [Header("예체능")]
        [SerializeField] private Sprite _physicalEducation; // 체육 — 공
        [SerializeField] private Sprite _fineArts;          // 미술 — 붓
        [SerializeField] private Sprite _music;             // 음악 — 음표

        /// <summary>세부전공의 탄막 스프라이트. None·미지정이면 null (기본 탄 유지).</summary>
        public Sprite SpriteOf(SubMajor subMajor)
        {
            switch (subMajor)
            {
                case SubMajor.Linguistics: return _linguistics;
                case SubMajor.Commerce: return _commerce;
                case SubMajor.Law: return _law;
                case SubMajor.NaturalScience: return _naturalScience;
                case SubMajor.Engineering: return _engineering;
                case SubMajor.ComputerScience: return _computerScience;
                case SubMajor.PhysicalEducation: return _physicalEducation;
                case SubMajor.FineArts: return _fineArts;
                case SubMajor.Music: return _music;
                default: return null;
            }
        }
    }
}
