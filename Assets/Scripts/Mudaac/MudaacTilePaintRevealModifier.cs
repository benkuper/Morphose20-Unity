using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Mudaac/Modifiers/Paint Reveal")]
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MudaacTileController))]
public class MudaacTilePaintRevealModifier : MudaacTileModifier
{
    public enum LoopMode { Once, Restart, PaintAndErase }

    [Header("Brush Swipes")]
    [Range(1, 32)] public int strokeCount = 6;
    [Tooltip("0: vertical brush swipes. Rotate the brush in the controller's facade plane.")]
    [Range(-180f, 180f)] public float brushAngle;
    public bool startFromRight = true;
    public bool firstStrokeUpwards = true;
    public bool alternateDirection = true;
    [Range(0f, 0.5f)] public float strokeOverlap = 0.18f;
    [Tooltip("Uneven brush tips and long stray bristles, as a fraction of the stroke length.")]
    [Range(0f, 0.5f)] public float edgeRoughness = 0.18f;
    [Range(0f, 0.8f)] public float strokeSpeedVariation = 0.2f;
    public int seed = 7;

    [Header("Paint")]
    public Material revealMaterial;
    [ColorUsage(false, true)] public Color paintColor = new Color(0.08f, 0.9f, 0.12f);
    [Range(10f, 600f)] public float bristleDensity = 180f;
    [Range(0f, 1f)] public float bristleStrength = 0.3f;
    [Tooltip("Fine gaps left between the bristles. Zero gives completely opaque paint.")]
    [Range(0f, 0.5f)] public float dryBrush = 0.08f;
    [Range(0f, 1f)] public float paintSmoothness = 0.45f;
    [Min(0f)] public float emission;

    [Header("Playback")]
    public bool automaticReveal = true;
    [Range(0f, 1f)] public float progress;
    [Min(0.01f)] public float revealDuration = 1.4f;
    public bool paused;
    public LoopMode loopMode = LoopMode.PaintAndErase;
    [Min(0f)] public float paintedHoldTime = 0.5f;
    [Min(0.01f)] public float eraseDuration = 1.4f;
    [Min(0f)] public float emptyHoldTime = 0.5f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int NexusColorId = Shader.PropertyToID("_Base_Color");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int SurfaceId = Shader.PropertyToID("_RevealSurface");
    private static readonly int PaintId = Shader.PropertyToID("_RevealColor");
    private static readonly int RestXId = Shader.PropertyToID("_RevealRestX");
    private static readonly int RestYId = Shader.PropertyToID("_RevealRestY");
    private static readonly int RestZId = Shader.PropertyToID("_RevealRestZ");
    private static readonly int RestOriginId = Shader.PropertyToID("_RevealRestOrigin");
    private static readonly int AxisXId = Shader.PropertyToID("_RevealAxisX");
    private static readonly int AxisYId = Shader.PropertyToID("_RevealAxisY");
    private static readonly int BoundsId = Shader.PropertyToID("_RevealBounds");
    private static readonly int ProgressId = Shader.PropertyToID("_RevealProgress");
    private static readonly int BrushId = Shader.PropertyToID("_RevealBrush");
    private static readonly int StyleId = Shader.PropertyToID("_RevealStyle");
    private static readonly int TimingId = Shader.PropertyToID("_RevealTiming");

    private readonly MudaacPaintPropagation geometry = new MudaacPaintPropagation();
    private MudaacTileController owner;
    private Vector3 across, along;
    private Vector2 minimum, size;
    private bool geometryDirty = true;
    private float elapsed;
    private float lastTime = float.NaN;

    public float RevealProgress { get; private set; }
    public float EraseProgress { get; private set; }
    public float CurrentProgress => RevealProgress * (1f - EraseProgress);

