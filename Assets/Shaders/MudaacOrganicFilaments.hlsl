#ifndef MUDAAC_ORGANIC_FILAMENTS_INCLUDED
#define MUDAAC_ORGANIC_FILAMENTS_INCLUDED

float2 MudaacOrganicHash(float2 p)
{
    float3 q = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return frac((q.xx + q.yz) * q.zy);
}

float MudaacOrganicNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    return lerp(lerp(MudaacOrganicHash(i).x, MudaacOrganicHash(i + float2(1, 0)).x, f.x),
        lerp(MudaacOrganicHash(i + float2(0, 1)).x, MudaacOrganicHash(i + 1.0).x, f.x), f.y);
}

float2 MudaacOrganicWarp(float2 p, float time)
{
    return float2(MudaacOrganicNoise(p + float2(time * 0.12, 2.7)),
        MudaacOrganicNoise(p + float2(19.3, -time * 0.1))) * 2.0 - 1.0;
}

void MudaacOrganicFilaments_float(float3 Position, float4 BaseColor, float4 FilamentColor, float4 Background,
    float4 RestX, float4 RestY, float4 RestZ, float4 RestOrigin, float4 AxisX, float4 AxisY,
    float4 Rect, float4 Pattern, float4 Strands, float4 Glow, float4 Surface, float4 Motion,
    out float3 Color, out float Alpha)
{
    // Reconstruct the authored surface: other tile modifiers cannot slide the pattern off its mesh.
    float3 world = RestOrigin.xyz + Position.x * RestX.xyz + Position.y * RestY.xyz + Position.z * RestZ.xyz;
    float2 p = float2(dot(world, AxisX.xyz), dot(world, AxisY.xyz));
    float scale = max(0.02, Pattern.x), t = Motion.x;
    float2 uv = (p - Rect.xy) / max(Rect.zw, 0.0001);
    float envelope = saturate(1.0 - uv.x * uv.x) * saturate(1.0 - uv.y * uv.y);
    // Tile influence bends the entire interior gently, vanishing smoothly at each tile boundary.
    // At zero influence, all fragments sample precisely the same facade-wide field.
    float2 tileWarp = float2(sin(uv.y * 2.4 + Surface.w * 6.283),
        cos(uv.x * 2.7 + Surface.w * 9.17)) * envelope;
    float2 q = (p - Motion.zw * t) / scale + Motion.y * float2(0.731, 1.173);
    q += tileWarp * saturate(Pattern.y) * 0.8;
    float2 warp = MudaacOrganicWarp(q * 0.85, t);
    q += warp * Pattern.z;
    q += MudaacOrganicWarp(q * 2.7 + warp, t * 0.6) * Pattern.z * 0.18;

    float2 grid = floor(q), local = frac(q);
    float first = 100.0, second = 100.0, third = 100.0;
    float2 nearest = 0.0, nearestId = 0.0;
    // Sites remain in their lattice cells; smooth motion never swaps a random seed over time.
    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float2 offset = float2(x, y), id = grid + offset;
            float2 hash = MudaacOrganicHash(id);
            float2 phase = hash * 6.2831853;
            float2 site = 0.5 + sin(phase + t * float2(0.7, -0.61)) * (0.14 + Pattern.w * 0.26);
            float2 delta = offset + site - local;
            float d = length(delta);
            if (d < first) { third = second; second = first; first = d; nearest = delta; nearestId = id; }
            else if (d < second) { third = second; second = d; }
            else if (d < third) { third = d; }
        }
    }
    // Curved cell membranes, luminous necks and radial fibres, throughout the tile interior.
    float distance = max(0.0, second - first) * 0.5 * scale;
    float aa = max(fwidth(distance), 0.00001);
    float width = max(0.0, Strands.x);
    float membrane = width > 0.0 ? 1.0 - smoothstep(width, width + aa, distance) : 0.0;
    float halo = Glow.x > 0.0 ? exp2(-distance / max(Glow.x, 0.0001) * 3.0) * Glow.y : 0.0;
    float junction = exp2(-(third - first) * 32.0) * Glow.z;
    float pulse = lerp(1.0, 0.25 + 0.75 * pow(0.5 + 0.5 * sin(t * 1.8
        + MudaacOrganicNoise(q * 0.65) * 8.0), 2.0), saturate(Glow.w));

    float angle = atan2(nearest.y, nearest.x);
    float angleWave = angle / 6.2831853 * floor(Strands.y) + first * 3.0
        + MudaacOrganicNoise(q * 4.0 + warp) * 2.5 - t * 0.15;
    float strand = pow(saturate(0.5 + 0.5 * sin(angleWave * 6.2831853)), 14.0);
    // Fade subpixel strands instead of letting them sparkle on distant facade cameras.
    strand = lerp(strand, 0.12, saturate(fwidth(angleWave) * 0.8));
    float veil = exp2(-distance / max(scale * 0.13, 0.0001) * 2.0);
    float grain = MudaacOrganicNoise(q * 95.0 + nearestId * 7.0);
    float fibres = strand * veil * Strands.z * (0.2 + grain * 0.8);
    float mist = veil * Strands.w * (0.25 + grain * 0.75);
    float intensity = (membrane + halo + junction + fibres + mist) * pulse;
    float3 substrate = lerp(BaseColor.rgb, Background.rgb, saturate(Surface.z));
    float3 effect = substrate + FilamentColor.rgb * intensity * max(0.0, Surface.y);
    Color = max(0.0, lerp(BaseColor.rgb, effect, saturate(Surface.x)));
    Alpha = 1.0;
}

#endif
