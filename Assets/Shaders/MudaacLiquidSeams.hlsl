#ifndef MUDAAC_LIQUID_SEAMS_INCLUDED
#define MUDAAC_LIQUID_SEAMS_INCLUDED

// A small, cached neighborhood of real tile edges, supplied through the tile's property block.
float4 _SeamEdges[128];
float4 _SeamCellRects[32];
float4 _SeamCellSeeds[32];

float MudaacSeamHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float MudaacSeamNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    return lerp(lerp(MudaacSeamHash(cell), MudaacSeamHash(cell + float2(1, 0)), f.x),
        lerp(MudaacSeamHash(cell + float2(0, 1)), MudaacSeamHash(cell + 1), f.x), f.y);
}

float MudaacSeamUnion(float a, float b, float radius)
{
    float h = saturate(1.0 - abs(a - b) / max(0.00001, radius));
    return min(a, b) - radius * h * h * 0.25;
}

float MudaacSeamSegment(float2 p, float4 edge)
{
    float2 direction = edge.zw - edge.xy;
    float t = saturate(dot(p - edge.xy, direction) / max(0.00000001, dot(direction, direction)));
    return length(p - edge.xy - direction * t);
}

float2 MudaacSeamSite(int index, float seed, float time, float4 rect)
{
    float2 phase = float2(MudaacSeamHash(float2(index + seed, 37.1)),
        MudaacSeamHash(float2(index + seed, 83.7))) * 6.2831853;
    float speed = lerp(0.65, 1.15, MudaacSeamHash(float2(index + seed, 127.3)));
    return rect.xy + sin(phase + time * speed * float2(1.0, -0.83)) * rect.zw * 0.72;
}

float MudaacSeamWidth(float baseWidth, float bulge, float amount, float ripple, float roughness)
{
    float extra = amount * bulge;
    // At zero base width, suppress the fine residual tails of the noise field completely.
    if (baseWidth <= 0.0)
        extra *= smoothstep(amount * 0.15, max(0.00001, amount * 0.3), extra);
    float width = baseWidth + extra;
    return max(0.0, width + roughness * ripple * saturate(width / max(0.001, baseWidth + amount)));
}

