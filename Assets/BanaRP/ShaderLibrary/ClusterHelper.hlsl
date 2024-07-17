#ifndef CUSTOM_ClUSTER_HELPER_INCLUDED
#define CUSTOM_ClUSTER_HELPER_INCLUDED

struct PointLight
{
    float3 color;
    float intensity;
    float3 position;
    float range;
};

struct LightIndex
{
    int start;
    int count;
};

StructuredBuffer<PointLight> _lightBuffer;
StructuredBuffer<uint> _lightAssignBuffer;
StructuredBuffer<LightIndex> _assignTable;

float _numClusterX;
float _numClusterY;
float _numClusterZ;

uint Index3DTo1D(uint3 index)
{
    return index.z * _numClusterX * _numClusterY + index.y * _numClusterX + index.x;
}


#endif
