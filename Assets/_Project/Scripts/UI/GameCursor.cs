using UnityEngine;

namespace Luddite.UI
{
    /// <summary>
    /// 인게임 마우스 커서 (D7 신규 — 사람 요청). OS 기본 화살표 대신 팩 커서를 쓴다.
    ///
    /// <para><b>핫스팟이 커서 그림의 뾰족한 끝과 정확히 맞아야 한다.</b> 조준이 마우스 위치로
    /// 이뤄지는 게임이라, 핫스팟이 어긋나면 "보이는 곳"과 "쏘는 곳"이 달라 보인다.
    /// (실제 조준 좌표는 커서 그림과 무관하게 OS 포인터 위치로 계산되므로 게임 로직은 멀쩡하지만,
    /// 플레이어는 그림을 믿기 때문에 어긋나면 조준이 밀린 것처럼 느낀다.)</para>
    ///
    /// <para><see cref="CursorMode.Auto"/> = 하드웨어 커서. WebGL에서도 동작하고
    /// 프레임 드랍과 무관하게 따라온다 (소프트웨어 커서는 게임 프레임에 묶여 끊긴다).</para>
    /// </summary>
    public class GameCursor : MonoBehaviour
    {
        [Tooltip("커서 텍스처. Read/Write Enabled + 무압축이어야 한다 (빌더가 설정)")]
        [SerializeField] private Texture2D _cursorTexture;

        [Tooltip("커서 그림의 뾰족한 끝 픽셀 (좌상단 원점). 빌더가 실측해 채운다")]
        [SerializeField] private Vector2 _hotspot = Vector2.zero;

        private void Awake()
        {
            Apply();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // 알트탭 등으로 포커스가 돌아오면 OS가 기본 커서로 되돌리는 경우가 있다
            if (hasFocus) Apply();
        }

        private void Apply()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            if (_cursorTexture != null) Cursor.SetCursor(_cursorTexture, _hotspot, CursorMode.Auto);
        }

        private void OnDisable()
        {
            // 에디터에서 플레이 모드를 나갈 때 커서가 남지 않도록 원복
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