void MudaacLiquidSeams_float(float3 Position, float4 BaseColor, float4 Liquid, float4 Interior, float4 Surface,
    float4 RestX, float4 RestY, float4 RestZ, float4 RestOrigin, float4 AxisX, float4 AxisY,
    float4 Rect, float4 Shape, float4 Flow, float4 Style, float4 Direction, float4 Network, float4 InteriorLines,
    out float3 Color, out float Smoothness, out float Metallic, out float3 Glow)
{
    // Authored world coordinates keep the seams on their tiles during other modifiers.
    float3 world = RestOrigin.xyz + Position.x * RestX.xyz + Position.y * RestY.xyz + Position.z * RestZ.xyz;
    float2 p = float2(dot(world, AxisX.xyz), dot(world, AxisY.xyz));
    // All tiles share this field, so motion agrees on both sides of a seam.
    float2 field = p * max(0.001, Flow.x) - Direction.xy * Flow.y + Flow.z * float2(0.731, 1.173);
    float2 warp = float2(MudaacSeamNoise(field * 0.63 + float2(0, Flow.y * 0.12)),
        MudaacSeamNoise(field * 0.63 + float2(17.3, -Flow.y * 0.15))) - 0.5;
    float slow = MudaacSeamNoise(field + warp * 1.6);
    float bulge = smoothstep(0.35, 0.85, slow);
    bulge *= bulge;
    float ripple = (MudaacSeamNoise(field * 19.0 + warp * 4.0) - 0.5) * 2.0;
    float width = MudaacSeamWidth(Shape.x, bulge, Shape.z, ripple, Shape.w);
    float radius = max(0.0, Shape.y) * (1.0 + bulge * 2.5);

    float horizontal = 100000.0, vertical = 100000.0;
    [loop]
    for (int edgeIndex = 0; edgeIndex < min(128, (int)Network.x); edgeIndex++)
    {
        float4 edge = _SeamEdges[edgeIndex];
        float d = MudaacSeamSegment(p, edge);
        if (abs(edge.x - edge.z) < abs(edge.y - edge.w)) vertical = min(vertical, d);
        else horizontal = min(horizontal, d);
    }
    float externalDistance = MudaacSeamUnion(horizontal - width, vertical - width, radius);
    float externalPresence = max(width, max(0.0, radius - abs(horizontal - vertical)));

    // Finite moving sites avoid animated hash/grid changes. Distances are in world metres.
    float internalDistance = 100000.0, internalPresence = 0.0;
    int count = clamp((int)InteriorLines.x, 0, 12);
    [loop]
    for (int cellTile = 0; cellTile < (count > 1 ? min(32, (int)Network.z) : 0); cellTile++)
    {
        float4 cellRect = _SeamCellRects[cellTile];
        float2 box = abs(p - cellRect.xy) - cellRect.zw;
        float clipDistance = length(max(box, 0.0)) + min(max(box.x, box.y), 0.0);
        if (clipDistance > radius + Style.y * 8.0 + Shape.z + InteriorLines.y) continue;
        float2 uv = (p - cellRect.xy) / max(cellRect.zw, 0.0001);
        float envelope = saturate(1.0 - uv.x * uv.x) * saturate(1.0 - uv.y * uv.y);
        float2 samplePoint = p + warp * InteriorLines.z * min(cellRect.z, cellRect.w) * 2.0 * envelope;
        float tileSeed = _SeamCellSeeds[cellTile].x + Flow.z * 17.0;
        float cellTime = Flow.y * InteriorLines.w;
        int closest = 0;
        float best = 100000000.0;
        float2 nearest = 0.0;
        [loop]
        for (int i = 0; i < count; i++)
        {
            float2 site = MudaacSeamSite(i, tileSeed, cellTime, cellRect);
            float2 delta = site - samplePoint;
            float d = dot(delta, delta);
            if (d < best) { best = d; closest = i; nearest = site; }
        }
        float internalWidth = MudaacSeamWidth(InteriorLines.y, bulge, Shape.z * 0.5, ripple, Shape.w * 0.5);
        float first = 100000.0, second = 100000.0;
        float cellDistance = 100000.0;
        [loop]
        for (int j = 0; j < count; j++)
        {
            if (j == closest) continue;
            float2 site = MudaacSeamSite(j, tileSeed, cellTime, cellRect);
            float2 delta = site - nearest;
            float d = dot((site + nearest) * 0.5 - samplePoint, delta / max(0.00001, length(delta)));
            if (d < first) { second = first; first = d; }
            else second = min(second, d);
            cellDistance = MudaacSeamUnion(cellDistance, d - internalWidth, radius);
        }
        cellDistance = max(cellDistance, clipDistance);
        float cellPresence = max(internalWidth, max(0.0, radius - abs(first - second)));
        if (cellDistance < internalDistance) internalPresence = cellPresence;
        internalDistance = MudaacSeamUnion(internalDistance, cellDistance, radius * 0.5);
    }
    float distance = MudaacSeamUnion(externalDistance, internalDistance, radius * 0.5);
    float presence = externalDistance < internalDistance ? externalPresence : internalPresence;
    presence = max(presence, max(0.0, radius * 0.5 - abs(externalDistance - internalDistance)));
    float aa = max(0.0001, fwidth(distance) * 0.7);
    // One-sided support prevents an antialiased half-bright line when thickness is exactly zero.
    float core = (1.0 - smoothstep(-aa, aa, distance)) * smoothstep(0.0, aa * 0.5, -distance);
    float halo = Style.y > 0.0 ? exp2(-max(0.0, distance) / max(0.0001, Style.y) * 4.0)
        * smoothstep(0.0, aa * 0.5, presence) : 0.0;
    float weight = saturate(Surface.w);
    float3 substrate = lerp(BaseColor.rgb, Interior.rgb, saturate(Surface.z));
    float3 painted = lerp(substrate, Liquid.rgb, core);
    Color = lerp(BaseColor.rgb, painted, weight);
    Smoothness = lerp(Surface.y, lerp(Surface.y, 0.15, saturate(Surface.z)), weight);
    Smoothness = lerp(Smoothness, 0.65, core * weight);
    // Black interiors should not reflect the bright studio sky as grey dielectric panels.
    Metallic = lerp(Surface.x, lerp(Surface.x, 1.0, saturate(Surface.z)) * (1.0 - core), weight);
    Glow = Liquid.rgb * max(0.0, Style.x) * weight * (core + halo * saturate(Style.z) * (1.0 - core));
}

#endif
