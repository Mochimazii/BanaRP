Shader "BanaRP/GBuffer"
{
    Properties
    {
        _MainTex ("Albedo Map", 2D) = "white" {}

        _Metallic_global ("Metallic", Range(0, 1)) = 0.5
        _Roughness_global ("Roughness", Range(0, 1)) = 0.5
        
        [Toggle] _Use_Metal_Map ("Use Metal Map", Float) = 1
        _MetallicGlossMap ("Metallic Map", 2D) = "white" {}

        _EmissionMap ("Emission Map", 2D) = "black" {}

        _OcclusionMap ("Occlusion Map", 2D) = "white" {}

        [Toggle] _Use_Normal_Map ("Use Normal Map", Float) = 1
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
    }
    SubShader
    {
        Pass
        {
            Tags {"LightMode" = "ShadowOnly"}
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/BanaRP/ShaderLibrary/Helper.hlsl"

            struct a2v
            {
                float3 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 depth : TEXCOORD0;
            };

            v2f vert (a2v IN)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(IN.vertex);
                o.depth = o.vertex.zw;
                return o;
            }

            float4 frag (v2f IN) : SV_Target
            {
                float d = IN.depth.x / IN.depth.y;
                // #if defined (UNITY_REVERSED_Z)
                //     d = 1.0 - d;
                // #endif
                float4 c = EncodeFloatRGBA(d);
                return c;
                //return float4(d,0,0,1);   // for debug
                //return (1,0,0,1);
            }
            
            ENDHLSL
        }
        
        Pass
        {
            // LightMode 通道标签的值必须与 ScriptableRenderContext.DrawRenderers 中的 ShaderTagId 匹配
            Tags { "LightMode" = "GBuffer"}
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct a2v
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD4;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float3 TtoW0 : TEXCOORD1;
                float3 TtoW1 : TEXCOORD2;
                float3 TtoW2 : TEXCOORD3;
            };

            /* 声明主纹理并且为主纹理设置一个采样器，没有什么说法，这是一种固定的格式。
            主纹理的声明同属性的声明，注意类型为TEXTURE2D，采样通过SAMPLER来定义，括号中的
            名字为采样器的变量名，变量名为sampler_纹理名，由于我们这里的纹理名为MainTex，
            所以，采样器为sampler_MainTex*/
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            TEXTURE2D(_MetallicGlossMap);
            SAMPLER(sampler_MetallicGlossMap);
            float4 _MetallicGlossMap_ST;
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);
            float4 _EmissionMap_ST;
            TEXTURE2D(_OcclusionMap);
            SAMPLER(sampler_OcclusionMap);
            float4 _OcclusionMap_ST;
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            float4 _BumpMap_ST;

            float _Use_Metal_Map;
            float _Use_Normal_Map;
            float _Metallic_global;
            float _Roughness_global;

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            StructuredBuffer<float4x4> _validMatrixBuffer;
            #endif

            void setup()
            {
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                unity_ObjectToWorld = _validMatrixBuffer[unity_InstanceID];
                unity_WorldToObject = unity_ObjectToWorld;
		        unity_WorldToObject._14_24_34 *= -1;
		        unity_WorldToObject._11_22_33 = 1.0f / unity_WorldToObject._11_22_33;
                #endif
            }
            
            v2f vert (a2v IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                // 法线和切线应该传到 pixel shader 中构建 TBN 矩阵, 这里直接构建会插值，有问题 
                float3 worldNormal = TransformObjectToWorldNormal(IN.normal);
                float3 worldTangent = TransformObjectToWorldDir(IN.tangent.xyz);
                float3 worldBinormal = cross(worldNormal, worldTangent) * IN.tangent.w;

                OUT.normal = worldNormal;
                OUT.TtoW0 = float3(worldTangent.x,worldBinormal.x,worldNormal.x);
                OUT.TtoW1 = float3(worldTangent.y,worldBinormal.y,worldNormal.y);
                OUT.TtoW2 = float3(worldTangent.z,worldBinormal.z,worldNormal.z);
                return OUT;
            }

            float2 _Jitter;
            float2 _prevJitter;
            float4x4 _prevJitteredVpMatrix;
            float4x4 _currentJitteredVpMatrixInv;

            void frag (
                v2f i,
                out float4 GT0 : SV_Target0,
                out float4 GT1 : SV_Target1,
                out float4 GT2 : SV_Target2,
                out float3 GT3 : SV_Target3)
            {
                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(color.a - 0.5);
                float3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb;
                float3 normal = i.normal;
                float metallic = _Metallic_global;
                float roughness = _Roughness_global;
                float ao = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, i.uv).g;

                if (_Use_Metal_Map)
                {
                    float4 metal = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_OcclusionMap, i.uv);
                    metallic = metal.r;
                    roughness = 1. - metal.a;
                }
                if(_Use_Normal_Map)
                {
                    normal = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv));
                    float3x3 TBN = float3x3(i.TtoW0, i.TtoW1, i.TtoW2);
                    normal = normalize(mul(TBN, normal));
                }

                // motion vector
                i.positionCS = TransformWorldToHClip(i.positionWS);
                i.positionCS /= i.positionCS.w;
                //i.positionCS.xy -= float2(_Jitter.x * 2, _Jitter.y * 2);
                float4 positionWS = mul(_currentJitteredVpMatrixInv, i.positionCS);
                positionWS /= positionWS.w;
                
                float4 prevPositionCS = mul(_prevJitteredVpMatrix, positionWS);
                prevPositionCS /= prevPositionCS.w;
                float2 prevScreenPos = prevPositionCS.xy * 0.5 + 0.5;
                prevScreenPos = prevScreenPos + _prevJitter;
                float2 currScreenPos = i.positionCS.xy * 0.5 + 0.5;
                currScreenPos = currScreenPos + _Jitter;

                float2 motionVec = currScreenPos - prevScreenPos;

                
                GT0 = color;
                GT1 = float4(normal * 0.5 + 0.5, ao);
                GT2 = float4(motionVec,roughness,metallic);
                GT3 = float3(emission);
            }
            
            ENDHLSL
        }
    }
}