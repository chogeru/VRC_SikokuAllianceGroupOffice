Shader "Custom/VRC_TerrainWave_8Layer"
{
    Properties
    {
        [HideInInspector] _Control ("Control 1 (Layers 0-3)", 2D) = "red" {}
        [HideInInspector] _Control2 ("Control 2 (Layers 4-7)", 2D) = "black" {}
        
        // Wave Params (Udon‚©‚ç§Œä)
        _WaveSpeed ("Wave Speed", Float) = 1.0
        _WaveAmp ("Wave Amplitude", Float) = 1.0
        _WaveDirX ("Wave Dir X", Float) = 1.0
        _WaveDirZ ("Wave Dir Z", Float) = 0.6
        _WaveFrequency ("Wave Frequency", Float) = 0.1
        _WaveTime ("Wave Time", Float) = 0.0

        // Layers 0-3
        _Splat0 ("L0 Albedo", 2D) = "white" {} _Normal0 ("L0 Normal", 2D) = "bump" {} _Scale0 ("L0 Scale", Float) = 10
        _Splat1 ("L1 Albedo", 2D) = "white" {} _Normal1 ("L1 Normal", 2D) = "bump" {} _Scale1 ("L1 Scale", Float) = 10
        _Splat2 ("L2 Albedo", 2D) = "white" {} _Normal2 ("L2 Normal", 2D) = "bump" {} _Scale2 ("L2 Scale", Float) = 10
        _Splat3 ("L3 Albedo", 2D) = "white" {} _Normal3 ("L3 Normal", 2D) = "bump" {} _Scale3 ("L3 Scale", Float) = 10

        // Layers 4-7
        _Splat4 ("L4 Albedo", 2D) = "white" {} _Normal4 ("L4 Normal", 2D) = "bump" {} _Scale4 ("L4 Scale", Float) = 10
        _Splat5 ("L5 Albedo", 2D) = "white" {} _Normal5 ("L5 Normal", 2D) = "bump" {} _Scale5 ("L5 Scale", Float) = 10
        _Splat6 ("L6 Albedo", 2D) = "white" {} _Normal6 ("L6 Normal", 2D) = "bump" {} _Scale6 ("L6 Scale", Float) = 10
        _Splat7 ("L7 Albedo", 2D) = "white" {} _Normal7 ("L7 Normal", 2D) = "bump" {} _Scale7 ("L7 Scale", Float) = 10

        _Glossiness ("Smoothness", Range(0,1)) = 0.0
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.5 
        // target 3.5ˆÈã„§ (ƒeƒNƒXƒ`ƒƒ”‚ª‘½‚¢‚½‚ß)

        sampler2D _Control, _Control2;
        
        // Macros for clean declaration
        #define DECLARE_LAYER(i) \
            sampler2D _Splat##i; sampler2D _Normal##i; float _Scale##i;

        DECLARE_LAYER(0) DECLARE_LAYER(1) DECLARE_LAYER(2) DECLARE_LAYER(3)
        DECLARE_LAYER(4) DECLARE_LAYER(5) DECLARE_LAYER(6) DECLARE_LAYER(7)

        half _Glossiness, _Metallic;
        float _WaveSpeed, _WaveAmp, _WaveDirX, _WaveDirZ, _WaveFrequency, _WaveTime;

        struct Input
        {
            float2 uv_Control;
            float3 worldPos;
        };

        // ”gŒvŽZ (Udon‚ÆŠ®‘S‚Éˆê’v‚³‚¹‚é)
        float GetWaveHeight(float3 pos)
        {
            float time = _WaveTime * _WaveSpeed;
            float dotVal = pos.x * _WaveDirX + pos.z * _WaveDirZ;
            float h = sin(dotVal * _WaveFrequency + time);
            h += sin(pos.x * 0.3 + time * 1.3) * 0.5;
            return h * _WaveAmp;
        }

        void vert (inout appdata_full v)
        {
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            float h = GetWaveHeight(worldPos);
            v.vertex.y += h;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Control Maps (Splatmaps)
            half4 c1 = tex2D (_Control, IN.uv_Control);
            half4 c2 = tex2D (_Control2, IN.uv_Control);

            float2 uvPos = IN.worldPos.xz;
            fixed3 albedo = 0;
            fixed3 normal = 0;

            // Macro for blending
            #define BLEND_LAYER(i, weight) \
                if(weight > 0.001) { \
                    float2 uv = uvPos / _Scale##i; \
                    albedo += tex2D(_Splat##i, uv).rgb * weight; \
                    normal += UnpackNormal(tex2D(_Normal##i, uv)) * weight; \
                }

            BLEND_LAYER(0, c1.r) BLEND_LAYER(1, c1.g) BLEND_LAYER(2, c1.b) BLEND_LAYER(3, c1.a)
            BLEND_LAYER(4, c2.r) BLEND_LAYER(5, c2.g) BLEND_LAYER(6, c2.b) BLEND_LAYER(7, c2.a)

            o.Albedo = albedo;
            o.Normal = normalize(normal + float3(0,0,0.01)); // Prevent zero normal
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}