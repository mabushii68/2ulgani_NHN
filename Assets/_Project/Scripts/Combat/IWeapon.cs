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

        // ── 탄창 상태 (D7 신규). HUD가 **읽기만** 한다 — UI가 무기를 수정하지 않는다 (CLAUDE.md 규칙 7).
        //    무기 교체(D8 전공별 무기)를 대비해 BasicWeapon이 아니라 이 인터페이스에 둔다 (규칙 6).

        /// <summary>탄창 1개당 발수.</summary>
        int MagazineSize { get; }

        /// <summary>현재 탄창에 남은 발수.</summary>
        int AmmoRemaining { get; }

        /// <summary>재장전 중인지. 이 동안 <see cref="CanFire"/>는 false다.</summary>
        bool IsReloading { get; }

        /// <summary>재장전 진행률 0~1 (재장전 중이 아니면 0). 게이지 연출용.</summary>
        float ReloadProgress01 { get; }

        /// <summary>
        /// 지금 실제로 나가는 탄의 스프라이트 (HUD 아이콘용). 없으면 null.
        /// <b>판단은 무기가 한다</b> — 세부전공 선택 여부에 따른 교체 규칙이 무기와 UI 두 곳에
        /// 흩어지면 어긋난다. UI는 결과만 읽는다 (CLAUDE.md 규칙 7).
        /// </summary>
        Sprite CurrentBulletSprite { get; }

        /// <summary>위 스프라이트에 적용할 틴트. 세부전공 탄은 원색(흰색), 기본 탄은 전공색.</summary>
        Color CurrentBulletColor { get; }
    }
}
