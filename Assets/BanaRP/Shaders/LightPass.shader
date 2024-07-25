Shader "BanaRP/LightPass"
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
            #include "Assets/BanaRP/ShaderLibrary/ClusterHelper.hlsl"
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
            sampler2D _GT2;
            sampler2D _GT3;

            samplerCUBE _irradianceIBL;
            samplerCUBE _prefilteredSpecIBL;
            sampler2D _brdfLUT;

            float4x4 _vpMat;
            float4x4 _vpMatInv;
            float3 _mainLightDirection;
	        float3 _mainLightColor;

            // shadow
            sampler2D _visibilityMap;
            bool _useShadowMask;
            sampler2D _shadowMask;

            float4 frag(v2f i, out float depthOut : SV_Depth) : SV_Target
            {
                float2 uv = i.uv;
                float4 GT1 = tex2D(_GT1, uv);
                float4 GT2 = tex2D(_GT2, uv);
                float3 GT3 = tex2D(_GT3, uv);

                // 从 GBuffer 中获取数据
                float3 albedo = tex2D(_GT0, uv).rgb;
                float3 normal = GT1.rgb * 2.0 - 1.0;
                float2 motionVec = GT2.rg;
                float roughness = GT2.b;
                float metallic = GT2.a;
                float3 emission = GT3.rgb;
                float ao = GT1.a;
                
                float d = tex2D(_gDepth, uv);
                float d_linear = Linear01Depth(d, _ZBufferParams);
                depthOut = d;

// if (_useShadowMask)
//                 {
//                     float shadowMask = tex2D(_shadowMask, i.uv).r;
//                     if (shadowMask > 0.00001 && shadowMask < 0.99995)
//                     {
//                         return float4(1,0,0,1);
//                     }
//                 }
                
                float4 ndcPoc = float4(i.uv * 2.0 - 1.0, d, 1.0);
                float4 worldPos = mul(_vpMatInv, ndcPoc);
                worldPos /= worldPos.w;

                float3 N = normalize(normal);
                float3 L = normalize(_mainLightDirection);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - worldPos.xyz);
                float3 radiance = _mainLightColor;

                // direct light
                float3 direct = PBR(N, V, L, albedo, radiance, roughness, metallic);
                // ibl light
                float3 ambient = IBL(N, V,
                    albedo, roughness, metallic,
                    _irradianceIBL, _prefilteredSpecIBL, _brdfLUT
                    );

                
                float3 color = ambient * ao;
                float visibility = tex2D(_visibilityMap, uv).r;
                
                color += direct * visibility;
                color += emission;

                // cluster point light
                uint x = floor(uv.x * _numClusterX);
                uint y = floor(uv.y * _numClusterY);
                uint z = floor(d_linear * _numClusterZ);

                uint3 clusterId_3D = uint3(x, y, z);
                uint clusterId_1D = Index3DTo1D(clusterId_3D);
                LightIndex lightIndex = _assignTable[clusterId_1D];

                int start = lightIndex.start;
                int pointEnd = start + lightIndex.pointCount;

                for (int i = start; i < pointEnd; ++i)
                {
                    uint lightId = _lightAssignBuffer[i];
                    PointLight plight = _pointLightBuffer[lightId];

                    float3 pL = normalize(plight.position - worldPos.xyz);
                    radiance = plight.color * plight.intensity;

                    // 衰减
                    float dist = distance(plight.position, worldPos.xyz);
                    float d2 = pow(dist, 2);
                    float r2 = pow(plight.range, 2);
                    float attenuation = 1.0 - saturate(pow(d2 / r2, 2));
                    attenuation *= attenuation;

                    color += PBR(N, V, pL, albedo, radiance, roughness, metallic) * attenuation;
                }

                int spotEnd = pointEnd + lightIndex.spotCount;
                for (int i = pointEnd; i < spotEnd; ++i)
                {
                    uint lightId = _lightAssignBuffer[i];
                    SpotLight slight = _spotLightBuffer[lightId];

                    float3 pL = normalize(slight.position - worldPos.xyz);
                    float theta = dot(pL, -slight.direction);
                    float cutOff = cos(atan(slight.bottomRadius / slight.height));
                    radiance = slight.color * slight.intensity;
                    
                    // 衰减
                    float dist = distance(slight.position, worldPos.xyz);
                    float attenuation = 1.0 / (dist * dist);
                    radiance *= attenuation;
                    if (theta > cutOff)
                    {
                        color += PBR(N, V, pL, albedo, radiance, roughness, metallic);
                    }
                }
                
                return float4(color, 1);
            }
            ENDHLSL
        }
    }
}