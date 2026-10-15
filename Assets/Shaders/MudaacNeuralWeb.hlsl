#ifndef MUDAAC_NEURAL_WEB_INCLUDED
#define MUDAAC_NEURAL_WEB_INCLUDED

float2 MudaacWebHash(float2 p)
{
    float3 q = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return frac((q.xx + q.yz) * q.zy);
}

float MudaacWebNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    return lerp(lerp(MudaacWebHash(cell).x, MudaacWebHash(cell + float2(1, 0)).x, f.x),
        lerp(MudaacWebHash(cell + float2(0, 1)).x, MudaacWebHash(cell + 1.0).x, f.x), f.y);
}

// Three closest smoothly wandering sites: F2-F1 forms strands, F3-F1 finds junctions.
float3 MudaacWebCells(float2 p, float time, float motion, float seed)
{
    float2 cell = floor(p);
    float2 local = frac(p);
    float3 nearest = 100.0;
    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float2 offset = float2(x, y);
            float2 random = MudaacWebHash(cell + offset + seed);
            float2 site = 0.5 + (random - 0.5) * 0.7
                + sin(time * float2(0.71, 0.83) + random.yx * 6.2831853) * (0.24 * motion);
            float distance = length(offset + site - local);
            nearest.z = max(nearest.y, min(nearest.z, distance));
            nearest.y = max(nearest.x, min(nearest.y, distance));
            nearest.x = min(nearest.x, distance);
        }
    }
    return nearest;
}

void MudaacNeuralWeb_float(float3 Position, float4 BaseColor, float4 Background, float4 StrandColor,
    float4 JunctionColor, float4 RestX, float4 RestY, float4 RestZ, float4 RestOrigin,
    float4 AxisX, float4 AxisY, float4 Shape, float4 Detail, float4 Light, float4 Motion,
    float4 Pulse, out float3 Color, out float Alpha)
{
    float3 world = RestOrigin.xyz + RestX.xyz * Position.x + RestY.xyz * Position.y + RestZ.xyz * Position.z;
    float2 plane = float2(dot(world, AxisX.xyz) - AxisX.w, dot(world, AxisY.xyz) - AxisY.w);
    float2 p = (plane + Motion.zw * Motion.x) / max(Shape.x, 0.01);
    float seed = Detail.w * 1.173;
    float2 warp = float2(MudaacWebNoise(p * 0.73 + seed + Motion.x * 0.12),
        MudaacWebNoise(p * 0.73 + seed + 37.2 - Motion.x * 0.09)) - 0.5;
    warp += (float2(MudaacWebNoise(p * 1.91 + seed + 13.7),
        MudaacWebNoise(p * 1.91 + seed + 81.3)) - 0.5) * 0.3;
    p += warp * Shape.w * 2.0;
    float strands = 0.0;
    float junctions = 0.0;
    float scale = 1.0;
    float strength = 1.0;
    int layers = clamp((int)Detail.x, 1, 3);
    [loop]
    for (int layer = 0; layer < layers; layer++)
    {
        float2 q = p * scale + layer * float2(17.13, 31.71);
        float3 cells = MudaacWebCells(q, Motion.x * (1.0 + layer * 0.17), Motion.y, seed + layer * 11.3);
        float edge = cells.y - cells.x;
        float node = cells.z - cells.x;
        float variation = MudaacWebNoise(q * 0.85 + seed + Motion.x * 0.16);
        float width = Shape.y / max(Shape.x, 0.01) * lerp(0.45, 1.6, variation);
        float aa = max(fwidth(edge), 0.0001);
        float core = 1.0 - smoothstep(width * 0.4, width + aa, edge);
        float halo = Shape.z > 0.0 ? exp2(-edge / max(Shape.z / Shape.x, 0.0001) * 3.0) * Light.y : 0.0;
        float membrane = exp2(-edge / max(width * 3.5, 0.0001)) * 0.2;
        float glow = exp2(-node / max(width * 5.0 + aa, 0.0001) * 1.8);
        float pulse = lerp(1.0, 0.55 + 0.45 * sin(Motion.x * 1.7 + variation * 12.0 + layer), saturate(Pulse.x));
        // Attenuate subpixel filaments instead of letting distant fine layers shimmer.
        float resolved = saturate(1.0 / max(length(fwidth(q)), 0.0001));
        strands += (core * 0.58 + halo + membrane) * strength * pulse * resolved;
        junctions += glow * strength * pulse * resolved;
        scale *= 3.6;
        strength *= saturate(Detail.y);
    }
    float grain = MudaacWebNoise(plane * 150.0 + seed);
    float grainModulation = lerp(1.0, 0.65 + grain * 0.7, saturate(Detail.z));
    float3 web = Background.rgb + (StrandColor.rgb * strands * grainModulation
        + JunctionColor.rgb * junctions * Light.z) * max(Light.x, 0.0) * step(0.000001, Shape.y);
    Color = lerp(BaseColor.rgb, web, saturate(Light.w));
    Alpha = 1.0;
}

#endif
