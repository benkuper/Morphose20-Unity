#ifndef KISS_THE_DOOR_GAUSSIAN_SPLAT_DATA_INCLUDED
#define KISS_THE_DOOR_GAUSSIAN_SPLAT_DATA_INCLUDED

struct GaussianSplatData
{
    float3 position;
    float3 scale;
    float4 rotation;
    float4 color;
};

StructuredBuffer<GaussianSplatData> _GaussianSplats;
int _GaussianSplatCount;
float4x4 _GaussianObjectToWorld;

void GaussianSplatData_float(
    float Index,
    out float3 Position,
    out float3 Scale,
    out float4 Rotation,
    out float4 Color)
{
    uint count = (uint)max(_GaussianSplatCount, 0);
    uint index = count > 0 ? min((uint)max(Index, 0.0), count - 1) : 0;
    GaussianSplatData splat = _GaussianSplats[index];
    Position = splat.position;
    Scale = splat.scale;
    Rotation = splat.rotation;
    Color = splat.color;
}

void GaussianSplatWorldPosition_float(float Index, out float3 WorldPosition)
{
    float3 position;
    float3 scale;
    float4 rotation;
    float4 color;
    GaussianSplatData_float(Index, position, scale, rotation, color);
    WorldPosition = mul(_GaussianObjectToWorld, float4(position, 1.0)).xyz;
}

#endif
