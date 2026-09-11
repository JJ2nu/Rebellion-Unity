// Map_Bar 전용 섀도 캐처 셰이더.
// 투명 GridCell 위에 얇은 평면을 깔고, 주광원(Directional Mixed)의 실시간 그림자만 어둡게 합성한다.
// 색은 출력하지 않고 그림자 강도만 알파로 내보내므로 타일·화살표의 기존 표시는 그대로 보인다.
Shader "Rebellion/MapShadowCatcher"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.05, 0.03, 0.02, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.55
        _EdgeFade ("Edge Fade (UV)", Range(0.001, 0.5)) = 0.06
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "MapShadowCatcher"
            Tags { "LightMode" = "UniversalForward" }

            // 검은색을 그림자 강도만큼 섞는다. 깊이는 쓰지 않아 위에 그려지는 반투명 표시를 막지 않는다.
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 주광원 그림자 관련 variant만 포함한다. 추가 광원 그림자는 비용 절약을 위해 받지 않는다.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShadowColor;
                float _ShadowStrength;
                float _EdgeFade;
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
                float3 positionWS : TEXCOORD0;
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
                output.positionWS = positionInputs.positionWS;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 주광원의 실시간 섀도맵만 샘플링한다. 섀도마스크(베이크 정적 그림자)는 라이트맵 바닥이 담당한다.
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                half shadowAttenuation = MainLightRealtimeShadow(shadowCoord);
                half occlusion = (1.0h - shadowAttenuation) * _ShadowStrength;

                // 평면 가장자리를 부드럽게 줄여 보드 경계에 그림자 절단선이 생기지 않게 한다.
                float2 edgeDistance = min(input.uv, 1.0 - input.uv);
                half edgeFade = saturate(min(edgeDistance.x, edgeDistance.y) / _EdgeFade);

                return half4(_ShadowColor.rgb, occlusion * edgeFade);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
