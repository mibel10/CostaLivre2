Shader "Costa Livre/City Sign"
{
    Properties { _MainTex ("Font atlas", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct Input { float4 position : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            Output Vert(Input input)
            {
                Output result;
                result.position = UnityObjectToClipPos(input.position);
                result.uv = input.uv;
                result.color = input.color;
                return result;
            }
            fixed4 Frag(Output input) : SV_Target
            {
                return fixed4(input.color.rgb, input.color.a * tex2D(_MainTex, input.uv).a);
            }
            ENDCG
        }
    }
}
