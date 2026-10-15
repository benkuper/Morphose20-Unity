using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[AddComponentMenu("Mudaac/Modifiers/Propagating Paint")]
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MudaacTileController))]
public class MudaacTilePaintModifier : MudaacTileModifier
{
    [Header("Paint Origin")]
    [Tooltip("Optional movable world-space origin. Otherwise use Origin In Facade Bounds.")]
    public Transform origin;
    public Vector3 originInFacadeBounds = new Vector3(0.98f, 0.12f, 0.5f);
    [Tooltip("Additional world-space origins. Up to sixteen origins in total are supported.")]
    public List<Transform> additionalOrigins = new List<Transform>();

    [Header("Spread")]
    [Tooltip("0: continuous paint across the facade, independent of tile boundaries. 1: whole tiles fill in a randomized order. Values between blend the two.")]
    [Range(0f, 1f)] public float tileRandomization;
    [Min(0f)] public float spreadSpeed = 2f;
    [Min(0.001f)] public float edgeSoftness = 0.2f;

    [Header("Organic Stroke Propagation")]
    [Tooltip("Blend a round front into curving strokes with independent speeds. Tile Randomization still controls continuous versus whole-tile fills.")]
    [Range(0f, 1f)] public float strokePropagation = 0.85f;
    [Tooltip("Preferred stroke direction in world space, projected onto the controller's facade plane.")]
    public Vector3 strokeDirection = Vector3.right;
    [Range(1f, 40f)] public float strokeStretch = 8f;
    [Tooltip("Distance between stroke lanes in metres.")]
    [Min(0.01f)] public float strokeSpacing = 0.75f;
    [Tooltip("Speed difference between lanes, in powers of two. High values create very fast tendrils and slow pools.")]
    [Range(0f, 8f)] public float strokeSpeedVariation = 2f;
    [Tooltip("Sideways bending of strokes, in metres.")]
    [Min(0f)] public float strokeCurvature = 1.5f;
    [Range(0f, 16f)] public float strokeBranching = 2f;
    [Min(0.001f)] public float strokeScale = 0.25f;
    [Range(0.1f, 16f)] public float strokeSharpness = 2f;

    [Header("Spread Noise")]
    [Tooltip("Distortion of the spreading edge, in metres. Zero gives a clean continuous wave.")]
    [FormerlySerializedAs("edgeTurbulence"), Min(0f)] public float spreadNoiseAmount = 0.65f;
    [Tooltip("Spatial frequency in inverse metres. Smaller values make broad paint pools.")]
    [Min(0.001f)] public float spreadNoiseScale = 0.55f;
    [Tooltip("Independent XYZ frequency multipliers. Stretch the noise into drips or horizontal bands.")]
    public Vector3 spreadNoiseStretch = Vector3.one;
    [Range(1, 8)] public int spreadNoiseOctaves = 3;
    [Tooltip("Frequency multiplier between noise layers.")]
    [Range(1f, 8f)] public float spreadNoiseLacunarity = 2f;
    [Tooltip("Strength retained by each finer noise layer.")]
    [Range(0f, 1f)] public float spreadNoisePersistence = 0.5f;
    [Tooltip("Bend noise coordinates into organic folds.")]
    [Min(0f)] public float spreadNoiseWarp = 0.7f;
    [Range(0.01f, 16f)] public float spreadNoiseContrast = 1f;
    [Tooltip("Evolution of spread noise, independent of pigment animation. Zero freezes the edge pattern.")]
    [Min(0f)] public float spreadNoiseSpeed = 0.15f;
    public Vector3 spreadNoiseDrift = new Vector3(0.1f, 0.25f, 0.05f);
    [InspectorName("Seed And Offset")] public MudaacNoiseSettings noise;

    [Header("Playback")]
    public bool automaticSpread = true;
    [Range(0f, 1f)] public float progress;
    public bool paused;
    public bool loop;
    [Min(0f)] public float filledHoldTime = 8f;

