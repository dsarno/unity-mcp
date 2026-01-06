Shader "Custom/PulsingNoise"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _NoiseScale ("Noise Scale", Range(0.1, 50)) = 10
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 1.5
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.3
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionIntensity ("Emission Intensity", Range(0, 5)) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        fixed4 _Color;
        float _NoiseScale;
        float _PulseSpeed;
        float _PulseAmount;
        half _Metallic;
        half _Glossiness;
        fixed4 _EmissionColor;
        float _EmissionIntensity;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        // Noise function
        float noise(float3 p)
        {
            return frac(sin(dot(p, float3(12.9898, 78.233, 45.5432))) * 43758.5453);
        }

        float fbm(float3 p)
        {
            float value = 0.0;
            float amplitude = 0.5;
            float frequency = 1.0;
            
            for (int i = 0; i < 4; i++)
            {
                value += amplitude * noise(p * frequency);
                frequency *= 2.0;
                amplitude *= 0.5;
            }
            return value;
        }

        void vert(inout appdata_full v)
        {
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            float time = _Time.y * _PulseSpeed;
            
            // Create pulsing displacement
            float pulse = sin(time) * 0.5 + 0.5;
            float3 noiseCoord = worldPos * _NoiseScale;
            float noiseValue = fbm(noiseCoord);
            
            float displacement = (pulse * _PulseAmount) + (noiseValue * _PulseAmount * 0.5);
            
            v.vertex.xyz += v.normal * displacement;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float time = _Time.y * _PulseSpeed;
            float pulse = sin(time) * 0.5 + 0.5;
            float3 noiseCoord = IN.worldPos * _NoiseScale;
            float noiseValue = fbm(noiseCoord);
            
            fixed4 c = _Color;
            c.rgb *= (0.8 + pulse * 0.2 + noiseValue * 0.1);
            
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = _EmissionColor.rgb * _EmissionIntensity * (pulse * 0.7 + noiseValue * 0.3);
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
