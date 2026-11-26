Shader "CostaLivre/CoastalSky"
{
    Properties
    {
        _SkyTint ("Zenith", Color) = (0.34, 0.47, 0.66, 1)
        _GroundColor ("Horizon", Color) = (0.56, 0.61, 0.68, 1)
        _Exposure ("Sky radiance", Float) = 1
        _SunSize ("Solar disk", Float) = 0.02
        _CloudCover ("Cloud cover", Float) = 0
        _CloudDrift ("Cloud optical drift", Float) = 0
        _SunDirection ("Sun direction", Vector) = (0, 1, 0, 0)
        _SunRadiance ("Sun radiance", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SkyTint, _GroundColor, _SunRadiance;
                float4 _SunDirection;
                float _Exposure, _SunSize, _CloudCover, _CloudDrift;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float Noise(float2 cloudUV)
            {
                float2 cell = floor(cloudUV);
                float2 f = frac(cloudUV);
                f = f * f * (3 - 2 * f);
                float4 x = frac((cell.x + float4(0, 1, 0, 1)) * 0.1031);
                float4 y = frac((cell.y + float4(0, 0, 1, 1)) * 0.1099);
                float4 mixed = x * (y + 31.37) + y * (x + 21.91);
                float4 h = frac((x + y + 2 * mixed) * (y + mixed));
                return lerp(lerp(h.x, h.y, f.x), lerp(h.z, h.w, f.x), f.y);
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                float3 ray = normalize(input.direction);
                float altitude = max(0, ray.y);
                float sunAngle = saturate(dot(ray, _SunDirection.xyz));
                float horizon = exp(-altitude * 5.3);
                float3 sky = lerp(_SkyTint.rgb, _GroundColor.rgb, horizon) * _Exposure;
                float visibility = smoothstep(-0.02, 0.08, _SunDirection.y);
                float disk = smoothstep(1 - _SunSize * _SunSize * 1.2, 1 - _SunSize * _SunSize * 0.47, sunAngle);
                sky += _SunRadiance.rgb * visibility * (disk * 3.2 + pow(sunAngle, 18) * 0.105);

                if (ray.y > 0.012 && _CloudCover > 0.015)
                {
                    float2 cloudUV = ray.xz * (2.2 / max(altitude, 0.07)) + _CloudDrift * float2(0.22, 0.08);
                    float threshold = lerp(0.76, 0.34, _CloudCover);
                    float transmittance = 1;
                    float3 scattered = 0;
                    // Short optical integration through a high cloud slab.
                    [unroll] for (int i = 0; i < 3; i++)
                    {
                        float2 samplePoint = cloudUV * (1 + i * 0.08) + float2(0.07, 0.12) * i;
                        float shape = Noise(samplePoint) * 0.64 + Noise(samplePoint * 2.13) * 0.26 +
                            Noise(samplePoint * 4.53) * 0.10;
                        float density = smoothstep(threshold, threshold + 0.26, shape);
                        float through = exp(-density * lerp(0.63, 1.44, _CloudCover));
                        float3 incident = lerp(_GroundColor.rgb * 0.40, _GroundColor.rgb * 0.82, i / 2.0) *
                            _Exposure + _SunRadiance.rgb * visibility * pow(sunAngle, 9) * (1 - _CloudCover * 0.72) * 0.22;
                        scattered += transmittance * (1 - through) * incident;
                        transmittance *= through;
                    }
                    sky = lerp(sky, sky * transmittance + scattered, smoothstep(0.012, 0.15, altitude));
                }
                return float4(sky, 1);
            }
            ENDHLSL
        }
    }
}
