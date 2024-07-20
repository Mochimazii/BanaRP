#ifndef CUSTOM_ClUSTER_HELPER_INCLUDED
#define CUSTOM_ClUSTER_HELPER_INCLUDED

struct PointLight
{
    float3 color;
    float intensity;
    float3 position;
    float range;
};

struct SpotLight
{
    float3 color;
    float intensity;
    float3 position;
    float height;
    float3 direction;
    float bottomRadius;
};

struct LightIndex
{
    int start;
    int pointCount;
    int spotCount;
};

// StructuredBuffer<PointLight> _lightBuffer;
StructuredBuffer<uint> _lightAssignBuffer;
StructuredBuffer<LightIndex> _assignTable;

StructuredBuffer<PointLight> _pointLightBuffer;
StructuredBuffer<SpotLight> _spotLightBuffer;

float _numClusterX;
float _numClusterY;
float _numClusterZ;

uint Index3DTo1D(uint3 index)
{
    return index.z * _numClusterX * _numClusterY + index.y * _numClusterX + index.x;
}


#endif
