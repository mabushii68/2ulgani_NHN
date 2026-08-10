using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Luddite.Combat;

namespace Luddite.UI
{
    /// <summary>
    /// 우하단 무기·탄약 표시 (D7 신규 — 사람 요청, 엔터 더 건전 방식).
    ///
    /// <para><b>읽기 전용이다</b> — <see cref="IWeapon"/>의 게터만 본다. UI가 무기를 수정하지 않는다
    /// (CLAUDE.md 규칙 7). 무기를 갈아끼워도(D8 전공별 무기) 인터페이스만 지키면 그대로 동작한다.</para>
    ///
    /// <para>표시: 무기 아이콘 + <c>남은 발수 / 탄창</c>. 재장전 중에는 문구가 <c>RELOADING</c>으로 바뀌고
    /// 아이콘 아래 진행 게이지가 찬다. 탄약 총량은 무한이라 "탄이 떨어졌다"는 상태는 없다.</para>
    /// </summary>
    public class AmmoCounter : MonoBehaviour
    {
        [Tooltip("탄약을 읽어올 무기. 비워 두면 Player 태그에서 IWeapon을 찾는다")]
        [SerializeField] private MonoBehaviour _weaponSource;

        [SerializeField] private TMP_Text _countLabel;
        [SerializeField] private Image _weaponIcon;

        [Tooltip("재장전 진행 게이지. pivot이 왼쪽이어야 왼→오로 찬다")]
        [SerializeField] private RectTransform _reloadFill;

        [Header("연출값")]
        [Tooltip("탄창이 이 비율 이하로 남으면 경고색")]
        [Range(0f, 1f)] [SerializeField] private float _lowAmmoRatio = 0.25f;

        [SerializeField] private Color _normalColor = new Color(0.92f, 0.92f, 0.95f, 1f);
        [SerializeField] private Color _lowColor = new Color(1f, 0.72f, 0.30f, 1f);

        [Tooltip("재장전 중 아이콘 알파 — 못 쏘는 상태임을 색으로도 알린다")]
        [Range(0f, 1f)] [SerializeField] private float _reloadingIconAlpha = 0.45f;

        private IWeapon _weapon;

        private void Awake()
        {
            _weapon = _weaponSource as IWeapon;
            if (_weapon == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) _weapon = player.GetComponentInChildren<IWeapon>();
            }
            if (_weapon == null) Debug.LogWarning("[AmmoCounter] IWeapon을 찾지 못함 — 표시가 멈춘다", this);
        }

        private void LateUpdate()
        {
            if (_weapon == null) return;

            bool reloading = _weapon.IsReloading;

            if (_countLabel != null)
            {
                // AI 발화가 아니라 플레이어 장비 상태다. 다만 숫자 위주라 영문 유지 (§10.5 판단)
                _countLabel.text = reloading
                    ? "RELOADING"
                    : _weapon.AmmoRemaining + " / " + _weapon.MagazineSize;

                int mag = Mathf.Max(1, _weapon.MagazineSize);
                bool low = !reloading && _weapon.AmmoRemaining <= mag * _lowAmmoRatio;
                _countLabel.color = low ? _lowColor : _normalColor;
            }

            if (_weaponIcon != null)
            {
                Color c = _weaponIcon.color;
                c.a = reloading ? _reloadingIconAlpha : 1f;
                _weaponIcon.color = c;
            }

            if (_reloadFill != null)
            {
                _reloadFill.gameObject.SetActive(reloading);
                if (reloading)
                {
                    Vector3 s = _reloadFill.localScale;
                    s.x = _weapon.ReloadProgress01;
                    _reloadFill.localScale = s;
                }
            }
        }
    }
}
