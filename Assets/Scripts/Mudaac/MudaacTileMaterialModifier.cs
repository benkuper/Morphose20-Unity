using UnityEngine;

[AddComponentMenu("Mudaac/Modifiers/Material")]
public class MudaacTileMaterialModifier : MudaacTileModifier
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int NexusColorId = Shader.PropertyToID("_Base_Color");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissiveId = Shader.PropertyToID("_EmissiveColor");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    public bool affectColor = true;
    [GradientUsage(true)] public Gradient colorOverWeight = new Gradient();
    public bool affectEmission;
    [ColorUsage(false, true)] public Color emissionColor = Color.black;
    public bool affectMetallic;
    [Range(0f, 1f)] public float metallic;
    public bool affectSmoothness;
    [Range(0f, 1f)] public float smoothness = 0.5f;

    protected override void UpdateTile(MudaacTile tile, MudaacTileController controller, float weight, float time)
    {
        if (weight <= 0f || !(affectColor || affectEmission || affectMetallic || affectSmoothness))
            return;
        for (int i = 0; i < tile.Materials.Length; i++)
        {
            Material material = tile.Materials[i];
            if (material == null)
                continue;
            MaterialPropertyBlock block = tile.GetMaterialBlock(i);
            if (affectColor)
            {
                int id = material.HasProperty(BaseColorId) ? BaseColorId
                    : material.HasProperty(NexusColorId) ? NexusColorId : ColorId;
                BlendColor(block, material, id, colorOverWeight.Evaluate(weight), weight);
            }
            if (affectEmission)
                BlendColor(block, material, material.HasProperty(EmissiveId) ? EmissiveId : EmissionId,
                    emissionColor, weight);
            if (affectMetallic)
                BlendFloat(block, material, MetallicId, metallic, weight);
            if (affectSmoothness)
                BlendFloat(block, material, SmoothnessId, smoothness, weight);
        }
    }

    private static void BlendColor(MaterialPropertyBlock block, Material material, int id, Color target, float weight)
    {
        if (material.HasProperty(id))
            block.SetColor(id, Color.Lerp(block.HasProperty(id) ? block.GetColor(id) : material.GetColor(id), target, weight));
    }

    private static void BlendFloat(MaterialPropertyBlock block, Material material, int id, float target, float weight)
    {
        if (material.HasProperty(id))
            block.SetFloat(id, Mathf.Lerp(block.HasProperty(id) ? block.GetFloat(id) : material.GetFloat(id), target, weight));
    }
}
