using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Mudaac/Modifiers/Liquid Seams")]
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MudaacTileController))]
public class MudaacTileLiquidSeamsModifier : MudaacTileModifier
{
    [Header("Inter-Tile Liquid")]
    public Material seamMaterial;
    [Tooltip("Width on each side of a tile seam, in world metres.")]
    [Min(0f)] public float seamWidth = 0.018f;
    [Tooltip("Rounds the dark tile interiors, leaving liquid pools at their junctions. World metres.")]
    [Min(0f)] public float junctionRadius = 0.22f;
    [Tooltip("Moving liquid bulges added to the fine seams, in world metres.")]
    [Min(0f)] public float flowAmount = 0.12f;
    [Tooltip("Small ripples on the liquid edge, in world metres.")]
    [Min(0f)] public float edgeRoughness = 0.004f;

    [Header("Inside Tile Lines")]
    [Tooltip("Moving cells inside each tile. Zero or one leaves the tile undivided.")]
    [Range(0, 12)] public int interiorCellCount = 4;
    [Tooltip("Half-width of the interior lines, in world metres. Zero leaves only their thicker pools.")]
    [Min(0f)] public float interiorLineWidth = 0.012f;
    [Tooltip("Bend the interior lines, as a fraction of the tile's smaller dimension.")]
    [Range(0f, 0.5f)] public float interiorCurvature = 0.12f;
    [Tooltip("Cell motion relative to Flow Speed. Zero freezes cell positions.")]
    [Min(0f)] public float interiorMotion = 0.35f;

    [Header("Appearance")]
    [ColorUsage(false, true)] public Color liquidColor = Color.white;
    [ColorUsage(false, true)] public Color interiorColor = Color.black;
    [Tooltip("Zero preserves the original tile colour. One uses Interior Color, as in the reference.")]
    [Range(0f, 1f)] public float interiorBlend = 1f;
    [Tooltip("Scene-linear emission. Adjust this to the camera exposure and bloom.")]
    [Min(0f)] public float emission = 400f;
    [Min(0f)] public float haloWidth = 0.02f;
    [Range(0f, 1f)] public float haloStrength = 0.05f;

    [Header("Flow")]
    [Min(0.001f)] public float flowScale = 1.2f;
    [Min(0f)] public float flowSpeed = 0.65f;
    [Tooltip("Travel direction in the controller's facade plane (right / up).")]
    public Vector2 flowDirection = new Vector2(0.4f, 1f);
    public int seed = 7;
    public bool paused;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int NexusColorId = Shader.PropertyToID("_Base_Color");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int LiquidId = Shader.PropertyToID("_SeamLiquid");
    private static readonly int InteriorId = Shader.PropertyToID("_SeamInterior");
    private static readonly int SurfaceId = Shader.PropertyToID("_SeamSurface");
    private static readonly int RestXId = Shader.PropertyToID("_SeamRestX");
    private static readonly int RestYId = Shader.PropertyToID("_SeamRestY");
    private static readonly int RestZId = Shader.PropertyToID("_SeamRestZ");
    private static readonly int RestOriginId = Shader.PropertyToID("_SeamRestOrigin");
    private static readonly int AxisXId = Shader.PropertyToID("_SeamAxisX");
    private static readonly int AxisYId = Shader.PropertyToID("_SeamAxisY");
    private static readonly int RectId = Shader.PropertyToID("_SeamRect");
    private static readonly int ShapeId = Shader.PropertyToID("_SeamShape");
    private static readonly int FlowId = Shader.PropertyToID("_SeamFlow");
    private static readonly int StyleId = Shader.PropertyToID("_SeamStyle");
    private static readonly int DirectionId = Shader.PropertyToID("_SeamDirection");
    private static readonly int NetworkId = Shader.PropertyToID("_SeamNetwork");
    private static readonly int EdgesId = Shader.PropertyToID("_SeamEdges");
    private static readonly int CellRectsId = Shader.PropertyToID("_SeamCellRects");
    private static readonly int CellSeedsId = Shader.PropertyToID("_SeamCellSeeds");
    private static readonly int InteriorLinesId = Shader.PropertyToID("_SeamInteriorLines");

    private readonly MudaacPaintPropagation geometry = new MudaacPaintPropagation();
    private readonly Dictionary<MudaacTile, Vector4> footprints = new Dictionary<MudaacTile, Vector4>();
    private readonly MudaacLiquidSeamNetwork network = new MudaacLiquidSeamNetwork();
    private MudaacTileController owner;
    private Vector3 across, along;
    private float lastTime = float.NaN;
    private float networkReach = -1f;
    public float FlowTime { get; private set; }

    private void Reset()
    {
#if UNITY_EDITOR
        seamMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacLiquidSeams.mat");
#endif
    }

    private void OnEnable()
    {
        owner = GetComponent<MudaacTileController>();
        footprints.Clear();
        networkReach = -1f;
        lastTime = float.NaN;
    }

