Shader "CostaLivre/CoastalWater"
{
    Properties
    {
        _Deep ("Deep water", Color) = (0.038, 0.15, 0.18, 1)
        _Sky ("Sky reflection", Color) = (0.25, 0.43, 0.48, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Deep;
                half4 _Sky;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fog : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 point = input.positionWS.xz;
                float time = _Time.y;
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float distanceFade = 1.0 - smoothstep(16.0, 100.0, distance(_WorldSpaceCameraPos, input.positionWS));
                float2 slope = float2(0.049, 0.031) * cos(dot(point, float2(1.1, 0.7)) - time * 1.18);
                slope += float2(-0.035, 0.059) * cos(dot(point, float2(-0.8, 1.35)) + time * 1.52);
                slope += distanceFade * float2(0.064, -0.028) * cos(dot(point, float2(4.8, -2.1)) + time * 2.45);
                float3 normal = normalize(input.normalWS + float3(slope.x, 0, slope.y));
                half fresnel = 0.04h + 0.87h * pow(1.0h - saturate(dot(normal, view)), 4.0h);
                Light sun = GetMainLight();
                half alignment = saturate(dot(reflect(-view, normal), sun.direction));
                half sparkle = 0.28h * pow(alignment, 36.0h) + 0.42h * pow(alignment, 120.0h);
                half tint = 0.9h + 0.11h * sin(dot(point, float2(0.44, -0.23)) + time * 0.35);
                half3 color = lerp(_Deep.rgb * tint, _Sky.rgb, fresnel) + sun.color * sparkle;
                return half4(MixFog(color, input.fog), 1.0h);
            }
            ENDHLSL
        }
    }
}
