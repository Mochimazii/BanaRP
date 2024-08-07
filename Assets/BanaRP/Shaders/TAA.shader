Shader "BanaRP/TAA"
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

            TEXTURE2D(_gDepth);
            SAMPLER(sampler_gDepth);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_HistoryTex);
            SAMPLER(sampler_HistoryTex);

            TEXTURE2D(_GT2);

            SamplerState sampler_Linear_Clamp;
            SamplerState sampler_Point_Clamp;
            
            float _BlendAlpha;
            float2 _Jitter;
            float2 _prevJitter;
            float4x4 _prevJitteredVpMatrix;
            float4x4 _currentJitteredVpMatrixInv;

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

            float4 frag(v2f i) : SV_Target
            {
                // TAA
                float2 mainTexUV = float2(i.uv.x, i.uv.y);
                #if UNITY_UV_STARTS_AT_TOP
                    mainTexUV.y = 1.0 - mainTexUV.y;
                #endif
                mainTexUV = mainTexUV + _Jitter;
                float4 currColor = SAMPLE_TEXTURE2D(_MainTex, sampler_Linear_Clamp, mainTexUV);
                
                // reprojection start
                // float d = SAMPLE_TEXTURE2D(_gDepth, sampler_gDepth, i.uv - _Jitter);
                // float4 ndcPoc = float4((i.uv - _Jitter) * 2.0 - 1.0, d, 1.0);
                // float4 worldPos = mul(_currentJitteredVpMatrixInv, ndcPoc);
                // worldPos /= worldPos.w;
                //
                // float4 prevNdcPos = mul(_prevJitteredVpMatrix, worldPos);
                // prevNdcPos /= prevNdcPos.w;
                // float2 prevUV = 0.5 * (prevNdcPos.xy + 1.0);
                // prevUV = prevUV + _prevJitter;
                // float4 historyColor = SAMPLE_TEXTURE2D(_HistoryTex, sampler_HistoryTex, prevUV);
                // reprojection end

                // motion vec
                float2 motion = SAMPLE_TEXTURE2D(_GT2, sampler_Linear_Clamp, i.uv ).xy;
                float2 historyUV = i.uv - motion;
                float4 historyColor = SAMPLE_TEXTURE2D(_HistoryTex, sampler_HistoryTex, historyUV);

                // Neighborhood Clamping
                float3 aabbMin, aabbMax;
                aabbMax = aabbMin = rgbToYCoCg(currColor);
                for (int i = 0; i < 9; ++i)
                {
                    float3 color = rgbToYCoCg(_MainTex.Sample(sampler_Point_Clamp, mainTexUV, kOffsets3x3[i]));
                    aabbMin = min(aabbMin, color);
                    aabbMax = max(aabbMax, color);
                }
                float3 historyYCoCg = rgbToYCoCg(historyColor);
                historyColor.rgb = YCoCgTorgb(ClipAABB( aabbMin, aabbMax, historyYCoCg));
                
                return lerp(historyColor, currColor, _BlendAlpha);
            }
            ENDHLSL
        }
    }
}