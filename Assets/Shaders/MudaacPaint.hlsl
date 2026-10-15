#ifndef MUDAAC_PAINT_INCLUDED
#define MUDAAC_PAINT_INCLUDED

// Set per renderer, allowing independent paint controllers to use their own origins.
float4 _MudaacPaintSources[16];

float MudaacPaintHash(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float MudaacPaintNoise(float3 p)
{
    float3 cell = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(lerp(MudaacPaintHash(cell), MudaacPaintHash(cell + float3(1, 0, 0)), f.x),
                     lerp(MudaacPaintHash(cell + float3(0, 1, 0)), MudaacPaintHash(cell + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(MudaacPaintHash(cell + float3(0, 0, 1)), MudaacPaintHash(cell + float3(1, 0, 1)), f.x),
                     lerp(MudaacPaintHash(cell + float3(0, 1, 1)), MudaacPaintHash(cell + float3(1, 1, 1)), f.x), f.y), f.z);
}

float MudaacSpreadNoise(float3 p, float4 controls, float4 detail, float time, float3 drift)
{
    p += drift * time;
    float3 warp = float3(MudaacPaintNoise(p * 0.57 + float3(time * 0.13, 5.2, 1.7)),
        MudaacPaintNoise(p * 0.57 + float3(9.1, time * 0.11, 3.4)),
        MudaacPaintNoise(p * 0.57 + float3(2.8, 7.6, time * 0.17))) - 0.5;
    p += warp * detail.z;
    float value = 0.0;
    float total = 0.0;
    float amplitude = 1.0;
    [unroll]
    for (int octave = 0; octave < 8; octave++)
    {
        if (octave < (int)controls.w)
        {
            value += (MudaacPaintNoise(p + float3(0, time * 0.19, 0)) * 2.0 - 1.0) * amplitude;
            total += amplitude;
            p = p * detail.x + 7.13;
            amplitude *= detail.y;
        }
    }
    return clamp(value / max(0.001, total) * controls.z, -1.0, 1.0);
}

float MudaacStrokeDistance(float3 relative, float3 world, float3 axis, float3 side,
    float4 strokes, float4 shape, float3 seed)
{
    float along = dot(relative, axis);
    float across = dot(relative, side);
    float depth = dot(relative, normalize(cross(axis, side)));
    float3 field = world * shape.z + seed;
    float bend = (MudaacPaintNoise(field + float3(2.7, 8.1, 4.3)) * 2.0 - 1.0)
        * strokes.w * saturate(length(relative) / max(strokes.y, 0.01));
    float lane = (across + bend) / max(strokes.y, 0.01);
    float branching = (MudaacPaintNoise(field * 1.7 + 7.1) - 0.5) * shape.y;
    float ribbon = pow(saturate(0.5 + 0.5 * sin(lane * 6.2831853 + branching)), max(shape.w, 0.1));
    // Exponential variation gives narrow, fast tendrils beside broad, slower deposits.
    float speed = exp2(lerp(-0.5, 1.0, ribbon) * strokes.z);
    return length(float3(along / max(shape.x, 1.0), across + bend, depth)) / max(speed, 0.001);
}

void MudaacPaint_float(float3 Position, float4 BaseColor, float4 Surface,
    float4 PaintA, float4 PaintB, float4 PaintC, float4 Center,
    float4 RestX, float4 RestY, float4 RestZ, float4 RestOrigin, float4 Travel, float4 Dynamics,
    float4 Style, float4 Flow, float4 Dry, float4 Seed, float4 Spread, float4 Noise, float4 Detail,
    float4 Stretch, float4 Drift, float4 NoiseSeed, float4 Strokes, float4 StrokeShape,
    float4 StrokeAxis, float4 StrokeSide, float4 Psyche, float4 Warp, float4 Brush, float4 Texture,
    out float3 Color, out float Smoothness, out float Metallic, out float3 Glow)
{
    // Object coordinates are reconstructed in the authored pose. Tile movement cannot swim the paint.
    float3 world = RestOrigin.xyz + Position.x * RestX.xyz + Position.y * RestY.xyz + Position.z * RestZ.xyz;
    float continuousDistance = 1e20;
    [loop]
    for (int source = 0; source < min(16, (int)Spread.w); source++)
    {
        float3 relative = world - _MudaacPaintSources[source].xyz;
        float radial = length(relative);
        float stroked = radial;
        if (Strokes.x > 0.0)
            stroked = MudaacStrokeDistance(relative, world, StrokeAxis.xyz, StrokeSide.xyz,
                Strokes, StrokeShape, NoiseSeed.xyz);
        continuousDistance = min(continuousDistance, lerp(radial, stroked, Strokes.x));
    }
    float distance = lerp(continuousDistance, Center.w, Spread.x);
    float3 p = world * max(Style.x, 0.01) + Seed.xyz;
    // At the continuous end every fragment sees the same field regardless of mesh partitioning.
    // At the tile end each surface samples one value and fills uniformly.
    float3 noisePosition = lerp(world, Center.xyz, Spread.x) * Noise.x * max(abs(Stretch.xyz), 0.001) + NoiseSeed.xyz;
    float edgeNoise = MudaacSpreadNoise(noisePosition, Noise, Detail, Dynamics.x * Noise.y, Drift.xyz);
    float signedFront = Travel.y - distance + edgeNoise * Spread.y;
    float edgeWidth = max(Travel.z, fwidth(signedFront) * 0.6);
    float coverage = smoothstep(-edgeWidth, edgeWidth, signedFront) * saturate(Travel.w);
    float arrival = Surface.z + (distance - Travel.x) * Surface.w;
    float wetness = 1.0 - saturate(max(0.0, Dynamics.x - arrival) / max(Dynamics.z, 0.01));
    float motion = Dynamics.x * max(0.0, Dynamics.y) * lerp(max(0.0, Dry.z), 1.0, wetness);
    float3 moving = p - Flow.xyz * motion;
    float3 distortion = float3(MudaacPaintNoise(moving * Warp.y + float3(motion * 0.09, 2.1, 8.7)),
        MudaacPaintNoise(moving * Warp.y + float3(9.2, motion * 0.07, 3.6)),
        MudaacPaintNoise(moving * Warp.y + float3(4.8, 7.3, motion * 0.11))) - 0.5;
    moving += distortion * Warp.x;
    float along = dot(moving, StrokeAxis.xyz);
    float across = dot(moving, StrokeSide.xyz);
    float swirlAngle = (MudaacPaintNoise(moving * Psyche.z + 13.1) - 0.5) * Psyche.y * 6.2831853;
    float sine, cosine;
    sincos(swirlAngle, sine, cosine);
    // Rotate a bounded displacement rather than the absolute coordinate: distant facade
    // sections retain broad curls instead of collapsing into subpixel noise.
    float2 swirlOffset = float2(sin(across * Psyche.z), cos(along * Psyche.z));
    float2 swirled = float2(cosine * swirlOffset.x - sine * swirlOffset.y,
        sine * swirlOffset.x + cosine * swirlOffset.y) * Psyche.y;
    moving += StrokeAxis.xyz * swirled.x + StrokeSide.xyz * swirled.y;
    float warp = MudaacPaintNoise(moving * 0.7 + float3(0.0, motion * 0.13, 0.0));
    float3 warped = moving + (warp - 0.5) * Dynamics.w * 3.0;
    float pigment = MudaacPaintNoise(warped) * 0.65 + MudaacPaintNoise(warped * 2.13 + 3.7) * 0.35;
    float veins = pow(saturate(0.5 + 0.5 * sin((pigment * 5.0 + moving.y * 0.35) * 16.0)), 18.0);
    float mixValue = saturate(pigment);
    mixValue = saturate((mixValue - 0.5) * (1.0 + max(0.0, Style.z) * 2.5) + 0.5);
    float3 ink = lerp(PaintA.rgb, PaintB.rgb, smoothstep(0.05, 0.72, mixValue));
    ink = lerp(ink, PaintC.rgb, saturate(smoothstep(0.72, 0.96, mixValue) + veins * Dynamics.w * 0.25));
    float palettePhase = (pigment * (1.0 + Psyche.x * 3.0) + warp * Dynamics.w * 0.25
        + motion * Psyche.w) * 6.2831853;
    float3 paletteWeights = pow(0.5 + 0.5 * cos(palettePhase + float3(0, 2.0943951, 4.1887902)), 2.0);
    float3 cyclicInk = (PaintA.rgb * paletteWeights.x + PaintB.rgb * paletteWeights.y + PaintC.rgb * paletteWeights.z)
        / max(0.001, paletteWeights.x + paletteWeights.y + paletteWeights.z);
    ink = lerp(ink, cyclicInk, saturate(Psyche.x));
    float gray = dot(ink, float3(0.2126, 0.7152, 0.0722));
    ink = max(0.0, lerp(gray.xxx, ink, Texture.z));
    float brushSine, brushCosine;
    sincos(Brush.w, brushSine, brushCosine);
    float2 brushUV = float2(dot(world, StrokeAxis.xyz), dot(world, StrokeSide.xyz));
    brushUV = float2(brushCosine * brushUV.x - brushSine * brushUV.y,
        brushSine * brushUV.x + brushCosine * brushUV.y);
    float3 brushPosition = float3(brushUV.x / max(Brush.z, 1.0), brushUV.y, dot(world, cross(StrokeAxis.xyz, StrokeSide.xyz)))
        * Brush.y + Seed.xyz;
    float broken = MudaacPaintNoise(brushPosition * float3(0.37, 0.13, 1.0));
    float bristlePhase = brushPosition.y * 6.2831853 + broken * Texture.x * 7.0;
    float bristle = sin(bristlePhase) * saturate(1.0 - fwidth(bristlePhase) / 6.2831853);
    float brushNoise = (MudaacPaintNoise(brushPosition) - 0.5) * 2.0;
    float textureValue = bristle * 0.35 + brushNoise * Texture.x * 0.35;
    float grain = (MudaacPaintNoise(world * Texture.y + Seed.xyz) - 0.5) * max(0.0, Style.w);
    float rim = exp(-abs(signedFront) / max(Travel.z * 2.0, 0.01));
    ink *= exp2(clamp(textureValue * Brush.x, -8.0, 8.0)) * max(0.0, 1.0 + grain - rim * 0.25);
    Color = lerp(BaseColor.rgb, ink, coverage);
    float paintSmoothness = saturate(lerp(Dry.x, Dry.y, wetness) - abs(brushNoise) * Brush.x * Texture.x * 0.08);
    Smoothness = lerp(Surface.y, paintSmoothness, coverage);
    Metallic = lerp(Surface.x, 0.0, coverage);
    Glow = ink * coverage * max(0.0, Dry.w) * (0.3 + wetness * 0.7);
}

#endif
