Shader "Rebellion/VFX/Window Light Shaft"
{
    Properties
    {
        [HDR] _BaseColor ("Light Color", Color) = (2.5, 1.1, 0.25, 0.1)
        _Opacity ("Opacity", Range(0, 1)) = 0.1
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.22
        _LengthFade ("Length Fade", Range(0.1, 4)) = 1.4
        _NoiseScale ("Noise Scale", Float) = 1.8
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.18
        _ScrollSpeed ("Scroll Speed", Float) = 0.035
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "WindowLightShaft"
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Opacity;
                half _EdgeSoftness;
                half _LengthFade;
                half _NoiseScale;
                half _NoiseStrength;
                half _ScrollSpeed;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1, 0));
                float c = Hash21(cell + float2(0, 1));
                float d = Hash21(cell + float2(1, 1));
                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half leftFade = smoothstep(0.0h, _EdgeSoftness, input.uv.x);
                half rightFade = smoothstep(0.0h, _EdgeSoftness, 1.0h - input.uv.x);
                half edgeFade = leftFade * rightFade;
                half distanceFade = pow(saturate(1.0h - input.uv.y), _LengthFade);
                distanceFade = lerp(0.04h, 1.0h, distanceFade);

                float2 noiseUv = input.positionWS.xz * _NoiseScale;
                noiseUv += _Time.y * _ScrollSpeed;
                half noise = ValueNoise(noiseUv);
                noise = lerp(1.0h, lerp(0.65h, 1.0h, noise), _NoiseStrength);

                half alpha = _BaseColor.a * _Opacity * edgeFade * distanceFade * noise;
                return half4(_BaseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
