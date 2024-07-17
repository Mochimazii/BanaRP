#ifndef BANA_SHADOW_INCLUDED
#define BANA_SHADOW_INCLUDED

#define NUM_SAMPLES 64
#define NUM_RINGS 10
#define EPS 1e-3
#define PI2 6.283185307179586

static float2 poissonDisk[NUM_SAMPLES];

float rand_2to1(float2 uv) {
    // 0 - 1
    const float a = 12.9898, b = 78.233, c = 43758.5453;
    float dt = dot( uv.xy, float2( a,b ) ), sn = fmod( dt, PI );
    return frac(sin(sn) * c);
}

void poissonDiskSamples(float2 randomSeed)
{
    float ANGLE_STEP = PI2 * float( NUM_RINGS ) / float( NUM_SAMPLES );
    float INV_NUM_SAMPLES = 1.0 / float( NUM_SAMPLES );

    float angle = rand_2to1( randomSeed ) * PI2;
    float radius = INV_NUM_SAMPLES;
    float radiusStep = radius;

    for( int i = 0; i < NUM_SAMPLES; i ++ ) {
        poissonDisk[i] = float2( cos( angle ), sin( angle ) ) * pow( radius, 0.75 );
        radius += radiusStep;
        angle += ANGLE_STEP;
    }
}

float ShadowMap01(sampler2D _shadowTex, float4 shadowNdcPos)
{
    float2 uv = shadowNdcPos.xy * 0.5 + 0.5;

    if(uv.x<0 || uv.x>1 || uv.y<0 || uv.y>1) return 1.0f;
    
    float d = shadowNdcPos.z;
    float d_sample = tex2D(_shadowTex, uv).r;

    #if defined (UNITY_REVERSED_Z)
        if (d > d_sample) return 1.0f;
    #else
        if (d < d_sample) return 1.0f;
    #endif
    
    return 0.0f;
}

float PCF(sampler2D _shadowTex, float4 shadowNdcPos, float  filterRadiusUV)
{
    float2 uv = shadowNdcPos.xy * 0.5 + 0.5;
    poissonDiskSamples(uv);
    float d_shadingPoint = shadowNdcPos.z;
    
    float blocker = 0.0f;
    for(int i = 0; i < NUM_SAMPLES; i++)
    {
        float2 offset = poissonDisk[i] * filterRadiusUV;
        float d_sample = tex2D(_shadowTex, uv+offset).r;

        #if defined (UNITY_REVERSED_Z)
            if (d_sample > d_shadingPoint)
            {
                blocker += 1.0f;
            }
        #endif
    }

    return 1 - blocker / float(NUM_SAMPLES);
}

// PCSS
float AverageBlockerDepth(sampler2D _shadowTex, float2 shadowUV, float d_shadingPoint, float searchRadius)
{
    int blockerNum = 0;
    float blockDepth = 0.;
    poissonDiskSamples(shadowUV);
    
    for(int i = 0; i < NUM_SAMPLES; i++){
        float d_sample = tex2D(_shadowTex, shadowUV + poissonDisk[i] * searchRadius).r;
        #if defined (UNITY_REVERSED_Z)
            if (d_sample > d_shadingPoint)
            {
                blockerNum++;
                blockDepth += d_sample;
            }
        #endif
    }

    if(blockerNum == 0)
        return -1.;
    else
        return blockDepth / float(blockerNum);
}

float PCSS(sampler2D _shadowTex, float4 shadowNdcPos, float lightSizeWorld, float frustumWidthWorld)
{
    float d_shadingPoint = shadowNdcPos.z;
    float2 shadowUV = shadowNdcPos.xy * 0.5 + 0.5;

    // avgblocker depth
    // 假设光源贴在 shadow map 上
    float lightSizeUV = lightSizeWorld / frustumWidthWorld;
    float avgBlockerDepth = AverageBlockerDepth(_shadowTex, shadowUV, d_shadingPoint, lightSizeUV);

    if(avgBlockerDepth < -EPS)
        return 1.0;

    // penumbra size
    float zReceiver = 1 - d_shadingPoint;
    avgBlockerDepth = 1 - avgBlockerDepth;
    float penumbra = (zReceiver - avgBlockerDepth) * lightSizeUV / avgBlockerDepth;
    float filterRadiusUV = penumbra;

    // filtering
    return PCF(_shadowTex, shadowNdcPos, filterRadiusUV);
}

float getVisibility(float4 worldPos, sampler2D _shadowTex, float4x4 _shadowVpMatrix, float lightSizeWorld, float frustumWidthWorld)
{
    float4 shadowNdcPos = mul(_shadowVpMatrix, worldPos);
    shadowNdcPos /= shadowNdcPos.w;

    return PCSS(_shadowTex, shadowNdcPos, lightSizeWorld, frustumWidthWorld);
    //return PCF(_shadowTex, shadowNdcPos, float2(0.003, 0.003));
    //return ShadowMap01(_shadowTex, shadowNdcPos);
}

#endif
