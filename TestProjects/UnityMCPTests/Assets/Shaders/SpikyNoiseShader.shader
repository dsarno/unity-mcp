Shader "Custom/SpikyNoise"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _NoiseScale ("Noise Scale", Range(0.1, 50)) = 8
        _SpikeAmount ("Spike Amount", Range(0, 1.5)) = 0.8
        _AnimationSpeed ("Animation Speed", Range(0, 5)) = 1
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _EmissionColor ("Emission Color", Color) = (0,0,0,1)
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
        float _SpikeAmount;
        float _AnimationSpeed;
        half _Metallic;
        half _Glossiness;
        fixed4 _EmissionColor;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
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
            float time = _Time.y * _AnimationSpeed;
            
            // Create spiky noise displacement
            float3 noiseCoord = worldPos * _NoiseScale + float3(time, time * 0.5, time * 0.3);
            float noiseValue = fbm(noiseCoord);
            
            // Make it spiky by using power function
            float spike = pow(noiseValue, 0.3);
            float displacement = spike * _SpikeAmount;
            
            v.vertex.xyz += v.normal * displacement;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float time = _Time.y * _AnimationSpeed;
            float3 noiseCoord = IN.worldPos * _NoiseScale + float3(time, time * 0.5, time * 0.3);
            float noiseValue = fbm(noiseCoord);
            
            fixed4 c = _Color;
            c.rgb *= (0.6 + noiseValue * 0.4);
            
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = _EmissionColor.rgb * noiseValue;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
