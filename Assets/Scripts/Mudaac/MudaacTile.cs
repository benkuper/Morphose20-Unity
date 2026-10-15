using UnityEngine;

// A binding to an existing renderer, not a component added to the imported model.
public sealed class MudaacTile
{
    public Renderer Renderer { get; }
    public Transform Transform { get; }
    public Vector3 RestLocalPosition { get; }
    public Quaternion RestLocalRotation { get; }
    public Vector3 RestLocalScale { get; }
    public bool RestVisible { get; }
    public Vector3 LocalCenter { get; }
    public Vector3 LocalPosition;
    public Quaternion LocalRotation;
    public Vector3 LocalScale;
    public bool Visible;

    private readonly MaterialPropertyBlock originalBlock = new MaterialPropertyBlock();
    private readonly MaterialPropertyBlock[] originalSlots;
    private readonly MaterialPropertyBlock[] workingSlots;
    private bool materialPrepared;
    private bool materialApplied;
    private Material[] replacementMaterials;
    private bool replacementPrepared;
    private bool replacementApplied;

    public Material[] Materials { get; }
    public bool IsValid => Renderer != null && Transform != null;
    public Vector3 RestWorldCenter
    {
        get
        {
            Vector3 center = RestLocalPosition + RestLocalRotation * Vector3.Scale(RestLocalScale, LocalCenter);
            return Transform.parent != null ? Transform.parent.TransformPoint(center) : center;
        }
    }
    public Quaternion RestWorldRotation => Transform.parent != null
        ? Transform.parent.rotation * RestLocalRotation : RestLocalRotation;

    public MudaacTile(Renderer renderer) : this(renderer, renderer.transform.localPosition,
        renderer.transform.localRotation, renderer.transform.localScale)
    {
    }

    public MudaacTile(Renderer renderer, Vector3 restLocalPosition, Quaternion restLocalRotation, Vector3 restLocalScale)
        : this(renderer, restLocalPosition, restLocalRotation, restLocalScale, renderer.enabled)
    {
    }

    public MudaacTile(Renderer renderer, Vector3 restLocalPosition, Quaternion restLocalRotation, Vector3 restLocalScale,
        bool restVisible)
    {
        Renderer = renderer;
        Transform = renderer.transform;
        RestLocalPosition = restLocalPosition;
        RestLocalRotation = restLocalRotation;
        RestLocalScale = restLocalScale;
        RestVisible = restVisible;
        LocalCenter = renderer.localBounds.center;
        Materials = renderer.sharedMaterials;
        renderer.GetPropertyBlock(originalBlock);
        originalSlots = new MaterialPropertyBlock[Materials.Length];
        workingSlots = new MaterialPropertyBlock[Materials.Length];
        for (int i = 0; i < Materials.Length; i++)
        {
            originalSlots[i] = new MaterialPropertyBlock();
            workingSlots[i] = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(originalSlots[i], i);
        }
        BeginFrame();
    }

    public void BeginFrame()
    {
        LocalPosition = RestLocalPosition;
        LocalRotation = RestLocalRotation;
        LocalScale = RestLocalScale;
        Visible = RestVisible;
        materialPrepared = false;
        replacementPrepared = false;
    }

    // Keep the mesh centre fixed even when the imported origin is off-centre.
    public void SetRotationAndScale(Quaternion rotation, Vector3 scale)
    {
        Vector3 center = LocalPosition + LocalRotation * Vector3.Scale(LocalScale, LocalCenter);
        LocalRotation = rotation;
        LocalScale = scale;
        LocalPosition = center - LocalRotation * Vector3.Scale(LocalScale, LocalCenter);
    }

    public Vector3 WorldVectorToParent(Vector3 vector) => Transform.parent != null
        ? Transform.parent.InverseTransformVector(vector) : vector;

    public MaterialPropertyBlock GetMaterialBlock(int slot)
    {
        if (!materialPrepared)
        {
            RestoreMaterials();
            for (int i = 0; i < workingSlots.Length; i++)
            {
                if (originalSlots[i].isEmpty)
                    Renderer.GetPropertyBlock(workingSlots[i]);
                else
                    Renderer.GetPropertyBlock(workingSlots[i], i);
            }
            materialPrepared = true;
        }
        return workingSlots[slot];
    }

    // A shared effect material is temporary, just like the working property blocks.
    public void SetSharedEffectMaterial(Material material)
    {
        if (material == null)
            return;
        if (replacementMaterials == null)
            replacementMaterials = new Material[Materials.Length];
        for (int i = 0; i < replacementMaterials.Length; i++)
        {
            if (replacementApplied && replacementMaterials[i] != material)
                replacementApplied = false;
            replacementMaterials[i] = material;
        }
        replacementPrepared = true;
    }

    public void Apply()
    {
        if (!IsValid)
            return;
        Transform.SetLocalPositionAndRotation(LocalPosition, LocalRotation);
        Transform.localScale = LocalScale;
        if (Renderer.enabled != Visible)
            Renderer.enabled = Visible;
        if (replacementPrepared && !replacementApplied)
        {
            Renderer.sharedMaterials = replacementMaterials;
            replacementApplied = true;
        }
        else if (!replacementPrepared && replacementApplied)
            RestoreSharedMaterials();
        if (materialPrepared)
        {
            for (int i = 0; i < workingSlots.Length; i++)
                Renderer.SetPropertyBlock(workingSlots[i], i);
            materialApplied = true;
        }
        else if (materialApplied)
            RestoreMaterials();
    }

    public void Restore()
    {
        if (!IsValid)
            return;
        Transform.SetLocalPositionAndRotation(RestLocalPosition, RestLocalRotation);
        Transform.localScale = RestLocalScale;
        if (Renderer.enabled != RestVisible)
            Renderer.enabled = RestVisible;
        if (replacementApplied)
            RestoreSharedMaterials();
        if (materialApplied)
            RestoreMaterials();
    }

    private void RestoreSharedMaterials()
    {
        Renderer.sharedMaterials = Materials;
        replacementApplied = false;
    }

    private void RestoreMaterials()
    {
        Renderer.SetPropertyBlock(originalBlock.isEmpty ? null : originalBlock);
        for (int i = 0; i < originalSlots.Length; i++)
            Renderer.SetPropertyBlock(originalSlots[i].isEmpty ? null : originalSlots[i], i);
        materialApplied = false;
    }
}
