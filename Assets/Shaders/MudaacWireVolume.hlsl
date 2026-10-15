#ifndef MUDAAC_WIRE_VOLUME_INCLUDED
#define MUDAAC_WIRE_VOLUME_INCLUDED

float MudaacWireHash(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float MudaacWireNoise(float3 p)
{
    float3 cell = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(lerp(MudaacWireHash(cell), MudaacWireHash(cell + float3(1,0,0)), f.x),
                     lerp(MudaacWireHash(cell + float3(0,1,0)), MudaacWireHash(cell + float3(1,1,0)), f.x), f.y),
                lerp(lerp(MudaacWireHash(cell + float3(0,0,1)), MudaacWireHash(cell + float3(1,0,1)), f.x),
                     lerp(MudaacWireHash(cell + float3(0,1,1)), MudaacWireHash(cell + 1.0), f.x), f.y), f.z);
}

// The second closest box face gives twelve geometric edges, without triangle diagonals.
float MudaacWireEdgeDistance(float3 position, float3 halfSize)
{
    float3 d = max(halfSize - abs(position), 0.0);
    return min(max(d.x, d.y), min(max(d.y, d.z), max(d.z, d.x)));
}

float2 MudaacWireEdge(float distance, float4 settings)
{
    float aa = max(fwidth(distance), 0.00001);
    float core = settings.x > 0.0 ? 1.0 - smoothstep(settings.x, settings.x + aa, distance) : 0.0;
    float halo = settings.y > 0.0 && settings.x > 0.0
        ? exp2(-distance / max(settings.x + settings.y, 0.00001) * 4.0) * settings.z : 0.0;
    return float2(core, halo);
}

void MudaacWireVolume_float(float3 Position, float3 ViewDirection, float4 BaseColor,
    float4 EdgeColor, float4 FogColor, float4 Center, float4 HalfSize,
    float4 Edge, float4 Surface, float4 Volume, float4 Texture, float4 Motion,
    out float3 Color, out float Alpha)
{
    // Read the final renderer transform, including modifiers later in the stack.
    float3 worldX = TransformObjectToWorldDir(float3(1, 0, 0), false);
    float3 worldY = TransformObjectToWorldDir(float3(0, 1, 0), false);
    float3 worldZ = TransformObjectToWorldDir(float3(0, 0, 1), false);
    float3 metric = max(float3(length(worldX), length(worldY), length(worldZ)), 0.000001);
    float3 up = float3(worldX.y, worldY.y, worldZ.y) / metric;
    float3 halfSize = max(HalfSize.xyz * metric, 0.00001);
    float3 p = clamp((Position - Center.xyz) * metric, -halfSize, halfSize);
    float3 ray = -normalize(ViewDirection * metric + float3(0.00000001, 0, 0));
    // Avoid division by zero for views parallel to a box face.
    float3 safeRay = lerp(-1.0, 1.0, step(0.0, ray)) * max(abs(ray), 0.00001);
    float3 exitDistances = (lerp(-halfSize, halfSize, step(0.0, ray)) - p) / safeRay;
    float length = max(0.0, min(exitDistances.x, min(exitDistances.y, exitDistances.z)));
    float3 exitPosition = clamp(p + ray * length, -halfSize, halfSize);
    float frontDistance = MudaacWireEdgeDistance(p, halfSize);
    float backDistance = MudaacWireEdgeDistance(exitPosition, halfSize);
    float2 front = MudaacWireEdge(frontDistance, Edge);
    float2 back = MudaacWireEdge(backDistance, Edge) * saturate(Edge.w);

    float integral = 0.0;
    int samples = clamp((int)Volume.w, 1, 12);
    float heightExtent = max(dot(halfSize, abs(up)), 0.00001);
    float3 seed = Texture.w * float3(0.73, 1.17, 2.31);
    [loop]
    for (int i = 0; i < samples; i++)
    {
        float3 samplePosition = p + ray * (length * ((i + 0.5) / samples));
        float height = saturate(dot(samplePosition, up) / (2.0 * heightExtent) + 0.5);
        float floorDensity = 1.0 + Volume.y * pow(1.0 - height, max(Volume.z, 0.01));
        float noise = MudaacWireNoise(samplePosition * Texture.x + seed + Motion.xyz);
        integral += floorDensity * lerp(1.0, noise * 2.0, saturate(Texture.y));
    }
    float fogAlpha = 1.0 - exp(-max(Volume.x, 0.0) * length * integral / samples);
    float grain = MudaacWireNoise(p * max(Texture.z, 0.01) + seed + Motion.xyz);
    float grainContrast = lerp(1.0, grain * 2.0, saturate(Motion.w));
    fogAlpha = saturate(fogAlpha * grainContrast);
    float faceAlpha = saturate(Surface.x * grainContrast);
    float frontAlpha = saturate((front.x + front.y) * Surface.y);
    float backAlpha = saturate((back.x + back.y) * Surface.y) * (1.0 - fogAlpha);
    float edgeAlpha = 1.0 - (1.0 - frontAlpha) * (1.0 - backAlpha);
    float bodyAlpha = 1.0 - (1.0 - fogAlpha) * (1.0 - faceAlpha);
    float targetAlpha = 1.0 - (1.0 - edgeAlpha) * (1.0 - bodyAlpha);
    float3 premultiplied = EdgeColor.rgb * Surface.z * edgeAlpha
        + FogColor.rgb * bodyAlpha * (1.0 - edgeAlpha);
    targetAlpha *= saturate(HalfSize.w);
    premultiplied *= saturate(HalfSize.w);
    Alpha = lerp(1.0, targetAlpha, saturate(Surface.w));
    Color = lerp(BaseColor.rgb, premultiplied, saturate(Surface.w)) / max(Alpha, 0.00001);
}

#endif
