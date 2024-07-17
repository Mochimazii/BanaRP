Shader "BanaRP/DepthWrite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite On ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/BanaRP/ShaderLibrary/Helper.hlsl"
            #include "Assets/BanaRP/Shaders/BRDF.hlsl"
            #include "Assets/BanaRP/Shaders/Shadow.hlsl"

            TEXTURE2D(_gDepth);
            SAMPLER(sampler_gDepth);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i, out float depthOut : SV_Depth) : SV_Target
            {
                // out depth for skybox
                depthOut = SAMPLE_TEXTURE2D(_gDepth, sampler_gDepth, i.uv);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
            }
            ENDHLSL
        }
    }
}