    private void OnDisable()
    {
        if (owner != null)
            owner.RestoreTiles();
    }

    [ContextMenu("Restart Flow")]
    public void RestartFlow()
    {
        FlowTime = 0f;
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
        if (modifierWeight <= 0f || seamMaterial == null || !seamMaterial.HasProperty(ShapeId))
        {
            lastTime = float.NaN;
            return;
        }
        if (across != controller.transform.right || along != controller.transform.up
            || footprints.Count != tiles.Count || !geometry.Matches(tiles))
        {
            across = controller.transform.right;
            along = controller.transform.up;
            geometry.Rebuild(tiles, Array.Empty<Vector3>(), default);
            RebuildFootprints();
            networkReach = -1f;
        }
        // Include neighboring segments wherever their rounded pools or halos can reach this tile.
        float reach = Mathf.Max(0f, seamWidth) + Mathf.Max(0f, flowAmount) + Mathf.Max(0f, edgeRoughness)
            + Mathf.Max(0f, junctionRadius) * 4f + Mathf.Max(0f, haloWidth) * 8f + 0.01f;
        if (networkReach != reach)
        {
            network.Rebuild(footprints, reach);
            networkReach = reach;
        }
        if (!paused && !float.IsNaN(lastTime))
            FlowTime += Mathf.Clamp(time - lastTime, 0f, 0.1f) * Mathf.Max(0f, flowSpeed);
        lastTime = time;
        base.UpdateTiles(controller, tiles, time);
    }

    private void RebuildFootprints()
    {
        footprints.Clear();
        foreach (var surface in geometry.Surfaces)
        {
            if (!surface.Tile.IsValid)
                continue;
            Bounds bounds = surface.Tile.Renderer.localBounds;
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                Vector3 world = surface.RestMatrix.MultiplyPoint3x4(bounds.center + offset);
                Vector2 point = new Vector2(Vector3.Dot(world, across), Vector3.Dot(world, along));
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            Vector2 center = (min + max) * 0.5f;
            Vector2 half = Vector2.Max((max - min) * 0.5f, Vector2.one * 0.0001f);
            footprints[surface.Tile] = new Vector4(center.x, center.y, half.x, half.y);
        }
    }

    protected override void UpdateTile(MudaacTile tile, MudaacTileController controller, float weight, float time)
    {
        var surface = geometry.Find(tile);
        Vector4 footprint;
        var neighborhood = network.Find(tile);
        if (surface == null || neighborhood == null || weight <= 0f || !footprints.TryGetValue(tile, out footprint))
            return;
        tile.SetSharedEffectMaterial(seamMaterial);
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
            block.SetColor(LiquidId, liquidColor);
            block.SetColor(InteriorId, interiorColor);
            block.SetVector(SurfaceId, new Vector4(ReadFloat(block, original, MetallicId, 0f),
                ReadFloat(block, original, SmoothnessId, 0.3f), Mathf.Clamp01(interiorBlend), weight));
            block.SetVector(RestXId, surface.RestMatrix.GetColumn(0));
            block.SetVector(RestYId, surface.RestMatrix.GetColumn(1));
            block.SetVector(RestZId, surface.RestMatrix.GetColumn(2));
            block.SetVector(RestOriginId, surface.RestMatrix.GetColumn(3));
            block.SetVector(AxisXId, across);
            block.SetVector(AxisYId, along);
            block.SetVector(RectId, footprint);
            block.SetVector(ShapeId, new Vector4(Mathf.Max(0f, seamWidth), Mathf.Max(0f, junctionRadius),
                Mathf.Max(0f, flowAmount), Mathf.Max(0f, edgeRoughness)));
            block.SetVector(FlowId, new Vector4(Mathf.Max(0.001f, flowScale), FlowTime, seed % 10000, 0f));
            block.SetVector(StyleId, new Vector4(Mathf.Max(0f, emission), Mathf.Max(0f, haloWidth), Mathf.Clamp01(haloStrength), 0f));
            block.SetVector(DirectionId, new Vector4(flowDirection.x, flowDirection.y, 0f, 0f));
            block.SetVector(NetworkId, new Vector4(neighborhood.Count, 0f, neighborhood.CellCount, networkReach));
            block.SetVectorArray(EdgesId, neighborhood.Edges);
            block.SetVectorArray(CellRectsId, neighborhood.CellRects);
            block.SetVectorArray(CellSeedsId, neighborhood.CellSeeds);
            block.SetVector(InteriorLinesId, new Vector4(Mathf.Clamp(interiorCellCount, 0, 12),
                Mathf.Max(0f, interiorLineWidth), Mathf.Clamp(interiorCurvature, 0f, 0.5f), Mathf.Max(0f, interiorMotion)));
        }
    }

    private static float ReadFloat(MaterialPropertyBlock block, Material material, int id, float fallback)
        => material.HasProperty(id) ? (block.HasProperty(id) ? block.GetFloat(id) : material.GetFloat(id)) : fallback;
}
