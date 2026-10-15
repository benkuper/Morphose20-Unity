using UnityEngine;

[AddComponentMenu("Mudaac/Modifiers/Offset")]
public class MudaacTileOffsetModifier : MudaacTileModifier
{
    [Tooltip("Offset in metres, independent of the imported model's scale.")]
    public Vector3 offset;
    public CoordinateSpace space = CoordinateSpace.Controller;
    public Transform repelOrigin;
    [Min(0f)] public float repel;

    protected override void UpdateTile(MudaacTile tile, MudaacTileController controller, float weight, float time)
    {
        Vector3 worldOffset = ToWorldDirection(tile, controller, offset, space);
        if (repelOrigin != null)
            worldOffset += (tile.RestWorldCenter - repelOrigin.position).normalized * repel;
        tile.LocalPosition += tile.WorldVectorToParent(worldOffset * weight);
    }
}
