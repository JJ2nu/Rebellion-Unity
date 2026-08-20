Shader "Custom/OutlineHull"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.8, 0, 1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.05)) = 0.015
        // 아래 렌더 상태 값들은 OutlineEffect가 런타임 머티리얼 인스턴스에 주입한다.
        // 기본값은 스텐실을 사용하지 않는 기존 단독 헐 패스와 동일하게 동작한다.
        [IntRange] _StencilRef ("Stencil Ref", Range(0, 255)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Op", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 1
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.ColorWriteMask)] _ColorMask ("Color Mask", Float) = 15
        _OffsetFactor ("Depth Offset Factor", Float) = 0
        _OffsetUnits ("Depth Offset Units", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+1"
        }

        Pass
        {
            Name "OutlineHull"
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest LEqual
            ColorMask [_ColorMask]
            Offset [_OffsetFactor], [_OffsetUnits]

            // 마스크 인스턴스: 유닛 실루엣 픽셀에 참조값을 기록 (Comp Always / Pass Replace)
            // 헐 인스턴스: 실루엣 바깥에만 외곽선을 그림 (Comp NotEqual / Pass Keep)
            Stencil
            {
                Ref [_StencilRef]
                Comp [_StencilComp]
                Pass [_StencilOp]
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 expandedPos = IN.positionOS.xyz + normalize(IN.normalOS) * _OutlineWidth;
                OUT.positionHCS = TransformObjectToHClip(expandedPos);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(_OutlineColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
