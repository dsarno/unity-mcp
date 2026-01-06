Shader "Custom/NoiseDisplacement"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _NoiseScale ("Noise Scale", Range(0.1, 50)) = 10
        _DisplacementAmount ("Displacement Amount", Range(0, 2)) = 0.3
        _Speed ("Animation Speed", Range(0, 5)) = 1
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

        sampler2D _MainTex;
        fixed4 _Color;
        float _NoiseScale;
        float _DisplacementAmount;
        float _Speed;
        half _Metallic;
        half _Glossiness;
        fixed4 _EmissionColor;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
        };

        // Simple noise function
        float noise(float3 p)
        {
            return frac(sin(dot(p, float3(12.9898, 78.233, 45.5432))) * 43758.5453);
        }

        // Improved noise with multiple octaves
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

        // Vertex displacement function
        void vert(inout appdata_full v)
        {
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            float time = _Time.y * _Speed;
            
            // Create animated noise
            float3 noiseCoord = worldPos * _NoiseScale + float3(time, time * 0.7, time * 0.3);
            float displacement = fbm(noiseCoord) * _DisplacementAmount;
            
            // Displace along normal
            v.vertex.xyz += v.normal * displacement;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            
            // Add noise to color for visual interest
            float3 noiseCoord = IN.worldPos * _NoiseScale * 0.5 + _Time.y * _Speed;
            float noiseValue = fbm(noiseCoord);
            c.rgb += noiseValue * 0.2;
            
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = _EmissionColor.rgb * (0.5 + noiseValue * 0.5);
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
