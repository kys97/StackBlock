// Opaque placement markers. Depth testing stays enabled; only face culling
// is configurable, so markers do not draw through already placed pieces.
Shader "StackBlock/Puzzle Surface"
{
    Properties
    {
        _Color ("Surface Color", Color) = (0,0,0,1)
        _MainTex ("Surface Texture", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]
        ZWrite On
        ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            struct Vertex { float4 position : POSITION; float2 uv : TEXCOORD0; };
            struct Fragment { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            Fragment vert(Vertex input)
            {
                Fragment output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }
            fixed4 frag(Fragment input) : SV_Target
            {
                return tex2D(_MainTex, input.uv) * _Color;
            }
            ENDCG
        }
    }
    FallBack Off
}
