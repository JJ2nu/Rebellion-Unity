Shader "Hidden/Rebellion/Soft Vignette"
{
    Properties
    {
        _SoftVignetteOpacity ("Opacity", Range(0, 1)) = 0.65
        _SoftVignetteRoundness ("Roundness", Range(0, 1)) = 0.75
        _SoftVignetteFeather ("Feather", Range(0.001, 2)) = 1.1
        _SoftVignetteSize ("Size", Range(0.1, 2)) = 1.35
        _SoftVignetteCenter ("Center", Vector) = (0.5, 0.5, 0, 0)
        _SoftVignetteAspectRatio ("Aspect Ratio", Range(0.25, 4)) = 1
        _SoftVignetteColor ("Color", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "SoftVignette"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _SoftVignetteOpacity;
                float _SoftVignetteRoundness;
                float _SoftVignetteFeather;
                float _SoftVignetteSize;
                float4 _SoftVignetteCenter;
                float _SoftVignetteAspectRatio;
                float4 _SoftVignetteColor;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);

                // Roundness가 1이면 타원, 0이면 둥근 사각형에 가까운 superellipse 거리를 사용한다.
                float2 position = abs((input.texcoord - _SoftVignetteCenter.xy) * 2.0);
                position.x *= _SoftVignetteAspectRatio;
                float exponent = lerp(12.0, 2.0, saturate(_SoftVignetteRoundness));
                float distanceFromCenter = pow(pow(position.x, exponent) + pow(position.y, exponent), 1.0 / exponent);

                // Feather 폭 안에서만 부드럽게 감쇠시켜 참조 이미지 같은 넓은 그라데이션을 만든다.
                float innerEdge = _SoftVignetteSize - _SoftVignetteFeather;
                float edgeFactor = smoothstep(innerEdge, _SoftVignetteSize, distanceFromCenter);
                float vignetteFactor = edgeFactor * saturate(_SoftVignetteOpacity);

                sceneColor.rgb = lerp(sceneColor.rgb, sceneColor.rgb * _SoftVignetteColor.rgb, vignetteFactor);
                return sceneColor;
            }
            ENDHLSL
        }
    }
}
