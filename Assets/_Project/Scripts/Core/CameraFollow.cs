using UnityEngine;

namespace Luddite.Core
{
    /// <summary>
    /// 플레이어 추적 카메라 (v1.1 개정 계약 — 2026-08-08 사람 확정).
    ///
    /// <para><b>🔴 계약: 축별 조건부 클램프.</b> 방 범위가 화면보다 <b>큰 축만</b> 클램프하고,
    /// 작은 축은 방 중심에 고정한다. 방보다 화면이 큰데 클램프하면 min &gt; max로 좌표가 역전되기
    /// 때문이다. 이 구조 덕에 방 크기를 바꿔도 카메라 코드는 손대지 않는다 — 방이 화면보다
    /// 커지는 순간 해당 축의 추적이 자동으로 살아난다.</para>
    ///
    /// <para>고정 카메라 계약(v1.0)은 폐기됐다. 화면 밖 위협 보정(가장자리 화살표)은
    /// 추적 카메라의 <b>동반 계약</b>이다 — TODO(보정): 오프스크린 화살표 미구현.</para>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [Tooltip("추적 대상. 비워 두면 Player 태그를 찾는다")]
        [SerializeField] private Transform _target;

        [Tooltip("추적 부드러움(초). 연출 타이밍이라 SO 아닌 인스펙터 노출 — 규칙 2의 명시적 예외")]
        [SerializeField] private float _smoothTime = 0.12f;

        [Header("현재 방 (던전 모드에서는 DungeonManager가 SetRoom으로 갱신)")]
        [SerializeField] private Vector2 _roomCenter = Vector2.zero;

        [Tooltip("방 내부 반경(유닛). 아레나 32×18 → (16, 9)")]
        [SerializeField] private Vector2 _roomHalfExtents = new Vector2(16f, 9f);

        [Tooltip("방 경계 너머로 더 따라가는 여유(유닛). 0이면 플레이어가 벽에 붙었을 때 화면 " +
                 "가장자리에 못 박혀 문·복도가 화면 밖으로 나간다. 방 밖을 비추므로 암반 배경이 필요하다")]
        [SerializeField] private float _edgePeek = 6f;

        private Camera _camera;
        private Vector3 _velocity;

        /// <summary>방 전환 시 호출 (던전 모드). 즉시 스냅하지 않고 부드럽게 따라간다.</summary>
        public void SetRoom(Vector2 center, Vector2 halfExtents)
        {
            _roomCenter = center;
            _roomHalfExtents = halfExtents;
        }

        /// <summary>방 전환 컷 등에서 보간 없이 즉시 맞출 때.</summary>
        public void SnapToTarget()
        {
            _velocity = Vector3.zero;
            transform.position = ComputeDesired();
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) _target = player.transform;
            }
            if (_target == null) Debug.LogWarning("[CameraFollow] 추적 대상 없음 — 카메라가 정지한다", this);
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            transform.position = Vector3.SmoothDamp(transform.position, ComputeDesired(), ref _velocity, _smoothTime);
        }

        private Vector3 ComputeDesired()
        {
            if (_target == null) return transform.position;

            // 카메라가 보는 반경. orthographicSize는 세로 반경이고 가로는 aspect를 곱한다
            float viewHalfHeight = _camera.orthographicSize;
            float viewHalfWidth = viewHalfHeight * _camera.aspect;

            float x = ClampAxis(_target.position.x, _roomCenter.x, _roomHalfExtents.x + _edgePeek, viewHalfWidth);
            float y = ClampAxis(_target.position.y, _roomCenter.y, _roomHalfExtents.y + _edgePeek, viewHalfHeight);
            return new Vector3(x, y, transform.position.z);   // z(카메라 깊이)는 절대 건드리지 않는다
        }

        /// <summary>
        /// 방(+여유)이 화면보다 큰 축만 추적한다. 작으면 방 중심 고정 —
        /// 이 분기가 없으면 Mathf.Clamp(min &gt; max)가 되어 카메라가 튄다.
        /// </summary>
        /// <param name="boundHalf">방 반경 + <see cref="_edgePeek"/>. 여유가 없으면 벽에 붙은 플레이어가
        /// 화면 가장자리에 고정되어 진행 방향(문·복도)이 보이지 않는다.</param>
        private static float ClampAxis(float targetValue, float center, float boundHalf, float viewHalf)
        {
            float slack = boundHalf - viewHalf;
            if (slack <= 0f) return center;
            return Mathf.Clamp(targetValue, center - slack, center + slack);
        }
    }
}
