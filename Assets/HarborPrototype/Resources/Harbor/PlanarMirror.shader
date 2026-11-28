Shader "Costa/PlanarMirror"
{
    Properties
    {
        [NoScaleOffset] _ReflectionTexture ("Planar reflection", 2D) = "black" {}
        _ReflectionAmount ("Capture available", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+16" }
        Pass
        {
            Cull Back
            Name "PlanarMirror"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ReflectionTexture); SAMPLER(sampler_ReflectionTexture);
            CBUFFER_START(UnityPerMaterial)
                float4x4 _ReflectionViewProjection;
                half _ReflectionAmount;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 reflection : TEXCOORD0; float2 uv : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(world);
                output.reflection = mul(_ReflectionViewProjection, float4(world, 1));
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half bevel = smoothstep(.01, .085, min(min(input.uv.x, 1 - input.uv.x), min(input.uv.y, 1 - input.uv.y)));
                half3 silver = lerp(half3(.037, .067, .083), half3(.17, .24, .27), input.uv.y);
                if (_ReflectionAmount < .5 || input.reflection.w <= 0)
                    return half4(silver * lerp(.65, 1, bevel), 1);
                float2 uv = input.reflection.xy / input.reflection.w * .5 + .5;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1 - uv.y;
                #endif
                half coverage = step(0, uv.x) * step(0, uv.y) * step(uv.x, 1) * step(uv.y, 1);
                half3 reflection = SAMPLE_TEXTURE2D(_ReflectionTexture, sampler_ReflectionTexture, saturate(uv)).rgb;
                return half4(lerp(silver, reflection * half3(.92, .96, .98), coverage * bevel * .96), 1);
            }
            ENDHLSL
        }
    }
}