    [Header("Pigment")]
    public Material paintMaterial;
    [ColorUsage(false, true)] public Color paintA = new Color(0.035f, 0.14f, 0.3f);
    [ColorUsage(false, true)] public Color paintB = new Color(0.05f, 0.7f, 0.65f);
    [ColorUsage(false, true)] public Color paintC = new Color(1f, 0.38f, 0.13f);
    [Min(0.01f)] public float pigmentScale = 0.55f;
    [Range(0f, 8f)] public float pigmentContrast = 0.65f;
    [Range(0f, 4f)] public float grain = 0.2f;
    public MudaacNoiseSettings pigmentNoise;

    [Header("Psychedelic Distortion")]
    [Tooltip("Cyclic palette bands. Above one increases the number of colour cycles.")]
    [Range(0f, 8f)] public float psychedelic = 1.2f;
    [Min(0f)] public float pigmentDistortion = 3.5f;
    [Min(0.001f)] public float pigmentDistortionScale = 0.8f;
    [Range(0f, 32f)] public float swirlStrength = 3f;
    [Min(0.001f)] public float swirlScale = 0.4f;
    [Min(0f)] public float colorCycleSpeed = 0.3f;
    [Range(0f, 4f)] public float colorSaturation = 1.4f;

    [Header("Brush Texture")]
    [Range(0f, 8f)] public float brushStrength = 1f;
    [Tooltip("Bristle frequency in inverse metres.")]
    [Min(0.01f)] public float brushScale = 12f;
    [Range(1f, 100f)] public float brushStretch = 24f;
    [Range(-180f, 180f)] public float brushAngle;
    [Range(0f, 8f)] public float brushRoughness = 1.2f;
    [Min(0.01f)] public float grainScale = 60f;

    [Header("Continuous Motion And Drying")]
    [Min(0f)] public float motionSpeed = 0.3f;
    public Vector3 pigmentFlow = new Vector3(0.12f, -0.25f, 0.05f);
    [Range(0f, 16f)] public float marbling = 0.7f;
    [Min(0.01f)] public float dryingTime = 12f;
    [Range(0f, 4f)] public float motionWhenDry = 0.4f;
    [Range(0f, 1f)] public float wetSmoothness = 0.8f;
    [Range(0f, 1f)] public float drySmoothness = 0.3f;
    [Min(0f)] public float emission;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int NexusColorId = Shader.PropertyToID("_Base_Color");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int SurfaceId = Shader.PropertyToID("_PaintSurface");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int PaintAId = Shader.PropertyToID("_PaintA");
    private static readonly int PaintBId = Shader.PropertyToID("_PaintB");
    private static readonly int PaintCId = Shader.PropertyToID("_PaintC");
    private static readonly int CenterId = Shader.PropertyToID("_PaintCenter");
    private static readonly int SourcesId = Shader.PropertyToID("_MudaacPaintSources");
    private static readonly int RestXId = Shader.PropertyToID("_PaintRestX");
    private static readonly int RestYId = Shader.PropertyToID("_PaintRestY");
    private static readonly int RestZId = Shader.PropertyToID("_PaintRestZ");
    private static readonly int RestOriginId = Shader.PropertyToID("_PaintRestOrigin");
    private static readonly int TravelId = Shader.PropertyToID("_PaintTravel");
    private static readonly int DynamicsId = Shader.PropertyToID("_PaintDynamics");
    private static readonly int StyleId = Shader.PropertyToID("_PaintStyle");
    private static readonly int FlowId = Shader.PropertyToID("_PaintFlow");
    private static readonly int DryId = Shader.PropertyToID("_PaintDry");
    private static readonly int SeedId = Shader.PropertyToID("_PaintSeed");
    private static readonly int SpreadId = Shader.PropertyToID("_PaintSpread");
    private static readonly int NoiseId = Shader.PropertyToID("_PaintNoise");
    private static readonly int DetailId = Shader.PropertyToID("_PaintNoiseDetail");
    private static readonly int StretchId = Shader.PropertyToID("_PaintNoiseStretch");
    private static readonly int DriftId = Shader.PropertyToID("_PaintNoiseDrift");
    private static readonly int NoiseSeedId = Shader.PropertyToID("_PaintNoiseSeed");
    private static readonly int StrokesId = Shader.PropertyToID("_PaintStrokes");
    private static readonly int StrokeShapeId = Shader.PropertyToID("_PaintStrokeShape");
    private static readonly int StrokeAxisId = Shader.PropertyToID("_PaintStrokeAxis");
    private static readonly int StrokeSideId = Shader.PropertyToID("_PaintStrokeSide");
    private static readonly int PsycheId = Shader.PropertyToID("_PaintPsyche");
    private static readonly int WarpId = Shader.PropertyToID("_PaintWarp");
    private static readonly int BrushId = Shader.PropertyToID("_PaintBrush");
    private static readonly int TextureId = Shader.PropertyToID("_PaintTexture");

