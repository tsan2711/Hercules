Shader "Unlit/TileHighlight"
{
    Properties
    {
        _HighlightTex ("Highlight Mask (white = on)", 2D) = "black" {}
        _GlowColor ("Glow Color", Color) = (0.2, 0.8, 1.0, 1.0)
        _GlowStrength ("Glow Strength", Range(0, 3)) = 1.5
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 2.0
        _PulseAmplitude ("Pulse Amplitude", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _HighlightTex;
            float4 _GlowColor;
            float _GlowStrength;
            float _PulseSpeed;
            float _PulseAmplitude;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float mask = tex2D(_HighlightTex, i.uv).r;
                
                if (mask <= 0.001)
                    return fixed4(0, 0, 0, 0);

                float pulse = 1.0 + _PulseAmplitude * sin(_Time.y * _PulseSpeed);
                float glow = mask * _GlowStrength * pulse;

                fixed4 col;
                col.rgb = _GlowColor.rgb * glow;
                col.a = glow * 0.6;
                return col;
            }
            ENDCG
        }
    }
}
