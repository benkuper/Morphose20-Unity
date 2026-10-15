#ifndef MUDAAC_PAINT_REVEAL_INCLUDED
#define MUDAAC_PAINT_REVEAL_INCLUDED

float MudaacRevealHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float MudaacRevealNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(MudaacRevealHash(cell), MudaacRevealHash(cell + float2(1, 0)), f.x),
        lerp(MudaacRevealHash(cell + float2(0, 1)), MudaacRevealHash(cell + 1), f.x), f.y);
}

float MudaacRevealMask(float2 uv, float progress, float4 settings, float4 brush, float variation, float seed, float alternate)
{
    if (progress <= 0.0) return 0.0;
    // All strips have completed at one, even with rough tips and uneven speeds.
    if (progress >= 1.0) return 1.0;
    float count = max(1.0, settings.y);
    float column = min(uv.x * count, count - 0.0001);
    float nearest = floor(column);
    float timeline = progress * (count + variation);
    float coverage = 0.0;
    [unroll]
    for (int neighbour = -1; neighbour <= 1; neighbour++)
    {
        float index = nearest + neighbour;
        if (index < 0.0 || index >= count) continue;
        float duration = 1.0 + (MudaacRevealHash(float2(index + seed, 19.7)) * 2.0 - 1.0) * variation;
        float local = saturate((timeline - index) / max(0.2, duration));
        float up = abs(settings.z - fmod(index, 2.0) * alternate);
        float lengthwise = lerp(1.0 - uv.y, uv.y, up);
        float bristle = MudaacRevealNoise(float2(column * brush.z, uv.y * 2.0) + seed);
        float clump = MudaacRevealNoise(float2(column * 11.0, index + seed));
        float tip = (clump * 0.7 + bristle * 0.3) * 2.0 - 1.0;
        float head = local * (1.0 + brush.y * 2.0) - brush.y + tip * brush.y;
        float front = head - lengthwise;
        float frontAA = max(0.0005, fwidth(lengthwise) * 0.75);
        float side = (1.0 + brush.x) * 0.5 - abs(column - index - 0.5);
        side += (MudaacRevealNoise(float2(uv.y * 7.0, index * 3.1 + seed)) - 0.5) * brush.y * 0.4;
        float sideAA = max(0.001, fwidth(column) * 0.75);
        float mask = smoothstep(-frontAA, frontAA, front) * smoothstep(-sideAA, sideAA, side);
        coverage = max(coverage, local > 0.0 ? mask : 0.0);
    }
    return coverage;
}

void MudaacPaintReveal_float(float3 Position, float4 BaseColor, float4 Surface, float4 PaintColor,
    float4 RestX, float4 RestY, float4 RestZ, float4 RestOrigin, float4 AxisX, float4 AxisY,
    float4 Bounds, float4 Progress, float4 Brush, float4 Style, float4 Timing,
    out float3 Color, out float Smoothness, out float Metallic, out float3 Glow)
{
    float3 world = RestOrigin.xyz + Position.x * RestX.xyz + Position.y * RestY.xyz + Position.z * RestZ.xyz;
    float2 uv = float2((dot(world, AxisX.xyz) - AxisX.w) / max(0.001, Bounds.x),
        (dot(world, AxisY.xyz) - AxisY.w) / max(0.001, Bounds.y));
    uv = saturate(uv);
    uv.x = lerp(uv.x, 1.0 - uv.x, Bounds.z);
    float mask = MudaacRevealMask(uv, Progress.x, Progress, Brush, Timing.x, Style.w, Bounds.w);
    float erase = MudaacRevealMask(uv, Timing.y, Progress, Brush, Timing.x, Style.w + 113.0, Bounds.w);
    float column = uv.x * max(1.0, Progress.y);
    float bend = MudaacRevealNoise(float2(uv.y * 3.0, column * 0.6 + Style.w)) - 0.5;
    float bristle = MudaacRevealNoise(float2(column * Brush.z + bend * 2.0, uv.y * 2.5) + Style.w);
    float fine = MudaacRevealNoise(float2(column * Brush.z * 2.1, uv.y * 9.0) + Style.w + 37.0);
    float gaps = Brush.w > 0.0 ? smoothstep(Brush.w * 0.35, Brush.w * 0.35 + 0.035, bristle) : 1.0;
    float coverage = mask * (1.0 - erase) * gaps * saturate(Progress.w);
    float ridges = (bristle - 0.5) * 0.8 + (fine - 0.5) * 0.2;
    float3 pigment = PaintColor.rgb * max(0.0, 1.0 + ridges * Style.x);
    Color = lerp(BaseColor.rgb, pigment, coverage);
    Smoothness = lerp(Surface.y, saturate(Style.y + ridges * Style.x * 0.25), coverage);
    Metallic = lerp(Surface.x, 0.0, coverage);
    Glow = pigment * coverage * max(0.0, Style.z);
}

#endif
