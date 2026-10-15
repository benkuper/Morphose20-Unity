using UnityEngine;

[AddComponentMenu("Mudaac/Modifiers/Noise Motion")]
public class MudaacTileMoveModifier : MudaacTileModifier
{
    public Vector3 motionAmplitude;
    public Vector3 rotationAmplitude;
    public float frequency = 1f;
    [Min(0f)] public float spatialScale = 0.5f;
    public CoordinateSpace space = CoordinateSpace.Controller;
    public MudaacNoiseSettings noise;

    protected override void UpdateTile(MudaacTile tile, MudaacTileController controller, float weight, float time)
    {
        Vector3 position = tile.RestWorldCenter;
        Vector3 motion = Vector3.Scale(noise.SampleVectorSigned(position, spatialScale, time, frequency), motionAmplitude) * weight;
        tile.LocalPosition += tile.WorldVectorToParent(ToWorldDirection(tile, controller, motion, space));
        Vector3 rotation = Vector3.Scale(noise.SampleVectorSigned(position, spatialScale, time, frequency, 3), rotationAmplitude) * weight;
        tile.SetRotationAndScale(tile.LocalRotation * Quaternion.Euler(rotation), tile.LocalScale);
    }

}
