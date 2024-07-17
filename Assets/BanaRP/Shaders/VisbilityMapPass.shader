Shader "BanaRP/VisibilityMapPass"
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
            sampler2D _GT0;
            sampler2D _GT1;
            sampler2D _shadowMask;

            float4x4 _vpMat;
            float4x4 _vpMatInv;

            // shadow
            bool _useShadowMask;
            float _split0;
            float _split1;
            float _split2;
            float _split3;

            float _pcssWorldLightSize[4];
            
            float _frustumWidth0;
            float _frustumWidth1;
            float _frustumWidth2;
            float _frustumWidth3;
            
            float4x4 _shadowVpMat0;
            float4x4 _shadowVpMat1;
            float4x4 _shadowVpMat2;
            float4x4 _shadowVpMat3;

            sampler2D _shadowtex0;
            sampler2D _shadowtex1;
            sampler2D _shadowtex2;
            sampler2D _shadowtex3;

            float frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                
                // 从 GBuffer 中获取数据
                float3 normal = tex2D(_GT1, uv).rgb * 2.0 - 1.0;
                float d = tex2D(_gDepth, uv);
                float d_linear = Linear01Depth(d, _ZBufferParams);

                float4 ndcPoc = float4(i.uv * 2.0 - 1.0, d, 1.0);
                float4 worldPos = mul(_vpMatInv, ndcPoc);
                worldPos /= worldPos.w;
                float4 worldPosOffset = worldPos;
                worldPosOffset.xyz += normal * 0.05;
                
                // shadow
                if (_useShadowMask)
                {
                    float shadowMask = tex2D(_shadowMask, i.uv).r;
                    if (shadowMask < 0.000001) return 0;
                    if (shadowMask > 0.999999) return 1;
                }

                float visibility = 1.0;
                if (d_linear < _split0)
                {
                    visibility *= getVisibility(worldPosOffset, _shadowtex0, _shadowVpMat0,
                                              _pcssWorldLightSize[0],
                                              _frustumWidth0);
                }
                else if (d_linear < _split0 + _split1)
                {
                    visibility *= getVisibility(worldPosOffset, _shadowtex1, _shadowVpMat1,
                                              _pcssWorldLightSize[1],
                                              _frustumWidth1);
                }
                else if (d_linear < _split0 + _split1 + _split2)
                {
                    visibility *= getVisibility(worldPosOffset, _shadowtex2, _shadowVpMat2,
                                              _pcssWorldLightSize[2],
                                              _frustumWidth2);
                }
                else if (d_linear < _split0 + _split1 + _split2 + _split3)
                {
                    visibility *= getVisibility(worldPosOffset, _shadowtex3, _shadowVpMat3,
                                              _pcssWorldLightSize[3],
                                              _frustumWidth3);
                }

                // shadow
                // bool softShadow = false;
                // float visibility = 1.0;
                // if (_useShadowMask)
                // {
                //     float shadowMask = tex2D(_shadowMask, i.uv).r;
                //     if (shadowMask < 0.000001) visibility = 0;
                //     else if (shadowMask > 0.999995) visibility = 1;
                //     else softShadow = true;
                // }
                //
                // if (softShadow)
                // {
                //     float4 worldPosOffset = worldPos;
                //     worldPosOffset.xyz += normal * 0.05;
                //     float shadow0 = getVisibility(worldPosOffset, _shadowtex0, _shadowVpMat0,
                //                                   _pcssWorldLightSize[0],
                //                                   _frustumWidth0);
                //     float shadow1 = getVisibility(worldPosOffset, _shadowtex1, _shadowVpMat1,
                //                                   _pcssWorldLightSize[1],
                //                                   _frustumWidth1);
                //     float shadow2 = getVisibility(worldPosOffset, _shadowtex2, _shadowVpMat2,
                //                                   _pcssWorldLightSize[2],
                //                                   _frustumWidth2);
                //     float shadow3 = getVisibility(worldPosOffset, _shadowtex3, _shadowVpMat3,
                //                                   _pcssWorldLightSize[3],
                //                                   _frustumWidth3);
                //
                //     float4 shadowSplit = float4(0, 0, 0, 0);
                //     if (d_linear < _split0)
                //     {
                //         visibility *= shadow0;
                //         shadowSplit = float4(0.1, 0, 0, 0);
                //     }
                //     else if (d_linear < _split0 + _split1)
                //     {
                //         visibility *= shadow1;
                //         shadowSplit = float4(0, 0.1, 0, 0);
                //     }
                //     else if (d_linear < _split0 + _split1 + _split2)
                //     {
                //         visibility *= shadow2;
                //         shadowSplit = float4(0, 0, 0.1, 0);
                //     }
                //     else if (d_linear < _split0 + _split1 + _split2 + _split3)
                //     {
                //         visibility *= shadow3;
                //         shadowSplit = float4(0.1, 0.1, 0, 0);
                //     }
                // }

                //return float4(shadow,shadow,shadow,0);

                // if (_useShadowMask)
                // {
                //     float shadowMask = tex2D(_shadowMask, i.uv).r;
                //     if (shadowMask > 0.00001 && shadowMask < 0.99995)
                //     {
                //         return float4(1,0,0,1);
                //     }
                // }

                return visibility;
            }
            ENDHLSL
        }
    }
}