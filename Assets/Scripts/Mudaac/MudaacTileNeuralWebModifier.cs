using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Mudaac/Modifiers/Neural Web")]
[ExecuteAlways, DisallowMultipleComponent]
public class MudaacTileNeuralWebModifier : MudaacTileModifier
{
    [Header("Material And Palette")]
    public Material webMaterial;
    [ColorUsage(false, true)] public Color backgroundColor = new Color(0.002f, 0.002f, 0.004f);
    [ColorUsage(false, true)] public Color strandColor = new Color(0.55f, 0.58f, 0.7f);
    [ColorUsage(false, true)] public Color junctionColor = new Color(0.95f, 0.97f, 1f);
    [Min(0f)] public float brightness = 1f;

    [Header("Web Structure")]
    [Tooltip("Size of the largest cells in metres, across the controller's facade plane.")]
    [Min(0.01f)] public float cellSize = 1.4f;
    [Tooltip("Strand core width in metres. Zero removes strands and their glow.")]
    [Min(0f)] public float strandWidth = 0.025f;
    [Min(0f)] public float glowWidth = 0.12f;
    [Range(0f, 2f)] public float glowStrength = 0.32f;
    [Tooltip("Nested webs add smaller filaments inside the large cells. Higher counts cost more GPU time.")]
    [Range(1, 3)] public int layers = 3;
    [Range(0f, 1f)] public float fineStrength = 0.32f;
    [Range(0f, 2f)] public float distortion = 0.35f;
    [Range(0f, 5f)] public float junctionStrength = 1.6f;
    [Range(0f, 1f)] public float grainStrength = 0.13f;
    public int seed = 7;

    [Header("Animation")]
    [Min(0f)] public float animationSpeed = 0.28f;
    [Tooltip("How far the cell junctions wander. Zero still allows drift and light pulses.")]
    [Range(0f, 1f)] public float cellMotion = 0.8f;
    [Tooltip("Pattern travel in facade-plane metres per second, multiplied by Animation Speed.")]
    public Vector2 drift = new Vector2(0.06f, 0.025f);
    [Range(0f, 1f)] public float pulseStrength = 0.35f;
    public bool paused;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int NexusColorId = Shader.PropertyToID("_Base_Color");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BackgroundId = Shader.PropertyToID("_WebBackground");
    private static readonly int StrandId = Shader.PropertyToID("_WebStrandColor");
    private static readonly int JunctionId = Shader.PropertyToID("_WebJunctionColor");
    private static readonly int RestXId = Shader.PropertyToID("_WebRestX");
    private static readonly int RestYId = Shader.PropertyToID("_WebRestY");
    private static readonly int RestZId = Shader.PropertyToID("_WebRestZ");
    private static readonly int RestOriginId = Shader.PropertyToID("_WebRestOrigin");
    private static readonly int AxisXId = Shader.PropertyToID("_WebAxisX");
    private static readonly int AxisYId = Shader.PropertyToID("_WebAxisY");
    private static readonly int ShapeId = Shader.PropertyToID("_WebShape");
    private static readonly int DetailId = Shader.PropertyToID("_WebDetail");
    private static readonly int LightId = Shader.PropertyToID("_WebLight");
    private static readonly int MotionId = Shader.PropertyToID("_WebMotion");
    private static readonly int PulseId = Shader.PropertyToID("_WebPulse");

    private float elapsed;
    private float lastTime = float.NaN;
    public float AnimationTime => elapsed;

    private void Reset() => AssignDefaultMaterial();

    private void AssignDefaultMaterial()
    {
#if UNITY_EDITOR
        webMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacNeuralWeb.mat");
#endif
    }

    private void OnEnable()
    {
        lastTime = float.NaN;
        if (webMaterial == null)
            AssignDefaultMaterial();
    }

    [ContextMenu("Restart Web Animation")]
    public void RestartAnimation()
    {
        elapsed = 0f;
        lastTime = float.NaN;
    }

    public override void OnTilesRestored(MudaacTileController controller) => lastTime = float.NaN;

    public override void UpdateTiles(MudaacTileController controller, IReadOnlyList<MudaacTile> tiles, float time)
    {
        if (modifierWeight <= 0f || webMaterial == null || !webMaterial.HasProperty(ShapeId))
        {
            lastTime = float.NaN;
            return;
        }
        float delta = float.IsNaN(lastTime) ? 0f : Mathf.Clamp(time - lastTime, 0f, 0.1f);
        lastTime = time;
        if (!paused)
            elapsed += delta * Mathf.Max(0f, animationSpeed);
        base.UpdateTiles(controller, tiles, time);
    }

    protected override void UpdateTile(MudaacTile tile, MudaacTileController controller, float weight, float time)
    {
        if (weight <= 0f)
            return;
        // Reconstruct authored geometry so later tile motion carries the pattern with it.
        Matrix4x4 rest = MudaacPaintPropagation.GetRestMatrix(tile);
        Vector3 x = controller.transform.right;
        Vector3 y = controller.transform.up;
        Vector3 origin = controller.transform.position;
        tile.SetSharedEffectMaterial(webMaterial);
        for (int i = 0; i < tile.Materials.Length; i++)
        {
            Material original = tile.Materials[i];
            MaterialPropertyBlock block = tile.GetMaterialBlock(i);
            int colorId = original != null && original.HasProperty(BaseColorId) ? BaseColorId
                : original != null && original.HasProperty(NexusColorId) ? NexusColorId : ColorId;
            block.SetColor(BaseColorId, block.HasProperty(colorId) ? block.GetColor(colorId)
                : original != null && original.HasProperty(colorId) ? original.GetColor(colorId) : Color.white);
            block.SetColor(BackgroundId, backgroundColor);
            block.SetColor(StrandId, strandColor);
            block.SetColor(JunctionId, junctionColor);
            block.SetVector(RestXId, rest.GetColumn(0));
            block.SetVector(RestYId, rest.GetColumn(1));
            block.SetVector(RestZId, rest.GetColumn(2));
            block.SetVector(RestOriginId, rest.GetColumn(3));
            block.SetVector(AxisXId, new Vector4(x.x, x.y, x.z, Vector3.Dot(origin, x)));
            block.SetVector(AxisYId, new Vector4(y.x, y.y, y.z, Vector3.Dot(origin, y)));
            block.SetVector(ShapeId, new Vector4(Mathf.Max(0.01f, cellSize), Mathf.Max(0f, strandWidth),
                Mathf.Max(0f, glowWidth), Mathf.Clamp(distortion, 0f, 2f)));
            block.SetVector(DetailId, new Vector4(Mathf.Clamp(layers, 1, 3), Mathf.Clamp01(fineStrength),
                Mathf.Clamp01(grainStrength), seed % 10000));
            block.SetVector(LightId, new Vector4(Mathf.Max(0f, brightness), Mathf.Clamp(glowStrength, 0f, 2f),
                Mathf.Clamp(junctionStrength, 0f, 5f), weight));
            block.SetVector(MotionId, new Vector4(elapsed, Mathf.Clamp01(cellMotion), drift.x, drift.y));
            block.SetVector(PulseId, new Vector4(Mathf.Clamp01(pulseStrength), 0f, 0f, 0f));
        }
    }
}
