Shader "Custom/WavyDistortion"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _WaveSpeed ("Wave Speed", Range(0, 10)) = 2
        _WaveFrequency ("Wave Frequency", Range(0, 20)) = 5
        _WaveAmplitude ("Wave Amplitude", Range(0, 1)) = 0.3
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
        float _WaveSpeed;
        float _WaveFrequency;
        float _WaveAmplitude;
        half _Metallic;
        half _Glossiness;
        fixed4 _EmissionColor;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        void vert(inout appdata_full v)
        {
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            float time = _Time.y * _WaveSpeed;
            
            // Create wavy distortion using sine waves
            float wave1 = sin(worldPos.x * _WaveFrequency + time) * _WaveAmplitude;
            float wave2 = sin(worldPos.z * _WaveFrequency * 1.3 + time * 0.7) * _WaveAmplitude;
            float wave3 = sin((worldPos.x + worldPos.z) * _WaveFrequency * 0.7 + time * 1.2) * _WaveAmplitude;
            
            float displacement = (wave1 + wave2 + wave3) / 3.0;
            
            // Displace along normal
            v.vertex.xyz += v.normal * displacement;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float time = _Time.y * _WaveSpeed;
            float wave = sin(IN.worldPos.x * _WaveFrequency + time) * 0.5 + 0.5;
            
            fixed4 c = _Color;
            c.rgb *= (0.7 + wave * 0.3);
            
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = _EmissionColor.rgb * wave;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
