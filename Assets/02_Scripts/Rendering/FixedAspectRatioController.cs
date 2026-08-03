using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
public sealed class FixedAspectRatioController : MonoBehaviour
{
    private const float TargetAspect = 16f / 9f;

    private static FixedAspectRatioController instance;
    private Camera clearCamera;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private int lastCameraCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        var go = new GameObject(nameof(FixedAspectRatioController));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<FixedAspectRatioController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureClearCamera();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Update()
    {
        ApplyAspectRatioIfNeeded();
    }

    private void LateUpdate()
    {
        ApplyAspectRatioIfNeeded();
    }

    private void ApplyAspectRatioIfNeeded()
    {
        if (lastScreenWidth == Screen.width &&
            lastScreenHeight == Screen.height &&
            lastCameraCount == Camera.allCamerasCount)
        {
            return;
        }

        ApplyAspectRatio();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyAspectRatio();
    }

    private void EnsureClearCamera()
    {
        if (clearCamera != null)
        {
            return;
        }

        var clearCameraObject = new GameObject("Aspect Ratio Clear Camera");
        clearCameraObject.transform.SetParent(transform);
        clearCamera = clearCameraObject.AddComponent<Camera>();
        clearCamera.clearFlags = CameraClearFlags.SolidColor;
        clearCamera.backgroundColor = Color.black;
        clearCamera.cullingMask = 0;
        clearCamera.depth = -10000f;
        clearCamera.useOcclusionCulling = false;
        clearCamera.allowHDR = false;
        clearCamera.allowMSAA = false;
    }

    private void ApplyAspectRatio()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastCameraCount = Camera.allCamerasCount;

        if (lastScreenWidth <= 0 || lastScreenHeight <= 0)
        {
            return;
        }

        EnsureClearCamera();

        Rect rect = GetViewportRect(lastScreenWidth, lastScreenHeight);

        foreach (Camera camera in Camera.allCameras)
        {
            if (camera == null || camera == clearCamera || camera.targetTexture != null)
            {
                continue;
            }

            camera.rect = rect;
        }
    }

    /// <summary>
    /// 현재 출력 크기 안에서 16:9 콘텐츠가 차지할 정규화 viewport를 반환한다.
    /// Camera, 화면 UI와 입력이 같은 계산을 공유해 검정 여백 경계가 어긋나지 않게 한다.
    /// </summary>
    public static Rect GetViewportRect(int screenWidth, int screenHeight)
    {
        if (screenWidth <= 0 || screenHeight <= 0)
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        float currentAspect = (float)screenWidth / screenHeight;
        if (Mathf.Approximately(currentAspect, TargetAspect))
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        if (currentAspect > TargetAspect)
        {
            float width = TargetAspect / currentAspect;
            return new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }

        float height = currentAspect / TargetAspect;
        return new Rect(0f, (1f - height) * 0.5f, 1f, height);
    }

    /// <summary>
    /// 현재 화면에서 실제 플레이 콘텐츠가 렌더링되는 픽셀 영역을 반환한다.
    /// </summary>
    public static Rect GetCurrentPixelRect()
    {
        Rect viewportRect = GetViewportRect(Screen.width, Screen.height);
        return new Rect(
            viewportRect.x * Screen.width,
            viewportRect.y * Screen.height,
            viewportRect.width * Screen.width,
            viewportRect.height * Screen.height);
    }

    /// <summary>
    /// 포인터가 검정 여백이 아닌 16:9 플레이 영역 안에 있는지 확인한다.
    /// </summary>
    public static bool ContainsScreenPoint(Vector2 screenPoint)
    {
        return GetCurrentPixelRect().Contains(screenPoint);
    }
}
