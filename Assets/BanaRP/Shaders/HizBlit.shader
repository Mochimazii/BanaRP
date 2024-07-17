Shader "BanaRP/HizBlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct a2v
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 position : SV_POSITION;
            };
            
            v2f vert (a2v v)
            {
                v2f o;
                o.position = TransformObjectToHClip(v.position);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float frag (v2f i) : SV_Target
            {
                // Mipmap[i+1]中下标为(x, y)像素，应该对应的是Mipmap[i]的(2x, 2y)、(2x, 2y+1)、(2x+1, 2y)、(2x+1, 2y+1)这四个像素。
                // 而在Shader中我们需要使用uv来表示，假设Mipmap[i+1]的边长为w，那么Mipmap[i]的边长即为2w。
                // Mipmap[i+1]中下标为(x, y)的像素的uv坐标即为(x/w, y/w)，
                // 对应的Mipmap[i]的四个像素的uv则为(2x/2w, 2y/2w)、(2x/2w, (2y+1)/2w)、((2x+1)/2w, 2y/2w)、((2x+1)/2w, (2y+1)/2w)，
                // 即：
                // Mipmap[i+1]的(u, v)对应Mipmap[i]的(u, v)、(u, v+0.5/w)、(u+0.5/w, v)、(u+0.5/w, v+0.5/w)
                float4 depth;
                float2 offset = 0.5 * _MainTex_TexelSize.xy;      
                depth.x = tex2D(_MainTex, i.uv + offset * float2(-0.5, -0.5)).x;
                depth.y = tex2D(_MainTex, i.uv + offset * float2(-0.5, 0.5)).x;
                depth.z = tex2D(_MainTex, i.uv + offset * float2(0.5, -0.5)).x;
                depth.w = tex2D(_MainTex, i.uv + offset * float2(0.5, 0.5)).x;

                #if defined(UNITY_REVERSED_Z)
                return min(min(depth.x, depth.y), min(depth.z, depth.w));
                #else
                return max(max(depth.x, depth.y), max(depth.z, depth.w));
                #endif
            }
            
            ENDHLSL
        }
    }
}