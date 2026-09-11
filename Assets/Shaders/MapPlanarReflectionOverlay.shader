// 맵 바닥 위 평면 반사 오버레이 셰이더.
// MapPlanarReflection이 매 프레임 그린 반사 RenderTexture를 화면 좌표로 샘플링해
// 투명 GridCell 위에 약하게 섞는다. 반사 카메라는 원본 카메라의 뷰에 반사 행렬을 곱하므로
// 평면 위의 점은 두 카메라에서 같은 화면 좌표에 놓인다.
Shader "Rebellion/MapPlanarReflectionOverlay"
{
    Properties
    {
        _ReflectionTex ("Reflection Texture", 2D) = "black" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Strength ("Strength", Range(0, 1)) = 0.3
        _EdgeFade ("Edge Fade (UV)", Range(0.001, 0.5)) = 0.06
        [Toggle] _FlipY ("Flip Y", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+60"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "MapPlanarReflectionOverlay"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Strength;
                float _EdgeFade;
                float _FlipY;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-5);
                // 플랫폼별 RenderTexture 상하 방향 차이는 재질의 Flip Y로 보정한다.
                screenUV.y = lerp(screenUV.y, 1.0 - screenUV.y, _FlipY);
                half3 reflection = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, screenUV).rgb;

                // 평면 가장자리를 부드럽게 줄여 보드 경계에 반사 절단선이 생기지 않게 한다.
                float2 edgeDistance = min(input.uv, 1.0 - input.uv);
                half edgeFade = saturate(min(edgeDistance.x, edgeDistance.y) / _EdgeFade);

                return half4(reflection * _Tint.rgb, _Strength * edgeFade);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
