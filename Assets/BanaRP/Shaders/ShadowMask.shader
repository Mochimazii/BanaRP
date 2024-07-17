Shader "BanaRP/ShadowMask"
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

            sampler2D _gDepth;
            sampler2D _GT1;
            float4 _MainTex_TexelSize;
            float4x4 _vpMatInv;

            // shadow
            float _split0;
            float _split1;
            float _split2;
            float _split3;
            
            float4x4 _shadowVpMat0;
            float4x4 _shadowVpMat1;
            float4x4 _shadowVpMat2;
            float4x4 _shadowVpMat3;

            sampler2D _shadowtex0;
            sampler2D _shadowtex1;
            sampler2D _shadowtex2;
            sampler2D _shadowtex3;

            float4 frag(v2f i) : SV_Target
            {
                float sum = 0;
                float2 uv = i.uv;
                for (float i = -1.5; i <= 1.51; ++i)
                {
                    for (float j = -1.5; j <= 1.51; ++j)
                    {
                        float3 normal = tex2D(_GT1, uv).rgb * 2.0 - 1.0;
                        float2 offset = float2(i, j) * _MainTex_TexelSize.xy;
                        float2 sample_uv = uv + offset;
                        float d = tex2D(_gDepth, sample_uv).r;
                        float d_linear = Linear01Depth(d, _ZBufferParams);

                        // Reconstruct world position
                        float4 ndcPoc = float4(sample_uv * 2.0 - 1.0, d, 1.0);
                        float4 worldPos = mul(_vpMatInv, ndcPoc);
                        worldPos /= worldPos.w;
                        float4 worldPosOffset = worldPos;
                        worldPosOffset.xyz += normal * 0.05;
                        
                        float shadow = 1.0;
                        if (d_linear < _split0)
                        {
                            float4 shadowNdcPos = mul(_shadowVpMat0, worldPosOffset);
                            shadowNdcPos.xyz /= shadowNdcPos.w;
                            shadow *= ShadowMap01(_shadowtex0, shadowNdcPos);
                        }
                        else if (d_linear < _split0 + _split1)
                        {
                            float4 shadowNdcPos = mul(_shadowVpMat1, worldPosOffset);
                            shadowNdcPos.xyz /= shadowNdcPos.w;
                            shadow *= ShadowMap01(_shadowtex1, shadowNdcPos);
                        }
                        else if (d_linear < _split0 + _split1 + _split2)
                        {
                            float4 shadowNdcPos = mul(_shadowVpMat2, worldPosOffset);
                            shadowNdcPos.xyz /= shadowNdcPos.w;
                            shadow *= ShadowMap01(_shadowtex2, shadowNdcPos);
                        }
                        else if (d_linear < _split0 + _split1 + _split2 + _split3)
                        {
                            float4 shadowNdcPos = mul(_shadowVpMat3, worldPosOffset);
                            shadowNdcPos.xyz /= shadowNdcPos.w;
                            shadow *= ShadowMap01(_shadowtex3, shadowNdcPos);
                        }
                        sum += shadow;
                    }
                }

                return sum / 16;
            }
            ENDHLSL

        }

    }

}