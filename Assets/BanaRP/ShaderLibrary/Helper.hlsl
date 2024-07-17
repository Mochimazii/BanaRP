#ifndef CUSTOM_HELPER_INCLUDED
#define CUSTOM_HELPER_INCLUDED

// Encoding/decoding [0..1) floats into 8 bit/channel RGBA. Note that 1.0 will not be encoded properly.
inline float4 EncodeFloatRGBA(float v)
{
    float4 kEncodeMul = float4(1.0, 255.0, 65025.0, 16581375.0);
    float kEncodeBit = 1.0 / 255.0;
    float4 enc = kEncodeMul * v;
    enc = frac(enc);
    enc -= enc.yzww * kEncodeBit;
    return enc;
}

inline float DecodeFloatRGBA(float4 enc)
{
    float4 kDecodeDot = float4(1.0, 1 / 255.0, 1 / 65025.0, 1 / 16581375.0);
    return dot(enc, kDecodeDot);
}

// for TAA color clamp
static const int2 kOffsets3x3[9] =
{
    int2(-1, -1),
    int2(0, -1),
    int2(1, -1),
    int2(-1, 0),
    int2(0, 0),
    int2(1, 0),
    int2(-1, 1),
    int2(0, 1),
    int2(1, 1),
};

float3 rgbToYCoCg(float3 RGB)
{
    float Y = dot(RGB, float3(1, 2, 1));
    float Co = dot(RGB, float3(2, 0, -2));
    float Cg = dot(RGB, float3(-1, 2, -1));

    float3 YCoCg = float3(Y, Co, Cg);
    return YCoCg;
}

float3 YCoCgTorgb(float3 YCoCg)
{
    float Y = YCoCg.x * 0.25;
    float Co = YCoCg.y * 0.25;
    float Cg = YCoCg.z * 0.25;

    float R = Y + Co - Cg;
    float G = Y + Cg;
    float B = Y - Co - Cg;

    float3 RGB = float3(R, G, B);
    return RGB;
}

float3 ClipAABB(float3 aabbMin, float3 aabbMax, float3 historyColor)
{
    float3 p_clip = 0.5 * (aabbMax + aabbMin);
    float3 e_clip = 0.5 * (aabbMax - aabbMin) + FLT_EPS;
    
    float3 v_clip = historyColor - p_clip;
    float3 v_uint = v_clip.xyz / e_clip;
    float3 a_uint = abs(v_uint);
    float ma_uint = max(a_uint.x, max(a_uint.y, a_uint.z));
    
    if (ma_uint > 1.0)
        return p_clip + v_clip / ma_uint;
    else
        return historyColor; // history color inside aabb
}
#endif
