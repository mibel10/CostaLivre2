Shader "CostaLivre/CoastalRain"
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

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            float4 _CostaMistColor;

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float distanceToCamera = distance(input.positionWS, _WorldSpaceCameraPos);
                half edge = saturate(1 - abs(input.uv.x * 2 - 1));
                half lengthFade = smoothstep(0, 0.28, input.uv.y) * smoothstep(0, 0.32, 1 - input.uv.y);
                half alpha = edge * lengthFade * smoothstep(0.4, 2.6, distanceToCamera) *
                    (1 - smoothstep(17, 26, distanceToCamera));
                return half4(_CostaMistColor.rgb * 0.75 + 0.06, alpha * 0.44);
            }
            ENDHLSL
        }
    }
}
