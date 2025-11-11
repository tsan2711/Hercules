Shader "Unlit/Pawn"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _RimColor ("Rim Color", Color) = (0.5, 0.8, 1.0, 1.0)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
        _RimIntensity ("Rim Intensity", Range(0.0, 5.0)) = 2.0
        _EmissionStrength ("Emission Strength", Range(0.0, 5.0)) = 1.0
        _PulseSpeed ("Pulse Speed", Range(0.0, 5.0)) = 2.0
        _PulseAmplitude ("Pulse Amplitude", Range(0.0, 1.0)) = 0.3
        
        // Dissolve properties
        [Header(Dissolve Effect)]
        _DissolveAmount ("Dissolve Amount", Range(0.0, 1.0)) = 0.0
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0.0, 0.5)) = 0.1
        _DissolveEdgeIntensity ("Dissolve Edge Intensity", Range(0.0, 5.0)) = 2.0
        _DissolveEdgeColor ("Dissolve Edge Color", Color) = (1.0, 0.5, 0.0, 1.0)
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _NoiseScale ("Noise Scale", Range(0.1, 10.0)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        // Enable alpha blending for dissolve effect
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite On

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
                float3 worldPos : TEXCOORD4;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _RimColor;
            float _RimPower;
            float _RimIntensity;
            float _EmissionStrength;
            float _PulseSpeed;
            float _PulseAmplitude;
            
            // Dissolve properties
            float _DissolveAmount;
            float _DissolveEdgeWidth;
            float _DissolveEdgeIntensity;
            float4 _DissolveEdgeColor;
            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;
            float _NoiseScale;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                // Calculate world normal and view direction for rim lighting
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(UnityWorldSpaceViewDir(o.worldPos));
                
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Calculate rim lighting effect
                float3 worldNormal = normalize(i.worldNormal);
                float3 viewDirection = normalize(i.viewDir);
                
                // Rim lighting calculation - fresnel effect
                float rimDot = 1.0 - saturate(dot(worldNormal, viewDirection));
                float rimEffect = pow(rimDot, _RimPower) * _RimIntensity;
                
                // Simple pulsing animation effect
                float pulse = sin(_Time.y * _PulseSpeed) * _PulseAmplitude + 1.0;
                
                // Apply rim color with emission
                float3 rimEmission = _RimColor.rgb * rimEffect * _EmissionStrength * pulse;
                
                // Combine base color with rim lighting
                col.rgb += rimEmission;
                
                // Dissolve effect
                float dissolveValue = 0.0;
                float edgeEffect = 0.0;
                
                if (_DissolveAmount > 0.0)
                {
                    // Sample noise texture for organic dissolve pattern (or use procedural noise)
                    float2 noiseUV = i.worldPos.xy * _NoiseScale;
                    float noise = 0.0;
                    
                    // Procedural noise using world position (works without texture)
                    float2 p = noiseUV;
                    float2 grid = floor(p);
                    float2 f = frac(p);
                    f = f * f * (3.0 - 2.0 * f); // Smoothstep for smoother noise
                    float n = grid.x + grid.y * 57.0;
                    float4 hash = float4(n, n + 1.0, n + 57.0, n + 58.0);
                    hash = frac(sin(hash) * 43758.5453);
                    float4 lerpHash = lerp(lerp(hash.x, hash.y, f.x), lerp(hash.z, hash.w, f.x), f.y);
                    noise = lerpHash.x;
                    
                    // Try to sample noise texture and blend if available
                    float4 noiseTexSample = tex2D(_NoiseTex, noiseUV);
                    if (noiseTexSample.a > 0.0) // If texture has alpha, use it
                    {
                        noise = lerp(noise, noiseTexSample.r, 0.5); // Blend procedural and texture noise
                    }
                    
                    // Create dissolve pattern using noise
                    float dissolvePattern = noise;
                    
                    // Calculate dissolve threshold
                    float threshold = _DissolveAmount;
                    
                    // Calculate edge effect
                    float edgeStart = threshold - _DissolveEdgeWidth;
                    float edgeEnd = threshold;
                    
                    // Edge glow effect
                    if (dissolvePattern > edgeStart && dissolvePattern < edgeEnd)
                    {
                        float edgeFactor = (dissolvePattern - edgeStart) / _DissolveEdgeWidth;
                        edgeFactor = saturate(edgeFactor);
                        edgeEffect = pow(edgeFactor, 0.5) * _DissolveEdgeIntensity;
                        col.rgb += _DissolveEdgeColor.rgb * edgeEffect;
                    }
                    
                    // Clip pixels that are dissolved
                    if (dissolvePattern < threshold)
                    {
                        clip(-1.0);
                    }
                    
                    // Fade out alpha as dissolve progresses
                    float fadeStart = threshold - _DissolveEdgeWidth * 2.0;
                    if (dissolvePattern > fadeStart && dissolvePattern < threshold)
                    {
                        float fadeFactor = (dissolvePattern - fadeStart) / (threshold - fadeStart);
                        fadeFactor = saturate(fadeFactor);
                        col.a *= fadeFactor;
                    }
                }
                
                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    
    // Fallback shader if dissolve is not needed
    FallBack "Diffuse"
}
