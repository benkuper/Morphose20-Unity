using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Mudaac/Modifiers/Organic Filaments")]
[ExecuteAlways, DisallowMultipleComponent]
public class MudaacTileOrganicFilamentsModifier : MudaacTileModifier
{
    [Header("Pattern")]
    public Material filamentMaterial;
    [Tooltip("Approximate size of the organic cells in world metres.")]
    [Min(0.02f)] public float cellSize = 0.85f;
    [Tooltip("Zero gives a continuous facade-wide field. Higher values let each tile bend and vary the field across its whole surface.")]
    [Range(0f, 1f)] public float tileInfluence = 0.35f;
    [Range(0f, 2f)] public float distortion = 0.65f;
    [Tooltip("How much the cells breathe and change shape.")]
    [Range(0f, 1f)] public float cellMotion = 0.7f;
    public int seed = 7;

    [Header("Membranes And Strands")]
    [Tooltip("Membrane width in world metres. Zero removes the membrane core.")]
    [Min(0f)] public float membraneWidth = 0.006f;
    [Range(0f, 2f)] public float strandStrength = 0.55f;
    [Range(4f, 150f)] public float strandDensity = 65f;
    [Range(0f, 1f)] public float mistStrength = 0.12f;
    [Min(0f)] public float glowWidth = 0.07f;
    [Range(0f, 2f)] public float glowStrength = 0.35f;
    [Range(0f, 4f)] public float junctionStrength = 1.4f;
    [Range(0f, 1f)] public float pulseAmount = 0.65f;

    [Header("Appearance")]
    [ColorUsage(false, true)] public Color filamentColor = Color.white;
    [ColorUsage(false, true)] public Color backgroundColor = Color.black;
    [Tooltip("Unlit brightness, independent of scene lighting and camera exposure.")]
    [Min(0f)] public float brightness = 1.6f;
    [Tooltip("Zero keeps the original tile base colour behind the filaments. One uses Background Color.")]
    [Range(0f, 1f)] public float backgroundBlend = 1f;

    [Header("Animation")]
    [Min(0f)] public float speed = 0.5f;
    [Tooltip("Travel in the controller's facade plane, in metres per animation second.")]
    public Vector2 drift = new Vector2(0.015f, 0.04f);
    public bool paused;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int NexusColorId = Shader.PropertyToID("_Base_Color");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int FilamentColorId = Shader.PropertyToID("_OrganicColor");
    private static readonly int BackgroundId = Shader.PropertyToID("_OrganicBackground");
    private static readonly int RestXId = Shader.PropertyToID("_OrganicRestX");
    private static readonly int RestYId = Shader.PropertyToID("_OrganicRestY");
    private static readonly int RestZId = Shader.PropertyToID("_OrganicRestZ");
    private static readonly int RestOriginId = Shader.PropertyToID("_OrganicRestOrigin");
    private static readonly int AxisXId = Shader.PropertyToID("_OrganicAxisX");
    private static readonly int AxisYId = Shader.PropertyToID("_OrganicAxisY");
    private static readonly int RectId = Shader.PropertyToID("_OrganicRect");
    private static readonly int PatternId = Shader.PropertyToID("_OrganicPattern");
    private static readonly int StrandsId = Shader.PropertyToID("_OrganicStrands");
    private static readonly int GlowId = Shader.PropertyToID("_OrganicGlow");
    private static readonly int SurfaceId = Shader.PropertyToID("_OrganicSurface");
    private static readonly int MotionId = Shader.PropertyToID("_OrganicMotion");

    // One clock per controller also supports sharing this modifier through Global Modifiers.
    private sealed class Clock
    {
        public float LastTime = float.NaN;
        public float Time;
    }

    private readonly Dictionary<MudaacTileController, Clock> clocks = new Dictionary<MudaacTileController, Clock>();
    private float animationTime;

    private void Reset()
    {
#if UNITY_EDITOR
        filamentMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacOrganicFilaments.mat");
#endif
    }

    private void OnEnable() => clocks.Clear();

    [ContextMenu("Restart Animation")]
    public void RestartAnimation() => clocks.Clear();

    public override void OnTilesRestored(MudaacTileController controller)
    {
        Clock clock;
        if (clocks.TryGetValue(controller, out clock))
            clock.LastTime = float.NaN;
    }

