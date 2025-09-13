Shader "Unlit/Pawn"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _RimColor ("Rim Color", Color) = (0.5, 0.8, 1.0, 1.0)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
        _RimIntensity ("Rim Intensity", Range(0.0, 5.0)) = 2.0
        _EmissionStrength ("Emission Strength", Range(0.0, 10.0)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _RimColor;
            float _RimPower;
            float _RimIntensity;
            float _EmissionStrength;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                // Calculate world normal and view direction for rim lighting
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(UnityWorldSpaceViewDir(worldPos));
                
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Calculate rim lighting effect
                float3 worldNormal = normalize(i.worldNormal);
                float3 viewDirection = normalize(i.viewDir);
                
                // Rim lighting calculation - fresnel effect
                float rimDot = 1.0 - saturate(dot(worldNormal, viewDirection));
                float rimEffect = pow(rimDot, _RimPower) * _RimIntensity;
                
                // Apply rim color with emission - Enhanced for bloom
                float3 rimEmission = _RimColor.rgb * rimEffect * _EmissionStrength;
                
                // Boost emission values for HDR bloom effect
                rimEmission *= (1.0 + rimEffect * 2.0); // Multiply by up to 3x for bright areas
                
                // Combine base color with rim lighting
                col.rgb += rimEmission;
                
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
