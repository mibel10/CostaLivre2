Shader "CostaLivre/CoastalWater"
{
    Properties
    {
        _Deep ("Deep water", Color) = (0.025, 0.29, 0.40, 1)
        _Shallow ("Shallow turquoise", Color) = (0.08, 0.64, 0.67, 1)
        _Sky ("Sky reflection", Color) = (0.30, 0.71, 0.86, 1)
        _Daylight ("Daylight", Range(0, 1)) = 1
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
                half4 _Shallow;
                half4 _Sky;
                half _Daylight;
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
                float2 waterPosition = input.positionWS.xz;
                float time = _Time.y;
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float distanceFade = 1.0 - smoothstep(16.0, 100.0, distance(_WorldSpaceCameraPos, input.positionWS));
                float2 slope = float2(0.049, 0.031) * cos(dot(waterPosition, float2(1.1, 0.7)) - time * 1.18);
                slope += float2(-0.035, 0.059) * cos(dot(waterPosition, float2(-0.8, 1.35)) + time * 1.52);
                slope += distanceFade * float2(0.064, -0.028) * cos(dot(waterPosition, float2(4.8, -2.1)) + time * 2.45);
                float3 normal = normalize(input.normalWS + float3(slope.x, 0, slope.y));
                half fresnel = 0.04h + 0.87h * pow(1.0h - saturate(dot(normal, view)), 4.0h);
                Light sun = GetMainLight();
                half alignment = saturate(dot(reflect(-view, normal), sun.direction));
                half sparkle = 0.32h * pow(alignment, 32.0h) + 0.52h * pow(alignment, 110.0h);
                half tint = 0.92h + 0.10h * sin(dot(waterPosition, float2(0.44, -0.23)) + time * 0.35);
                half shallow = 1.0h - smoothstep(23.5h, 46.0h, (half)waterPosition.x);
                half3 waterBase = lerp(_Deep.rgb, _Shallow.rgb, shallow * 0.85h) * tint;
                half shoreFoam = (1.0h - smoothstep(24.2h, 26.8h, (half)waterPosition.x))
                    * smoothstep(0.25h, 0.85h, (half)(0.5 + 0.5 * sin(waterPosition.y * 1.4 + time * 1.9 + slope.x * 8.0)));
                half3 color = lerp(waterBase, _Sky.rgb, fresnel * 0.82h) + sun.color * sparkle + half3(0.86h, 0.94h, 0.95h) * (shoreFoam * 0.24h);
                color *= lerp(half3(0.14h, 0.24h, 0.42h), half3(1, 1, 1), _Daylight);
                return half4(MixFog(color, input.fog), 1.0h);
            }
            ENDHLSL
        }
    }
}
