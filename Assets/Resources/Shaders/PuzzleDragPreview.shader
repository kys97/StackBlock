Shader "StackBlock/Puzzle Drag Preview"
{
    Properties
    {
        _Color ("Piece Color", Color) = (1,1,1,1)
        _MainTex ("Piece Texture", 2D) = "white" {}
        _Opacity ("Preview Opacity", Range(0,1)) = 0.55
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off
        CGPROGRAM
        #pragma surface surf Lambert alpha:fade noshadow
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _Opacity;
        struct Input { float2 uv_MainTex; };
        void surf(Input input, inout SurfaceOutput output)
        {
            fixed4 color = tex2D(_MainTex, input.uv_MainTex) * _Color;
            output.Albedo = color.rgb;
            output.Alpha = color.a * _Opacity;
        }
        ENDCG
    }
    FallBack Off
}
