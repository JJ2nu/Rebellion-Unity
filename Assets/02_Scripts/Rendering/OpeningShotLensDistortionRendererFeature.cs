using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 현재 렌더링 중인 카메라에 OpeningShotLensDistortionController가 있을 때만 렌즈 왜곡 Pass를 추가한다.
/// 카메라별 값은 공용 Material을 수정하지 않고 실제 Draw 호출의 MaterialPropertyBlock으로 전달한다.
/// </summary>
public sealed class OpeningShotLensDistortionRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Material material;

    private OpeningShotLensDistortionPass lensDistortionPass;

    public override void Create()
    {
        lensDistortionPass = new OpeningShotLensDistortionPass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;

        if (material == null || camera == null)
        {
            return;
        }

        if (renderingData.cameraData.cameraType == CameraType.Preview ||
            renderingData.cameraData.cameraType == CameraType.Reflection)
        {
            return;
        }

        if (!camera.TryGetComponent(out OpeningShotLensDistortionController controller) ||
            !controller.isActiveAndEnabled ||
            !controller.EffectEnabled)
        {
            return;
        }

        lensDistortionPass.Setup(material, controller);
        renderer.EnqueuePass(lensDistortionPass);
    }

    private sealed class OpeningShotLensDistortionPass : ScriptableRenderPass
    {
        private const string PassName = "Opening Shot Lens Distortion";
        private const string CopyPassName = "Copy Color for Opening Shot Lens Distortion";

        private static readonly int BlitTextureProperty = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBiasProperty = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int CenterProperty = Shader.PropertyToID("_OpeningShotLensCenter");
        private static readonly int RadiusProperty = Shader.PropertyToID("_OpeningShotLensRadius");
        private static readonly int EdgeWidthProperty = Shader.PropertyToID("_OpeningShotLensEdgeWidth");
        private static readonly int StrengthProperty = Shader.PropertyToID("_OpeningShotLensStrength");
        private static readonly MaterialPropertyBlock SharedPropertyBlock = new MaterialPropertyBlock();

        private Material passMaterial;
        private Vector2 center;
        private float radius;
        private float edgeWidth;
        private float strength;

        public void Setup(Material material, OpeningShotLensDistortionController controller)
        {
            passMaterial = material;
            center = controller.Center;
            radius = controller.Radius;
            edgeWidth = controller.EdgeWidth;
            strength = controller.Strength;

            // 현재 화면을 텍스처로 읽어야 하므로 URP가 카메라 중간 컬러 버퍼를 준비하게 한다.
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer || passMaterial == null)
            {
                return;
            }

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc sourceDescription = renderGraph.GetTextureDesc(source);
            sourceDescription.name = "_OpeningShotLensDistortionSource";
            sourceDescription.clearBuffer = false;
            TextureHandle copiedSource = renderGraph.CreateTexture(sourceDescription);

            // 원본과 출력 버퍼를 분리해 같은 텍스처를 동시에 읽고 쓰는 GPU 의존성 문제를 피한다.
            renderGraph.AddBlitPass(source, copiedSource, Vector2.one, Vector2.zero, passName: CopyPassName);

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out PassData passData))
            {
                passData.source = copiedSource;
                passData.material = passMaterial;
                passData.center = center;
                passData.radius = radius;
                passData.edgeWidth = edgeWidth;
                passData.strength = strength;

                builder.UseTexture(passData.source, AccessFlags.Read);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    SharedPropertyBlock.Clear();
                    SharedPropertyBlock.SetTexture(BlitTextureProperty, data.source);
                    SharedPropertyBlock.SetVector(BlitScaleBiasProperty, new Vector4(1f, 1f, 0f, 0f));
                    SharedPropertyBlock.SetVector(CenterProperty, new Vector4(data.center.x, data.center.y, 0f, 0f));
                    SharedPropertyBlock.SetFloat(RadiusProperty, data.radius);
                    SharedPropertyBlock.SetFloat(EdgeWidthProperty, data.edgeWidth);
                    SharedPropertyBlock.SetFloat(StrengthProperty, data.strength);

                    context.cmd.DrawProcedural(
                        Matrix4x4.identity,
                        data.material,
                        0,
                        MeshTopology.Triangles,
                        3,
                        1,
                        SharedPropertyBlock);
                });
            }
        }

        private sealed class PassData
        {
            public TextureHandle source;
            public Material material;
            public Vector2 center;
            public float radius;
            public float edgeWidth;
            public float strength;
        }
    }
}
