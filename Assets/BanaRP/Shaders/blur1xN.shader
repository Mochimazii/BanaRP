Shader "BanaRP/blur1xN"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

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

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _gdepth;
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4x4 _vpMatInv;

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                float value = 0;
                float weight = 0;
                float r = 1;

                float radius = 1;
                // float d = tex2D(_gdepth, uv).r;
                // float4 worldPos = mul(_vpMatInv, float4(uv*2-1, d, 1));
                // worldPos /= worldPos.w;
                // float dist = distance(_WorldSpaceCameraPos.xyz, worldPos.xyz);
                // radius = 10 / (pow(dist, 2) + 0.01);
                // clamp(radius, 1, 10);

                for (int i = -r; i <= r; ++i)
                {
                    float2 offset = float2(0, i) * _MainTex_TexelSize.xy;
                    float2 uv_sample = uv + offset * radius;
                    value += tex2D(_MainTex, uv_sample).r;
                    weight++;
                }

                return value / weight;
            }
            
            ENDHLSL
        }

    }

}