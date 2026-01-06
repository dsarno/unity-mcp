Shader "Custom/HolographicNoise"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _NoiseScale ("Noise Scale", Range(0.1, 50)) = 10
        _ScanSpeed ("Scan Speed", Range(0, 10)) = 2
        _DistortionAmount ("Distortion Amount", Range(0, 0.5)) = 0.1
        _Metallic ("Metallic", Range(0,1)) = 0.8
        _Glossiness ("Smoothness", Range(0,1)) = 0.9
        _EmissionColor ("Emission Color", Color) = (0,1,1,1)
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
        float _ScanSpeed;
        float _DistortionAmount;
        half _Metallic;
        half _Glossiness;
        fixed4 _EmissionColor;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 viewDir;
        };

        float noise(float3 p)
        {
            return frac(sin(dot(p, float3(12.9898, 78.233, 45.5432))) * 43758.5453);
        }

        float fbm(float3 p)
        {
            float value = 0.0;
            float amplitude = 0.5;
            float frequency = 1.0;
            
            for (int i = 0; i < 3; i++)
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
            float time = _Time.y * _ScanSpeed;
            
            // Create holographic scan effect
            float scanLine = frac((worldPos.y + time) * 0.5);
            float3 noiseCoord = worldPos * _NoiseScale + float3(time * 0.3, time * 0.5, time * 0.2);
            float noiseValue = fbm(noiseCoord);
            
            float displacement = (scanLine * noiseValue) * _DistortionAmount;
            
            v.vertex.xyz += v.normal * displacement;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float time = _Time.y * _ScanSpeed;
            float scanLine = frac((IN.worldPos.y + time) * 0.5);
            float3 noiseCoord = IN.worldPos * _NoiseScale + float3(time * 0.3, time * 0.5, time * 0.2);
            float noiseValue = fbm(noiseCoord);
            
            // Holographic color shift
            fixed4 c = _Color;
            float fresnel = 1.0 - saturate(dot(normalize(IN.viewDir), normalize(IN.worldPos)));
            c.rgb = lerp(c.rgb, _EmissionColor.rgb, fresnel * 0.3);
            c.rgb *= (0.7 + scanLine * 0.3 + noiseValue * 0.2);
            
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = _EmissionColor.rgb * (scanLine * 0.8 + noiseValue * 0.2);
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
