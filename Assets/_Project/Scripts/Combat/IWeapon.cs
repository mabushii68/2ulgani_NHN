using UnityEngine;

namespace Luddite.Combat
{
    /// <summary>
    /// 무기 계약. PlayerController에 강결합 금지 (CLAUDE.md 규칙 6) —
    /// D8 전공별 최종 무기 교체는 이 인터페이스를 구현한 컴포넌트를 갈아끼우는 것으로 처리한다.
    /// 연사 타이밍(쿨다운)은 무기가 소유한다. 소유자는 "쏘고 싶다"만 알린다.
    /// </summary>
    public interface IWeapon
    {
        /// <summary>쿨다운이 끝나 지금 발사 가능한지.</summary>
        bool CanFire { get; }

        /// <summary>쿨다운 갱신. 소유자가 매 프레임 호출한다.</summary>
        void Tick(float deltaTime);

        /// <summary>발사. <see cref="CanFire"/>가 false면 아무 일도 하지 않는다.</summary>
        /// <param name="origin">발사 기준 위치 (보통 소유자 중심)</param>
        /// <param name="aimDirection">조준 방향(정규화)</param>
        void Fire(Vector2 origin, Vector2 aimDirection);
    }
}
