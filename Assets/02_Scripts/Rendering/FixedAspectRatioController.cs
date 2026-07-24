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

    private void LateUpdate()
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

        float currentAspect = (float)lastScreenWidth / lastScreenHeight;
        Rect rect = CalculateViewportRect(currentAspect);

        foreach (Camera camera in Camera.allCameras)
        {
            if (camera == null || camera == clearCamera || camera.targetTexture != null)
            {
                continue;
            }

            camera.rect = rect;
        }
    }

    private static Rect CalculateViewportRect(float currentAspect)
    {
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
}