    private readonly MudaacPaintPropagation propagation = new MudaacPaintPropagation();
    private readonly List<Vector3> sources = new List<Vector3>(16);
    private readonly Vector4[] shaderSources = new Vector4[16];
    private MudaacTileController owner;
    private bool geometryDirty = true;
    private float lastTime = float.NaN;
    private float clock;
    private float travel;
    private float hold;
    private Vector3 cachedOffset;
    private int cachedSeed;
    private Vector3 strokeAxis, strokeSide;
    private float metricMaximum;

    public MudaacPaintPropagation Propagation => propagation;
    public float CurrentProgress => propagation.MaximumDistance > 0f ? Mathf.Clamp01(travel / FillDistance) : 0f;
    public float AnimationTime => clock;
    private float FillDistance => Mathf.Max(propagation.MaximumDistance, metricMaximum) + Mathf.Max(0f, spreadNoiseAmount)
        + Mathf.Max(0.001f, edgeSoftness) * 2f;

    private void Reset()
    {
#if UNITY_EDITOR
        paintMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacPaint.mat");
#endif
    }

    private void OnEnable()
    {
        owner = GetComponent<MudaacTileController>();
        geometryDirty = true;
        lastTime = float.NaN;
    }

    private void OnDisable()
    {
        if (owner != null)
            owner.RestoreTiles();
    }

    [ContextMenu("Restart Paint")]
    public void RestartPaint()
    {
        clock = travel = hold = 0f;
        lastTime = float.NaN;
        geometryDirty = true;
    }

    [ContextMenu("Refresh Spread")]
    public void RefreshSpread() => geometryDirty = true;

    public override void OnTilesRestored(MudaacTileController controller)
    {
        if (controller == owner)
            lastTime = float.NaN;
    }

    public override void UpdateTiles(MudaacTileController controller, IReadOnlyList<MudaacTile> tiles, float time)
    {
        if (controller != owner)
            return;
        if (modifierWeight <= 0f || paintMaterial == null || !paintMaterial.HasProperty(SpreadId))
        {
            lastTime = float.NaN;
            return;
        }
        GatherSources(controller);
        if (geometryDirty || !propagation.Matches(tiles) || cachedOffset != noise.offset || cachedSeed != noise.seed)
        {
            propagation.Rebuild(tiles, sources, noise);
            cachedOffset = noise.offset;
            cachedSeed = noise.seed;
            geometryDirty = false;
        }
        Vector3 normal = controller.transform.forward.normalized;
        strokeAxis = Vector3.ProjectOnPlane(strokeDirection, normal).normalized;
        if (strokeAxis.sqrMagnitude < 0.001f)
            strokeAxis = controller.transform.right.normalized;
        strokeSide = Vector3.Cross(normal, strokeAxis).normalized;
        Bounds facade = propagation.FacadeBounds;
        Vector3 extents = facade.extents;
        float curvedMaximum = float.PositiveInfinity;
        for (int i = 0; i < sources.Count; i++)
        {
            Vector3 offset = facade.center - sources[i];
            float along = Mathf.Abs(Vector3.Dot(offset, strokeAxis))
                + Vector3.Dot(extents, Abs(strokeAxis));
            float across = Mathf.Abs(Vector3.Dot(offset, strokeSide))
                + Vector3.Dot(extents, Abs(strokeSide));
            float depth = Mathf.Abs(Vector3.Dot(offset, normal)) + Vector3.Dot(extents, Abs(normal));
            float distance = new Vector3(along / Mathf.Max(1f, strokeStretch),
                across + Mathf.Max(0f, strokeCurvature), depth).magnitude;
            curvedMaximum = Mathf.Min(curvedMaximum, distance);
        }
        curvedMaximum *= Mathf.Pow(2f, Mathf.Clamp(strokeSpeedVariation, 0f, 8f) * 0.5f);
        metricMaximum = Mathf.Lerp(propagation.MaximumDistance, curvedMaximum, Mathf.Clamp01(strokePropagation));
        float delta = float.IsNaN(lastTime) ? 0f : Mathf.Clamp(time - lastTime, 0f, 0.1f);
        lastTime = time;
        if (!paused)
        {
            clock += delta;
            if (automaticSpread)
                travel = Mathf.Min(FillDistance, travel + Mathf.Max(0f, spreadSpeed) * delta);
        }
        if (!automaticSpread)
            travel = Mathf.Clamp01(progress) * FillDistance;
        if (!paused && loop && automaticSpread && travel >= FillDistance)
        {
            hold += delta;
            if (hold >= Mathf.Max(0f, filledHoldTime))
            {
                travel = hold = 0f;
                foreach (var surface in propagation.Surfaces)
                    surface.ArrivalTime = float.NaN;
            }
        }
        else
            hold = 0f;
        base.UpdateTiles(controller, tiles, time);
    }

