using UnityEngine;

[AddComponentMenu("Mudaac/Modifiers/Rotation")]
public class MudaacTileRotationModifier : MudaacTileModifier
{
    [Tooltip("Rotation in degrees around each panel's mesh centre and local axes.")]
    public Vector3 rotation;

    protected override void UpdateTile(MudaacTile tile, MudaacTileController controller, float weight, float time)
    {
        tile.SetRotationAndScale(tile.LocalRotation * Quaternion.Euler(rotation * weight), tile.LocalScale);
    }
}
