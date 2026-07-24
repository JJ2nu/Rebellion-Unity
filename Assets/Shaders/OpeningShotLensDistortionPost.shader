Shader "Hidden/Rebellion/Opening Shot Lens Distortion Post"
{
    Properties
    {
        _OpeningShotLensCenter ("Center", Vector) = (0.5, 0.5, 0, 0)
        _OpeningShotLensRadius ("Radius", Range(0, 1)) = 0.46
        _OpeningShotLensEdgeWidth ("Edge Width", Range(0.001, 0.5)) = 0.16
        _OpeningShotLensStrength ("Strength", Range(-0.25, 0.25)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "OpeningShotLensDistortion"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OpeningShotLensCenter;
                float _OpeningShotLensRadius;
                float _OpeningShotLensEdgeWidth;
                float _OpeningShotLensStrength;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 direction = input.texcoord - _OpeningShotLensCenter.xy;
                float distanceFromCenter = length(direction);
                float edgeStart = max(0.0, _OpeningShotLensRadius - _OpeningShotLensEdgeWidth);
                float edgeFactor = smoothstep(edgeStart, _OpeningShotLensRadius, distanceFromCenter);
                float2 normalizedDirection = direction / max(distanceFromCenter, 0.00001);

                // 렌즈 중심은 유지하고 원 가장자리로 갈수록 샘플 위치를 안쪽으로 당겨 굴절감을 만든다.
                float2 warpedUv = saturate(input.texcoord - normalizedDirection * _OpeningShotLensStrength * edgeFactor);
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, warpedUv);
            }
            ENDHLSL
        }
    }
}