    private void GatherSources(MudaacTileController controller)
    {
        Vector3 fallback = controller.LocalBounds.min + Vector3.Scale(controller.LocalBounds.size,
            new Vector3(Mathf.Clamp01(originInFacadeBounds.x), Mathf.Clamp01(originInFacadeBounds.y), Mathf.Clamp01(originInFacadeBounds.z)));
        int count = 0;
        StoreSource(count++, origin != null ? origin.position : controller.transform.TransformPoint(fallback));
        if (additionalOrigins != null)
            foreach (Transform item in additionalOrigins)
                if (item != null && count < shaderSources.Length)
                    StoreSource(count++, item.position);
        if (sources.Count != count)
        {
            sources.RemoveRange(count, sources.Count - count);
            geometryDirty = true;
        }
    }

    private void StoreSource(int index, Vector3 point)
    {
        if (index == sources.Count)
        {
            sources.Add(point);
            geometryDirty = true;
        }
        else if ((sources[index] - point).sqrMagnitude > 0.000001f)
        {
            sources[index] = point;
            geometryDirty = true;
        }
        shaderSources[index] = point;
    }

    protected override void UpdateTile(MudaacTile tile, MudaacTileController controller, float weight, float time)
    {
        var surface = propagation.Find(tile);
        if (surface == null || weight <= 0f || travel <= 0f)
            return;
        float randomized = surface.RandomDistance / propagation.MaximumDistance * metricMaximum;
        float tileDistance = Mathf.Lerp(surface.Distance, randomized, Mathf.Clamp01(tileRandomization));
        float earliest = Mathf.Lerp(Mathf.Max(0f, surface.Distance - surface.Bounds.extents.magnitude),
            randomized, Mathf.Clamp01(tileRandomization));
        if (strokePropagation <= 0f && earliest > travel + Mathf.Max(0f, spreadNoiseAmount) + Mathf.Max(0.001f, edgeSoftness))
            return;
        if (float.IsNaN(surface.ArrivalTime))
            surface.ArrivalTime = clock + (tileDistance - travel) / Mathf.Max(0.01f, spreadSpeed);
        tile.SetSharedEffectMaterial(paintMaterial);
        for (int i = 0; i < tile.Materials.Length; i++)
        {
            Material original = tile.Materials[i];
            if (original == null)
                continue;
            MaterialPropertyBlock block = tile.GetMaterialBlock(i);
            int colorId = original.HasProperty(BaseColorId) ? BaseColorId : original.HasProperty(NexusColorId) ? NexusColorId : ColorId;
            block.SetColor(BaseColorId, original.HasProperty(colorId)
                ? (block.HasProperty(colorId) ? block.GetColor(colorId) : original.GetColor(colorId)) : Color.white);
            block.SetVector(SurfaceId, new Vector4(original.HasProperty(MetallicId)
                ? (block.HasProperty(MetallicId) ? block.GetFloat(MetallicId) : original.GetFloat(MetallicId)) : 0f,
                original.HasProperty(SmoothnessId)
                ? (block.HasProperty(SmoothnessId) ? block.GetFloat(SmoothnessId) : original.GetFloat(SmoothnessId)) : 0.3f,
                surface.ArrivalTime, 1f / Mathf.Max(0.01f, spreadSpeed)));
            block.SetColor(PaintAId, paintA);
            block.SetColor(PaintBId, paintB);
            block.SetColor(PaintCId, paintC);
            block.SetVector(CenterId, new Vector4(surface.Center.x, surface.Center.y, surface.Center.z, randomized));
            block.SetVectorArray(SourcesId, shaderSources);
            block.SetVector(RestXId, surface.RestMatrix.GetColumn(0));
            block.SetVector(RestYId, surface.RestMatrix.GetColumn(1));
            block.SetVector(RestZId, surface.RestMatrix.GetColumn(2));
            block.SetVector(RestOriginId, surface.RestMatrix.GetColumn(3));
            block.SetVector(TravelId, new Vector4(tileDistance, travel, Mathf.Max(0.001f, edgeSoftness), weight));
            block.SetVector(DynamicsId, new Vector4(clock, motionSpeed, Mathf.Max(0.01f, dryingTime), marbling));
            block.SetVector(StyleId, new Vector4(Mathf.Max(0.01f, pigmentScale), 0f, pigmentContrast, grain));
            block.SetVector(FlowId, pigmentFlow);
            block.SetVector(DryId, new Vector4(drySmoothness, wetSmoothness, motionWhenDry, emission));
            block.SetVector(SeedId, GetNoiseOffset(pigmentNoise));
            block.SetVector(SpreadId, new Vector4(Mathf.Clamp01(tileRandomization), Mathf.Max(0f, spreadNoiseAmount),
                metricMaximum, sources.Count));
            block.SetVector(NoiseId, new Vector4(Mathf.Max(0.001f, spreadNoiseScale), Mathf.Max(0f, spreadNoiseSpeed),
                Mathf.Clamp(spreadNoiseContrast, 0.01f, 16f), Mathf.Clamp(spreadNoiseOctaves, 1, 8)));
            block.SetVector(DetailId, new Vector4(Mathf.Clamp(spreadNoiseLacunarity, 1f, 8f),
                Mathf.Clamp01(spreadNoisePersistence), Mathf.Max(0f, spreadNoiseWarp), 0f));
            block.SetVector(StretchId, spreadNoiseStretch);
            block.SetVector(DriftId, spreadNoiseDrift);
            block.SetVector(NoiseSeedId, GetNoiseOffset(noise));
            block.SetVector(StrokesId, new Vector4(Mathf.Clamp01(strokePropagation), Mathf.Max(0.01f, strokeSpacing),
                Mathf.Clamp(strokeSpeedVariation, 0f, 8f), Mathf.Max(0f, strokeCurvature)));
            block.SetVector(StrokeShapeId, new Vector4(Mathf.Max(1f, strokeStretch), Mathf.Max(0f, strokeBranching),
                Mathf.Max(0.001f, strokeScale), Mathf.Max(0.1f, strokeSharpness)));
            block.SetVector(StrokeAxisId, strokeAxis);
            block.SetVector(StrokeSideId, strokeSide);
            block.SetVector(PsycheId, new Vector4(Mathf.Max(0f, psychedelic), Mathf.Max(0f, swirlStrength),
                Mathf.Max(0.001f, swirlScale), Mathf.Max(0f, colorCycleSpeed)));
            block.SetVector(WarpId, new Vector4(Mathf.Max(0f, pigmentDistortion), Mathf.Max(0.001f, pigmentDistortionScale), 0f, 0f));
            block.SetVector(BrushId, new Vector4(Mathf.Max(0f, brushStrength), Mathf.Max(0.01f, brushScale),
                Mathf.Max(1f, brushStretch), brushAngle * Mathf.Deg2Rad));
            block.SetVector(TextureId, new Vector4(Mathf.Max(0f, brushRoughness), Mathf.Max(0.01f, grainScale),
                Mathf.Max(0f, colorSaturation), 0f));
        }
    }

    private static Vector3 GetNoiseOffset(MudaacNoiseSettings settings)
        => settings.offset + MudaacNoise.SampleVectorSigned(Vector3.zero, 0f, settings.seed, 31) * 37.17f;

    private static Vector3 Abs(Vector3 vector)
        => new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));
}
