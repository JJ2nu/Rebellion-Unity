Shader "Rebellion/UI/Weathered HUD"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [MaterialToggle] _ZWrite ("ZWrite", Float) = 0
        _WearStrength ("Dust and Wear", Range(0, 1)) = 0.55
        _Desaturation ("Desaturation", Range(0, 1)) = 0.28
        _DustTint ("Earth Tint", Color) = (0.68, 0.59, 0.45, 1)
        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _DustTint;
                half _WearStrength;
                half _Desaturation;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            float DustHash(float2 coordinates)
            {
                return frac(sin(dot(coordinates, float2(127.1, 311.7))) * 43758.5453);
            }

            float DustNoise(float2 coordinates)
            {
                float2 cell = floor(coordinates);
                float2 fraction = frac(coordinates);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);
                return lerp(lerp(DustHash(cell), DustHash(cell + float2(1, 0)), fraction.x),
                    lerp(DustHash(cell + float2(0, 1)), DustHash(cell + float2(1, 1)), fraction.x),
                    fraction.y);
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                half4 result = CommonUnlitFragment(input, input.color);
                half luminance = dot(result.rgb, half3(0.2126, 0.7152, 0.0722));
                result.rgb = lerp(result.rgb, luminance.xxx, _Desaturation);

                // 질감은 Sprite UV에 고정해 카메라나 시간이 바뀌어도 흔들리지 않는다.
                half patches = DustNoise(input.uv * 13.0 + float2(5.7, 2.3));
                half grain = DustNoise(input.uv * 105.0);
                half scratches = (1.0 - smoothstep(0.015, 0.055,
                    abs(frac(input.uv.y * 65.0 + input.uv.x * 17.0) - 0.5))) *
                    step(0.78, DustHash(floor(input.uv * float2(9.0, 38.0))));

                result.rgb *= lerp(half3(1, 1, 1), _DustTint.rgb,
                    _WearStrength * (0.12 + patches * 0.36));
                result.rgb *= 1.0 - _WearStrength * (grain * 0.14 + scratches * 0.24);
                // 알파와 원래 문양은 유지한다. 밝은 광택이나 새 테두리는 더하지 않는다.
                return result;
            }
            ENDHLSL
        }
    }
}
