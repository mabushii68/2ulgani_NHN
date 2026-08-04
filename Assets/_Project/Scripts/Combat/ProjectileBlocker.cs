using UnityEngine;

namespace Luddite.Combat
{
    /// <summary>
    /// 이 컴포넌트가 붙은 오브젝트에 닿은 투사체는 데미지 없이 소멸한다 (아레나 벽 등).
    /// TODO(레이어 정리): 본래는 전용 Wall 레이어 + Physics2D 충돌 매트릭스로 처리해야 한다.
    /// 레이어 추가는 ProjectSettings 변경이라 사람 승인 대상이므로, 그때까지 이 마커로 대체한다.
    /// </summary>
    public class ProjectileBlocker : MonoBehaviour
    {
    }
}