    public override void UpdateTiles(MudaacTileController controller, IReadOnlyList<MudaacTile> tiles, float time)
    {
        Clock clock;
        if (!clocks.TryGetValue(controller, out clock))
        {
            clock = new Clock();
            clocks.Add(controller, clock);
        }
        if (modifierWeight <= 0f || filamentMaterial == null || !filamentMaterial.HasProperty(PatternId))
        {
            clock.LastTime = float.NaN;
            return;
        }
        if (!paused && !float.IsNaN(clock.LastTime))
            clock.Time += Mathf.Clamp(time - clock.LastTime, 0f, 0.1f) * Mathf.Max(0f, speed);
        clock.LastTime = time;
        animationTime = clock.Time;
        base.UpdateTiles(controller, tiles, time);
    }

    protected override void UpdateTile(MudaacTile tile, MudaacTileController controller, float weight, float time)
    {
        if (weight <= 0f)
            return;
        Matrix4x4 rest = MudaacPaintPropagation.GetRestMatrix(tile);
        Bounds bounds = tile.Renderer.localBounds;
        Vector3 across = controller.transform.right, along = controller.transform.up;
        Vector3 center = rest.MultiplyPoint3x4(bounds.center);
        Vector3 x = rest.GetColumn(0), y = rest.GetColumn(1), z = rest.GetColumn(2);
        Vector3 extent = bounds.extents;
        float halfX = Mathf.Abs(Vector3.Dot(across, x)) * extent.x
            + Mathf.Abs(Vector3.Dot(across, y)) * extent.y + Mathf.Abs(Vector3.Dot(across, z)) * extent.z;
        float halfY = Mathf.Abs(Vector3.Dot(along, x)) * extent.x
            + Mathf.Abs(Vector3.Dot(along, y)) * extent.y + Mathf.Abs(Vector3.Dot(along, z)) * extent.z;
        // Authored centres seed variation without depending on tile-list ordering or animated poses.
        float variation = MudaacNoise.Sample01(center, 0f, seed, 211);
        tile.SetSharedEffectMaterial(filamentMaterial);
        for (int i = 0; i < tile.Materials.Length; i++)
        {
            Material original = tile.Materials[i];
            MaterialPropertyBlock block = tile.GetMaterialBlock(i);
            int colorId = original != null && original.HasProperty(BaseColorId) ? BaseColorId
                : original != null && original.HasProperty(NexusColorId) ? NexusColorId : ColorId;
            block.SetColor(BaseColorId, block.HasProperty(colorId) ? block.GetColor(colorId)
                : original != null && original.HasProperty(colorId) ? original.GetColor(colorId) : Color.white);
            block.SetColor(FilamentColorId, filamentColor);
            block.SetColor(BackgroundId, backgroundColor);
            block.SetVector(RestXId, rest.GetColumn(0));
            block.SetVector(RestYId, rest.GetColumn(1));
            block.SetVector(RestZId, rest.GetColumn(2));
            block.SetVector(RestOriginId, rest.GetColumn(3));
            block.SetVector(AxisXId, across);
            block.SetVector(AxisYId, along);
            block.SetVector(RectId, new Vector4(Vector3.Dot(center, across), Vector3.Dot(center, along),
                Mathf.Max(0.0001f, halfX), Mathf.Max(0.0001f, halfY)));
            block.SetVector(PatternId, new Vector4(Mathf.Max(0.02f, cellSize), Mathf.Clamp01(tileInfluence),
                Mathf.Clamp(distortion, 0f, 2f), Mathf.Clamp01(cellMotion)));
            block.SetVector(StrandsId, new Vector4(Mathf.Max(0f, membraneWidth), Mathf.Clamp(strandDensity, 4f, 150f),
                Mathf.Clamp(strandStrength, 0f, 2f), Mathf.Clamp01(mistStrength)));
            block.SetVector(GlowId, new Vector4(Mathf.Max(0f, glowWidth), Mathf.Clamp(glowStrength, 0f, 2f),
                Mathf.Clamp(junctionStrength, 0f, 4f), Mathf.Clamp01(pulseAmount)));
            block.SetVector(SurfaceId, new Vector4(weight, Mathf.Max(0f, brightness), Mathf.Clamp01(backgroundBlend), variation));
            block.SetVector(MotionId, new Vector4(animationTime, seed % 10000, drift.x, drift.y));
        }
    }
}
