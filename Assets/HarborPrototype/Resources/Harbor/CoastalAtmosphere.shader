Shader "Hidden/CostaLivre/CoastalAtmosphere"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D(_CostaExposureHistory);
        TEXTURE2D(_CostaExposure);
        TEXTURE2D(_CostaScattering);
        float4 _CostaMist, _CostaMistColor, _CostaMeterParams;

        float3 SceneColor(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
        }

        float EyeDepth(float2 uv)
        {
            return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
        }

        float3 WorldPoint(float2 uv)
        {
            float depth = SampleSceneDepth(uv);
            #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
            #endif
            return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
        }

        float4 Meter(Varyings input) : SV_Target
        {
            float total = 0, weight = 0;
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 12; x++)
            {
                float2 uv = (float2(x, y) + 0.5) / float2(12, 8);
                float importance = 1 - 0.85 * saturate(dot(uv - 0.5, uv - 0.5) * 2);
                float luminance = dot(max(0, SceneColor(uv)), float3(0.2126, 0.7152, 0.0722));
                total += log2(clamp(luminance, 0.014, 6)) * importance;
                weight += importance;
            }
            float targetEV = clamp(log2(0.24) - total / weight, -0.65, 1.15);
            if (_CostaMeterParams.y < 0.5)
                return targetEV;

            float previousEV = LOAD_TEXTURE2D(_CostaExposureHistory, int2(0, 0)).r;
            float speed = targetEV < previousEV ? 2.9 : 1.25;
            return lerp(previousEV, targetEV, 1 - exp(-_CostaMeterParams.x * speed));
        }

        float4 Composite(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;
            float3 scene = SceneColor(uv);
            if (_CostaMeterParams.x > 0.5)
            {
                // Bilateral upsampling avoids halos where the half resolution fog crosses silhouettes.
                float2 texel = 2 * rcp(_ScaledScreenParams.xy);
                float2 pixel = uv / texel - 0.5;
                float2 lower = floor(pixel);
                float2 mixWeight = frac(pixel);
                float depth = EyeDepth(uv);
                float4 sum = 0;
                float weights = 0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float2 offset = float2(i & 1, i >> 1);
                    float2 tap = saturate((lower + offset + 0.5) * texel);
                    float2 bilinear = lerp(1 - mixWeight, mixWeight, offset);
                    float weight = max(0.001, bilinear.x * bilinear.y) *
                        exp2(-abs(EyeDepth(tap) - depth) / max(0.35, depth * 0.04));
                    sum += SAMPLE_TEXTURE2D_LOD(_CostaScattering, sampler_PointClamp, tap, 0) * weight;
                    weights += weight;
                }
                float4 volume = weights > 0.0001 ? sum / weights :
                    SAMPLE_TEXTURE2D_LOD(_CostaScattering, sampler_PointClamp, uv, 0);
                scene = scene * volume.a + volume.rgb;
            }
            float exposure = exp2(LOAD_TEXTURE2D(_CostaExposure, int2(0, 0)).r);
            return float4(scene * exposure, 1);
        }
        ENDHLSL

        Pass
        {
            Name "Luminance"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Meter
            ENDHLSL
        }
        Pass
        {
            Name "Scattering"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Lighting
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"

            float _CostaFogLightCount;
            float4 _CostaFogLights[2];
            float4 _CostaFogLightColor;

            float3 LampScattering(float3 point)
            {
                float radiance = 0;
                [loop] for (int i = 0; i < (int)_CostaFogLightCount; i++)
                {
                    float3 offset = point - _CostaFogLights[i].xyz;
                    float squared = dot(offset, offset);
                    float range = _CostaFogLights[i].w;
                    float rangeFade = saturate(1 - squared / (range * range));
                    float direction = -offset.y * rsqrt(max(0.001, squared));
                    float cone = smoothstep(_CostaFogLightColor.w, 0.866, direction);
                    radiance += cone * rangeFade * rangeFade / max(1, squared);
                }
                return _CostaFogLightColor.rgb * (radiance * 0.42);
            }

            float3 SurfaceNormal(float2 uv, float3 center, float eyeDepth)
            {
                float2 step = rcp(_ScaledScreenParams.xy);
                float2 left = uv - float2(step.x, 0), right = uv + float2(step.x, 0);
                float2 down = uv - float2(0, step.y), up = uv + float2(0, step.y);
                float3 dx = abs(EyeDepth(left) - eyeDepth) < abs(EyeDepth(right) - eyeDepth) ?
                    center - WorldPoint(left) : WorldPoint(right) - center;
                float3 dy = abs(EyeDepth(down) - eyeDepth) < abs(EyeDepth(up) - eyeDepth) ?
                    center - WorldPoint(down) : WorldPoint(up) - center;
                float3 normal = SafeNormalize(cross(dx, dy));
                return dot(normal, _WorldSpaceCameraPos - center) < 0 ? -normal : normal;
            }

            float3 DiffuseBounce(float2 uv, float3 surface, float eyeDepth)
            {
                float3 receiver = SurfaceNormal(uv, surface, eyeDepth);
                float2 radius = float2(_ScaledScreenParams.y / _ScaledScreenParams.x, 1) *
                    min(0.085, 1.1 / max(eyeDepth, 1));
                float3 irradiance = 0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float angle = (i + 0.23) * 1.570796;
                    float2 tap = uv + float2(cos(angle), sin(angle)) * radius;
                    if (any(tap <= 0.002) || any(tap >= 0.998))
                        continue;
                    float sampleDepth = EyeDepth(tap);
                    if (sampleDepth >= _ProjectionParams.z * 0.99)
                        continue;
                    float3 emitter = WorldPoint(tap);
                    float3 offset = emitter - surface;
                    float separation = length(offset);
                    if (separation < 0.15 || separation > 5)
                        continue;
                    float3 direction = offset / separation;
                    float receive = saturate(dot(receiver, direction));
                    if (receive <= 0)
                        continue;
                    float3 sourceNormal = SurfaceNormal(tap, emitter, sampleDepth);
                    float emit = saturate(dot(sourceNormal, -direction));
                    float middleDepth = EyeDepth((uv + tap) * 0.5);
                    float expectedDepth = (2 * sampleDepth * eyeDepth) / max(sampleDepth + eyeDepth, 0.001);
                    float visibility = smoothstep(expectedDepth - 0.45, expectedDepth - 0.12, middleDepth);
                    irradiance += min(3.0, SceneColor(tap)) *
                        (receive * emit * visibility / (1 + separation * separation));
                }
                return irradiance * min(0.75, SceneColor(uv)) * 0.14;
            }

            float4 Lighting(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float3 point = WorldPoint(uv);
                float3 ray = point - _WorldSpaceCameraPos;
                float distanceToSurface = length(ray);
                float3 direction = ray / max(distanceToSurface, 0.001);
                int steps = (int)_CostaMist.w;
                float rayLength = min(distanceToSurface, 190.0);
                float segment = rayLength / max(steps, 1);
                Light sunlight = GetMainLight();
                float cosine = dot(direction, sunlight.direction);
                const float g = 0.53;
                float phase = (1 - g * g) * rsqrt(pow(max(0.04, 1 + g * g - 2 * g * cosine), 3));
                float3 airlight = 0;
                float transmittance = 1;
                float jitter = InterleavedGradientNoise(input.positionCS.xy, 0);
                [loop] for (int index = 0; index < steps; index++)
                {
                    float3 samplePoint = _WorldSpaceCameraPos + direction * ((index + lerp(0.18, 0.82, jitter)) * segment);
                    float density = _CostaMist.y * exp(-max(0, samplePoint.y) * _CostaMist.z);
                    float through = exp(-density * segment);
                    half shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(samplePoint));
                    float shadowFade = GetMainLightShadowFade(samplePoint);
                    shadow = lerp(shadow, 1, shadowFade);
                    float3 light = _CostaMistColor.rgb * 0.67 + sunlight.color * (0.095 * phase * shadow);
                    if (_CostaFogLightCount > 0)
                        light += LampScattering(samplePoint);
                    airlight += transmittance * (1 - through) * light;
                    transmittance *= through;
                }
                float depth = EyeDepth(uv);
                if (_CostaMeterParams.x > 0.5 && depth < min(100, _ProjectionParams.z * 0.99))
                    airlight += transmittance * DiffuseBounce(uv, point, depth);
                return float4(airlight, transmittance);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            ENDHLSL
        }
    }
}
