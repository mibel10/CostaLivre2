Shader "CostaLivre/ImpactParticle"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float3 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }
            half4 Fragment(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float falloff = saturate(1 - dot(p, p));
                float cloud = smoothstep(0, 0.78, falloff) *
                    (0.83 + 0.17 * sin(p.x * 8 + p.y * 5) * sin(p.y * 9 - p.x * 3));
                float chip = smoothstep(0, .22, 1 - max(abs(p.x + .33 * p.y), abs(p.y - .2 * p.x)));
                float form = lerp(cloud, chip, smoothstep(.35, .7, input.color.a));
                float hot = step(input.color.b, input.color.r * .34) * step(.51, input.color.a);
                return half4(input.color.rgb * (1 + .58 * hot), input.color.a * form);
            }
            ENDHLSL
        }
    }
}
