using UnityEngine;

[AddComponentMenu("Mudaac/Modifiers/Wire Volume")]
[ExecuteAlways, DisallowMultipleComponent]
public class MudaacTileWireVolumeModifier : MudaacTileModifier
{
    [Header("Material")]
    public Material wireMaterial;
    [Range(0f, 1f)] public float opacity = 1f;

    [Header("Wire Edges")]
    [ColorUsage(false, true)] public Color edgeColor = new Color(0.75f, 0.75f, 0.75f);
    [Tooltip("Edge width in metres, independent of the imported model's scale.")]
    [Min(0f)] public float edgeWidth = 0.006f;
    [Range(0f, 1f)] public float edgeOpacity = 0.75f;
    [Min(0f)] public float edgeBrightness = 1f;
    [Tooltip("Soft light around each edge, in metres.")]
    [Min(0f)] public float glowWidth = 0.02f;
    [Range(0f, 2f)] public float glowStrength = 0.12f;
    [Tooltip("Visibility of the far edges through the volume.")]
    [Range(0f, 1f)] public float rearEdgeOpacity = 0.35f;

    [Header("Volume And Faces")]
    [ColorUsage(false, true)] public Color volumeColor = new Color(0.35f, 0.35f, 0.35f);
    [Range(0f, 1f)] public float faceOpacity = 0.025f;
    [Tooltip("Interior fog density per metre. Zero removes the interior haze.")]
    [Min(0f)] public float volumeDensity = 0.15f;
    [Tooltip("Extra density at the bottom of each tile, using world up.")]
    [Range(0f, 8f)] public float bottomDensity = 3f;
    [Range(0.1f, 12f)] public float heightFalloff = 4f;
    [Tooltip("Interior samples per pixel. Higher values give smoother fog at a greater GPU cost.")]
    [Range(1, 12)] public int volumeSamples = 6;

    [Header("Texture And Motion")]
    [Min(0.01f)] public float noiseScale = 2.5f;
    [Range(0f, 1f)] public float noiseStrength = 0.65f;
    [Min(0.01f)] public float grainScale = 140f;
    [Range(0f, 1f)] public float grainStrength = 0.65f;
    public int seed = 7;
    [Tooltip("Noise-coordinate drift per second. Zero freezes the texture.")]
    public Vector3 drift = new Vector3(0.04f, 0.08f, 0.02f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int NexusColorId = Shader.PropertyToID("_Base_Color");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EdgeColorId = Shader.PropertyToID("_WireColor");
    private static readonly int FogColorId = Shader.PropertyToID("_WireFogColor");
    private static readonly int CenterId = Shader.PropertyToID("_WireCenter");
    private static readonly int HalfSizeId = Shader.PropertyToID("_WireHalfSize");
    private static readonly int EdgeId = Shader.PropertyToID("_WireEdge");
    private static readonly int SurfaceId = Shader.PropertyToID("_WireSurface");
    private static readonly int VolumeId = Shader.PropertyToID("_WireVolume");
    private static readonly int TextureId = Shader.PropertyToID("_WireTexture");
    private static readonly int MotionId = Shader.PropertyToID("_WireMotion");

    private void Reset()
    {
#if UNITY_EDITOR
        wireMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacWireVolume.mat");
#endif
    }

    protected override void UpdateTile(MudaacTile tile, MudaacTileController controller, float weight, float time)
    {
        if (weight <= 0f || wireMaterial == null || !wireMaterial.HasProperty(VolumeId))
            return;

        Bounds bounds = tile.Renderer.localBounds;
        Vector3 halfSize = bounds.extents;
        Vector3 motion = drift * time;

        tile.SetSharedEffectMaterial(wireMaterial);
        for (int i = 0; i < tile.Materials.Length; i++)
        {
            Material original = tile.Materials[i];
            MaterialPropertyBlock block = tile.GetMaterialBlock(i);
            int colorId = original != null && original.HasProperty(BaseColorId) ? BaseColorId
                : original != null && original.HasProperty(NexusColorId) ? NexusColorId : ColorId;
            block.SetColor(BaseColorId, block.HasProperty(colorId) ? block.GetColor(colorId)
                : original != null && original.HasProperty(colorId) ? original.GetColor(colorId) : Color.white);
            block.SetColor(EdgeColorId, edgeColor);
            block.SetColor(FogColorId, volumeColor);
            block.SetVector(CenterId, bounds.center);
            block.SetVector(HalfSizeId, new Vector4(halfSize.x, halfSize.y, halfSize.z, Mathf.Clamp01(opacity)));
            block.SetVector(EdgeId, new Vector4(Mathf.Max(0f, edgeWidth), Mathf.Max(0f, glowWidth),
                Mathf.Max(0f, glowStrength), Mathf.Clamp01(rearEdgeOpacity)));
            block.SetVector(SurfaceId, new Vector4(Mathf.Clamp01(faceOpacity), Mathf.Clamp01(edgeOpacity),
                Mathf.Max(0f, edgeBrightness), weight));
            block.SetVector(VolumeId, new Vector4(Mathf.Max(0f, volumeDensity), Mathf.Max(0f, bottomDensity),
                Mathf.Max(0.1f, heightFalloff), Mathf.Clamp(volumeSamples, 1, 12)));
            block.SetVector(TextureId, new Vector4(Mathf.Max(0.01f, noiseScale), Mathf.Clamp01(noiseStrength),
                Mathf.Max(0.01f, grainScale), seed % 10000));
            block.SetVector(MotionId, new Vector4(motion.x, motion.y, motion.z, Mathf.Clamp01(grainStrength)));
        }
    }
}
