using UnityEngine;

[AddComponentMenu("Mudaac/Modifiers/Scale")]
public class MudaacTileScaleModifier : MudaacTileModifier
{
    public Vector3 scale = Vector3.one;

    protected override void UpdateTile(MudaacTile tile, MudaacTileController controller, float weight, float time)
    {
        tile.SetRotationAndScale(tile.LocalRotation,
            Vector3.Scale(tile.LocalScale, Vector3.Lerp(Vector3.one, scale, weight)));
    }
}