    private void Reset()
    {
#if UNITY_EDITOR
        revealMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacPaintReveal.mat");
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

    [ContextMenu("Restart Reveal")]
    public void RestartReveal()
    {
        elapsed = RevealProgress = EraseProgress = 0f;
        lastTime = float.NaN;
    }

    public override void OnTilesRestored(MudaacTileController controller)
    {
        if (controller == owner)
            lastTime = float.NaN;
    }

    public override void UpdateTiles(MudaacTileController controller, IReadOnlyList<MudaacTile> tiles, float time)
    {
        if (controller != owner)
            return;
        if (modifierWeight <= 0f || revealMaterial == null || !revealMaterial.HasProperty(ProgressId))
        {
            lastTime = float.NaN;
            return;
        }
        float angle = brushAngle * Mathf.Deg2Rad;
        Vector3 x = controller.transform.right * Mathf.Cos(angle) + controller.transform.up * Mathf.Sin(angle);
        Vector3 y = -controller.transform.right * Mathf.Sin(angle) + controller.transform.up * Mathf.Cos(angle);
        if (geometryDirty || across != x || along != y || !geometry.Matches(tiles))
        {
            across = x;
            along = y;
            geometry.Rebuild(tiles, Array.Empty<Vector3>(), default);
            RebuildProjection();
            geometryDirty = false;
        }
        float delta = float.IsNaN(lastTime) ? 0f : Mathf.Clamp(time - lastTime, 0f, 0.1f);
        lastTime = time;
        if (!paused && automaticReveal)
            elapsed += delta;
        UpdatePlayback();
        if (RevealProgress > 0f && EraseProgress < 1f)
            base.UpdateTiles(controller, tiles, time);
    }

    private void UpdatePlayback()
    {
        if (!automaticReveal)
        {
            RevealProgress = Mathf.Clamp01(progress);
            EraseProgress = 0f;
            return;
        }
        float paintTime = Mathf.Max(0.01f, revealDuration);
        float eraseTime = Mathf.Max(0.01f, eraseDuration);
        float hold = Mathf.Max(0f, paintedHoldTime);
        float cycle = paintTime + hold + Mathf.Max(0f, emptyHoldTime);
        if (loopMode == LoopMode.PaintAndErase)
            cycle += eraseTime;
        float phase = loopMode == LoopMode.Once ? elapsed : Mathf.Repeat(elapsed, cycle);
        RevealProgress = Mathf.Clamp01(phase / paintTime);
        EraseProgress = loopMode == LoopMode.PaintAndErase
            ? Mathf.Clamp01((phase - paintTime - hold) / eraseTime) : 0f;
        if (loopMode == LoopMode.Restart && phase >= paintTime + hold)
            RevealProgress = 0f;
    }

    private void RebuildProjection()
    {
        minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var surface in geometry.Surfaces)
        {
            if (!surface.Tile.IsValid)
                continue;
            Bounds bounds = surface.Tile.Renderer.localBounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                Vector3 world = surface.RestMatrix.MultiplyPoint3x4(bounds.center + offset);
                Vector2 point = new Vector2(Vector3.Dot(world, across), Vector3.Dot(world, along));
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
        }
        if (float.IsPositiveInfinity(minimum.x))
            minimum = maximum = Vector2.zero;
        size = Vector2.Max(maximum - minimum, Vector2.one * 0.001f);
    }

    protected override void UpdateTile(MudaacTile tile, MudaacTileController controller, float weight, float time)
    {
        var surface = geometry.Find(tile);
        if (surface == null || weight <= 0f)
            return;
        tile.SetSharedEffectMaterial(revealMaterial);
        for (int i = 0; i < tile.Materials.Length; i++)
        {
            Material original = tile.Materials[i];
            if (original == null)
                continue;
            MaterialPropertyBlock block = tile.GetMaterialBlock(i);
            int colorId = original.HasProperty(BaseColorId) ? BaseColorId
                : original.HasProperty(NexusColorId) ? NexusColorId : ColorId;
            block.SetColor(BaseColorId, original.HasProperty(colorId)
                ? (block.HasProperty(colorId) ? block.GetColor(colorId) : original.GetColor(colorId)) : Color.white);
            block.SetVector(SurfaceId, new Vector4(ReadFloat(block, original, MetallicId, 0f),
                ReadFloat(block, original, SmoothnessId, 0.3f), 0f, 0f));
            block.SetColor(PaintId, paintColor);
            block.SetVector(RestXId, surface.RestMatrix.GetColumn(0));
            block.SetVector(RestYId, surface.RestMatrix.GetColumn(1));
            block.SetVector(RestZId, surface.RestMatrix.GetColumn(2));
            block.SetVector(RestOriginId, surface.RestMatrix.GetColumn(3));
            block.SetVector(AxisXId, new Vector4(across.x, across.y, across.z, minimum.x));
            block.SetVector(AxisYId, new Vector4(along.x, along.y, along.z, minimum.y));
            block.SetVector(BoundsId, new Vector4(size.x, size.y, startFromRight ? 1f : 0f, alternateDirection ? 1f : 0f));
            block.SetVector(ProgressId, new Vector4(RevealProgress, Mathf.Clamp(strokeCount, 1, 32), firstStrokeUpwards ? 1f : 0f, weight));
            block.SetVector(BrushId, new Vector4(Mathf.Clamp(strokeOverlap, 0f, 0.5f), Mathf.Clamp(edgeRoughness, 0f, 0.5f),
                Mathf.Clamp(bristleDensity, 10f, 600f), Mathf.Clamp(dryBrush, 0f, 0.5f)));
            block.SetVector(StyleId, new Vector4(Mathf.Clamp01(bristleStrength), Mathf.Clamp01(paintSmoothness), Mathf.Max(0f, emission), seed % 10000));
            block.SetVector(TimingId, new Vector4(Mathf.Clamp(strokeSpeedVariation, 0f, 0.8f), EraseProgress, 0f, 0f));
        }
    }

    private static float ReadFloat(MaterialPropertyBlock block, Material material, int id, float fallback)
        => material.HasProperty(id) ? (block.HasProperty(id) ? block.GetFloat(id) : material.GetFloat(id)) : fallback;
}
