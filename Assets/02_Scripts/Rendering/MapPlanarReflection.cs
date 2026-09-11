using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 맵 바닥 평면에 인물·가변 오브젝트의 거울 반사를 그린다.
/// 원본 카메라의 뷰에 반사 행렬을 곱한 숨김 카메라를 렌더 루프 밖(LateUpdate)에서
/// <see cref="UniversalRenderPipeline.SingleCameraRequest"/>로 RenderTexture에 그리고,
/// 그 결과를 <see cref="overlayRenderer"/>의 재질(Rebellion/MapPlanarReflectionOverlay)에 넘긴다.
/// 맵 Prefab이 활성일 때만 동작하며 비활성화 시 카메라·RT·재질 인스턴스를 모두 정리한다.
/// </summary>
public sealed class MapPlanarReflection : MonoBehaviour
{
    private const string ReflectionTextureProperty = "_ReflectionTex";

    [Header("Overlay")]
    [Tooltip("반사 결과를 화면에 섞을 투명 평면 Renderer. 재질은 Rebellion/MapPlanarReflectionOverlay를 사용한다.")]
    [SerializeField] private Renderer overlayRenderer;
    [Tooltip("반사 평면의 월드 높이. 오버레이 평면과 같은 값을 둔다.")]
    [SerializeField] private float planeHeight = 0.12f;
    [Tooltip("반사 카메라의 클립 평면을 반사 평면보다 이만큼 위로 올려 평면과 겹치는 표면을 제외한다.")]
    [SerializeField, Min(0f)] private float clipPlaneOffset = 0.03f;

    [Header("Reflection Camera")]
    [SerializeField, Min(64)] private int textureWidth = 640;
    [SerializeField, Min(64)] private int textureHeight = 360;
    [Tooltip("반사 카메라가 그리지 않을 레이어. UI와 오버레이/섀도 캐처가 놓인 TransparentFX를 기본으로 제외한다.")]
    [SerializeField] private LayerMask excludedLayers = (1 << 5) | (1 << 1);

    private Camera sourceCamera;
    private Camera reflectionCamera;
    private RenderTexture reflectionTexture;
    private Material overlayMaterial;
    private Material originalOverlayMaterial;
    private bool isRendering;

    /// <summary>진단용. 마지막으로 그린 반사 RenderTexture.</summary>
    public RenderTexture ReflectionTexture => reflectionTexture;

    private void OnEnable()
    {
        if (overlayRenderer == null)
        {
            Debug.LogWarning("[MapPlanarReflection] overlayRenderer가 연결되지 않아 반사를 끕니다.", this);
            enabled = false;
            return;
        }

        // 활성 게임 카메라는 맵 활성화 시점에 한 번만 찾아 캐시한다. 매 프레임 검색하지 않는다.
        sourceCamera = Camera.main;

        reflectionTexture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.DefaultHDR)
        {
            name = "MapPlanarReflectionRT",
            useMipMap = false,
            autoGenerateMips = false,
            filterMode = FilterMode.Bilinear
        };
        reflectionTexture.Create();

        GameObject cameraObject = new GameObject("MapPlanarReflectionCamera")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        cameraObject.transform.SetParent(transform, false);
        reflectionCamera = cameraObject.AddComponent<Camera>();
        reflectionCamera.enabled = false;
        reflectionCamera.useOcclusionCulling = false;
        reflectionCamera.allowMSAA = false;
        reflectionCamera.allowHDR = true;

        // 후처리·그림자·불필요한 중간 텍스처를 꺼 반사 렌더 비용을 줄인다.
        UniversalAdditionalCameraData cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderType = CameraRenderType.Base;
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = false;
        cameraData.requiresColorOption = CameraOverrideOption.Off;
        cameraData.requiresDepthOption = CameraOverrideOption.Off;
        cameraData.antialiasing = AntialiasingMode.None;
        cameraData.allowXRRendering = false;

