using UnityEngine;
using Luddite.Core;

namespace Luddite.Player
{
    /// <summary>
    /// 플레이어 스프라이트 구동 (뷰 전용). <see cref="PlayerController"/>를 <b>읽기만</b> 한다.
    ///
    /// <para>
    /// 적처럼 속도로 자동 구동하지 않고 별도 어댑터를 두는 이유는 <b>방향의 기준이 다르기</b> 때문이다 —
    /// 플레이어는 마우스로 조준하므로(GDD §3.1) 몸이 향해야 하는 쪽은 이동 방향이 아니라 조준 방향이다.
    /// 왼쪽으로 피하면서 오른쪽을 쏘는 것이 이 게임의 기본 동작(회피 심리전 §7)이라,
    /// 이동 방향으로 몸을 돌리면 "무엇을 쏘고 있는지"가 화면에서 사라진다.
    /// </para>
    /// </summary>
    public class PlayerSpriteView : MonoBehaviour
    {
        [SerializeField] private PlayerController _controller;
        [SerializeField] private DirectionalSpriteAnimator _animator;

        [Tooltip("이 입력 크기 미만이면 정지로 본다. 연출값")]
        [SerializeField] private float _moveThreshold = 0.01f;

        private void Awake()
        {
            if (_controller == null) _controller = GetComponentInParent<PlayerController>();
            if (_animator == null) _animator = GetComponentInChildren<DirectionalSpriteAnimator>();

            if (_controller == null) Debug.LogError("[PlayerSpriteView] PlayerController를 찾지 못함", this);
            if (_animator == null) Debug.LogError("[PlayerSpriteView] DirectionalSpriteAnimator를 찾지 못함", this);
        }

        private void Update()
        {
            if (_controller == null || _animator == null) return;

            _animator.SetFacing(_controller.AimDirection);
            _animator.Play(_controller.MoveInput.sqrMagnitude >= _moveThreshold * _moveThreshold
                ? DirectionalSpriteAnimator.CLIP_WALK
                : DirectionalSpriteAnimator.CLIP_IDLE);
        }
    }
}
