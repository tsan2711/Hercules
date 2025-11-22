Shader "Custom/DissolveIn"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture (Optional)", 2D) = "gray" {}
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _EdgeWidth ("Edge Width", Range(0, 0.3)) = 0.1
        _EdgeColor ("Edge Color", Color) = (1, 0.5, 0, 1)
        _EdgeIntensity ("Edge Intensity", Range(0, 5)) = 2.0
        _NoiseScale ("Noise Scale", Range(0.1, 10)) = 1.0
        _NoiseSpeed ("Noise Speed", Range(0, 2)) = 0.5
        _Color ("Color Tint", Color) = (1, 1, 1, 1)
        _GrowthCenter ("Growth Center", Vector) = (0.5, 0.5, 0, 0)
        _GrowthDirection ("Growth Direction", Range(0, 1)) = 0
        _GrowthInfluence ("Growth Influence", Range(0, 1)) = 0.7
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 8.0
        _UseProceduralNoise ("Use Procedural Noise", Range(0, 1)) = 1
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
                float2 noiseUV : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;
            
            float _DissolveAmount;
            float _EdgeWidth;
            fixed4 _EdgeColor;
            float _EdgeIntensity;
            float _NoiseScale;
            float _NoiseSpeed;
            fixed4 _Color;
            float4 _GrowthCenter;
            float _GrowthDirection;
            float _GrowthInfluence;
            float _PulseSpeed;
            float _UseProceduralNoise;

            // Simple noise function for procedural noise
            float random(float2 st) {
                return frac(sin(dot(st.xy, float2(12.9898, 78.233))) * 43758.5453123);
            }

            float noise(float2 st) {
                float2 i = floor(st);
                float2 f = frac(st);
                
                float a = random(i);
                float b = random(i + float2(1.0, 0.0));
                float c = random(i + float2(0.0, 1.0));
                float d = random(i + float2(1.0, 1.0));
                
                float2 u = f * f * (3.0 - 2.0 * f);
                
                return lerp(a, b, u.x) +
                       (c - a) * u.y * (1.0 - u.x) +
                       (d - b) * u.x * u.y;
            }

            // Fractal noise for more complex patterns
            float fbm(float2 st) {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 0.0;
                
                for (int i = 0; i < 4; i++) {
                    value += amplitude * noise(st);
                    st *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                // Animate noise UV coordinates
                float2 timeOffset = float2(_Time.y * _NoiseSpeed, _Time.y * _NoiseSpeed * 0.7);
                o.noiseUV = TRANSFORM_TEX(v.uv, _NoiseTex) * _NoiseScale + timeOffset;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample main texture
                fixed4 mainColor = tex2D(_MainTex, i.uv) * _Color;
                
                // Generate procedural noise
                float proceduralNoise = fbm(i.noiseUV);
                
                // Sample noise texture
                float noiseTexture = tex2D(_NoiseTex, i.noiseUV).r;
                
                // Combine texture and procedural noise based on user preference
                float combinedNoise = lerp(noiseTexture, proceduralNoise, _UseProceduralNoise);
                
                // Create growing pattern from center or edges
                float2 center = _GrowthCenter.xy;
                float distanceFromCenter = distance(i.uv, center);
                
                // Growth from edges
                float distanceFromEdge = min(min(i.uv.x, 1.0 - i.uv.x), min(i.uv.y, 1.0 - i.uv.y));
                float edgeGrowthPattern = 1.0 - distanceFromEdge;
                
                // Growth from center  
                float centerGrowthPattern = 1.0 - distanceFromCenter;
                
                // Lerp between center and edge growth based on _GrowthDirection
                float growthPattern = lerp(centerGrowthPattern, edgeGrowthPattern, _GrowthDirection);
                
                // Combine noise with growth pattern
                float dissolvePattern = combinedNoise + growthPattern * _GrowthInfluence;
                
                // Add temporal variation for organic growth
                float timeVariation = sin(_Time.y * 2.0 + distanceFromCenter * 10.0) * 0.05;
                dissolvePattern += timeVariation;
                
                // Calculate adjusted dissolve threshold for appearing effect
                float adjustedThreshold = _DissolveAmount * 1.5; // Extend range for smoother transition
                
                // Create main dissolve mask - this makes the object appear
                float dissolveMask = smoothstep(adjustedThreshold - 0.1, adjustedThreshold, dissolvePattern);
                
                // Create edge burn effect - the glowing edge that leads the dissolve
                float edgeStart = adjustedThreshold - _EdgeWidth;
                float edgeEnd = adjustedThreshold;
                float edgeMask = smoothstep(edgeStart - 0.05, edgeStart, dissolvePattern) - 
                                smoothstep(edgeEnd - 0.05, edgeEnd, dissolvePattern);
                
                // Enhanced edge glow with pulsing effect
                float edgePulse = 1.0 + sin(_Time.y * _PulseSpeed + distanceFromCenter * 15.0) * 0.3;
                fixed4 edgeGlow = _EdgeColor * _EdgeIntensity * edgeMask * edgePulse;
                
                // Soft transition zone for newly appeared areas
                float softTransition = smoothstep(adjustedThreshold, adjustedThreshold + 0.2, dissolvePattern);
                float newlyAppeared = dissolveMask * (1.0 - softTransition);
                
                // Add subtle glow to newly appeared areas
                fixed4 newAreaGlow = _EdgeColor * 0.3 * newlyAppeared;
                
                // Combine all effects
                fixed4 finalColor = mainColor * dissolveMask + edgeGlow + newAreaGlow;
                
                // Set alpha with smooth falloff
                float finalAlpha = dissolveMask + edgeMask * 0.8 + newlyAppeared * 0.4;
                finalColor.a = mainColor.a * finalAlpha;
                
                // Discard completely transparent pixels
                if (finalColor.a <= 0.001)
                    discard;
                
                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
