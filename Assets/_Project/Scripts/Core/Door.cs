using UnityEngine;
using Luddite.Combat;

namespace Luddite.Core
{
    /// <summary>
    /// 방 출입문 (개정안 §2). 잠기면 통행·탄환을 모두 막고, 열리면 통과시킨다.
    ///
    /// <para><b>🔴 전멸형 종료 계약의 방 단위 번역:</b> 문은 배정 적 전멸 시에만 열린다.
    /// 시간 경과로 열리는 경로를 절대 만들지 말 것 — 시간제 종료는 "버티기"를 최적해로 만들어
    /// 회피 데이터 공급을 오염시킨다.</para>
    ///
    /// <para>스프라이트 4프레임(`Door_Front_0..3`)을 닫힘→열림 단계로 쓴다.
    /// 애니메이션은 하지 않고 상태별 프레임만 교체한다 (연출 예산 절약).</para>
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Door : MonoBehaviour
    {
        [Tooltip("잠김 상태에서 통행·탄환을 막는 콜라이더")]
        [SerializeField] private BoxCollider2D _blocker;

        [Tooltip("닫힘 프레임 (Door_*_0)")]
        [SerializeField] private Sprite _closedSprite;

        [Tooltip("열림 프레임 (Door_*_3)")]
        [SerializeField] private Sprite _openSprite;

        private SpriteRenderer _renderer;
        private bool _locked = true;

        public bool IsLocked => _locked;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (_blocker == null) _blocker = GetComponent<BoxCollider2D>();
            Apply();
        }

        /// <summary>방 진입 시 잠금. 재플레이(RunStarted)에서도 이 경로로 복구된다.</summary>
        public void Lock()
        {
            _locked = true;
            Apply();
        }

        /// <summary>배정 적 전멸 시에만 호출된다 (🔴 계약).</summary>
        public void Unlock()
        {
            _locked = false;
            Apply();
        }

        private void Apply()
        {
            if (_blocker != null) _blocker.enabled = _locked;
            if (_renderer != null)
            {
                Sprite s = _locked ? _closedSprite : _openSprite;
                if (s != null) _renderer.sprite = s;
            }
            // 잠김 = 탄환도 막는다. ProjectileBlocker는 마커라 붙였다 뗐다 하지 않고
            // 콜라이더 활성만으로 제어한다 (Projectile이 GetComponentInParent로 찾는다)
            var marker = GetComponent<ProjectileBlocker>();
            if (marker != null) marker.enabled = true;
        }
    }
}