        // 오버레이 재질은 인스턴스로 복제해 공유 재질에 RT 참조를 남기지 않는다.
        originalOverlayMaterial = overlayRenderer.sharedMaterial;
        overlayMaterial = new Material(originalOverlayMaterial)
        {
            name = originalOverlayMaterial.name + " (PlanarReflection)"
        };
        overlayMaterial.SetTexture(ReflectionTextureProperty, reflectionTexture);
        overlayRenderer.sharedMaterial = overlayMaterial;
    }

    private void OnDisable()
    {
        if (overlayRenderer != null && originalOverlayMaterial != null)
        {
            overlayRenderer.sharedMaterial = originalOverlayMaterial;
        }

        if (overlayMaterial != null)
        {
            Destroy(overlayMaterial);
            overlayMaterial = null;
        }

        if (reflectionCamera != null)
        {
            Destroy(reflectionCamera.gameObject);
            reflectionCamera = null;
        }

        if (reflectionTexture != null)
        {
            reflectionTexture.Release();
            Destroy(reflectionTexture);
            reflectionTexture = null;
        }

        sourceCamera = null;
    }

    private void LateUpdate()
    {
        if (sourceCamera == null)
        {
            // 카메라가 나중에 생성되는 경우를 위한 재시도. 찾을 때까지만 반복된다.
            sourceCamera = Camera.main;
            if (sourceCamera == null)
            {
                return;
            }
        }

        if (reflectionCamera == null || reflectionTexture == null || isRendering)
        {
            return;
        }

        RenderReflection();
    }

    private void RenderReflection()
    {
        CopyCameraSettings(sourceCamera, reflectionCamera);

        Vector3 planeNormal = Vector3.up;
        Vector3 planePosition = new Vector3(0f, planeHeight, 0f);
        float planeDistance = -Vector3.Dot(planeNormal, planePosition);
        Matrix4x4 reflectionMatrix = CalculateReflectionMatrix(new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, planeDistance));

        // 원본 뷰 행렬에 반사를 곱하면 평면 위의 점은 같은 화면 좌표에 놓인다.
        reflectionCamera.worldToCameraMatrix = sourceCamera.worldToCameraMatrix * reflectionMatrix;
        reflectionCamera.transform.position = reflectionMatrix.MultiplyPoint(sourceCamera.transform.position);
        reflectionCamera.transform.rotation = Quaternion.LookRotation(
            reflectionMatrix.MultiplyVector(sourceCamera.transform.forward),
            reflectionMatrix.MultiplyVector(sourceCamera.transform.up));

        // 평면 아래(반사 카메라 기준 평면 뒤)의 물체를 잘라내는 사선 투영.
        reflectionCamera.projectionMatrix = sourceCamera.projectionMatrix;
        Vector4 clipPlane = CameraSpacePlane(reflectionCamera, planePosition + planeNormal * clipPlaneOffset, planeNormal);
        reflectionCamera.projectionMatrix = reflectionCamera.CalculateObliqueMatrix(clipPlane);

        UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest
        {
            destination = reflectionTexture
        };

        if (!RenderPipeline.SupportsRenderRequest(reflectionCamera, request))
        {
            return;
        }

        // 반사 행렬은 좌우 방향을 뒤집으므로 컬링 방향을 반대로 두고 그린다.
        isRendering = true;
        bool previousInvertCulling = GL.invertCulling;
        GL.invertCulling = !previousInvertCulling;
        try
        {
            RenderPipeline.SubmitRenderRequest(reflectionCamera, request);
        }
        finally
        {
            GL.invertCulling = previousInvertCulling;
            isRendering = false;
        }
    }

    private void CopyCameraSettings(Camera source, Camera destination)
    {
        destination.clearFlags = source.clearFlags == CameraClearFlags.Skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        destination.backgroundColor = source.backgroundColor;
        destination.orthographic = source.orthographic;
        destination.orthographicSize = source.orthographicSize;
        destination.fieldOfView = source.fieldOfView;
        destination.nearClipPlane = source.nearClipPlane;
        destination.farClipPlane = source.farClipPlane;
        destination.aspect = source.aspect;
        destination.cullingMask = source.cullingMask & ~excludedLayers.value;
    }

    private static Vector4 CameraSpacePlane(Camera camera, Vector3 position, Vector3 normal)
    {
        Matrix4x4 worldToCamera = camera.worldToCameraMatrix;
        Vector3 cameraPosition = worldToCamera.MultiplyPoint(position);
        Vector3 cameraNormal = worldToCamera.MultiplyVector(normal).normalized;
        return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -Vector3.Dot(cameraPosition, cameraNormal));
    }

    private static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
    {
        Matrix4x4 reflection = Matrix4x4.identity;
        reflection.m00 = 1f - 2f * plane[0] * plane[0];
        reflection.m01 = -2f * plane[0] * plane[1];
        reflection.m02 = -2f * plane[0] * plane[2];
        reflection.m03 = -2f * plane[3] * plane[0];

        reflection.m10 = -2f * plane[1] * plane[0];
        reflection.m11 = 1f - 2f * plane[1] * plane[1];
        reflection.m12 = -2f * plane[1] * plane[2];
        reflection.m13 = -2f * plane[3] * plane[1];

        reflection.m20 = -2f * plane[2] * plane[0];
        reflection.m21 = -2f * plane[2] * plane[1];
        reflection.m22 = 1f - 2f * plane[2] * plane[2];
        reflection.m23 = -2f * plane[3] * plane[2];
        return reflection;
    }
}
