using UnityEngine;

/// <summary>
/// 빌드 Player 시작 시 항상 모니터 기본 해상도의 테두리 없는 전체화면(FullScreenWindow)을 강제한다.
/// Unity Windows Player는 마지막 화면 모드를 레지스트리에 저장해 다음 실행에 재사용하므로,
/// 창 크기 조절이 허용된 상태에서 한 번 창/최대화 상태가 저장되면 ProjectSettings의
/// 기본 전체화면 설정이 무시된 채 계속 창모드로 실행되는 문제를 막는다.
/// </summary>
public static class StartupFullscreenEnforcer
{
    // 첫 Scene이 그려지기 전에 실행해 타이틀 화면부터 전체화면으로 표시한다.
    // 전처리기 제외 대신 런타임 가드를 사용해 Editor 컴파일에서도 코드가 검증되게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnforceFullscreen()
    {
        // Editor에서는 Game View 크기를 건드리지 않도록 아무 동작도 하지 않는다.
        if (Application.isEditor || Application.platform == RuntimePlatform.WebGLPlayer)
        {
            return;
        }

        // systemWidth/Height는 데스크톱(모니터 기본) 해상도라 울트라와이드에서도 화면 전체를 덮고,
        // 16:9 콘텐츠 영역과 검정 여백은 기존 FixedAspectRatioController가 처리한다.
        Screen.SetResolution(
            Display.main.systemWidth,
            Display.main.systemHeight,
            FullScreenMode.FullScreenWindow);
    }
}
