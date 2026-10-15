using UnityEngine;

// The base influencer supplies a spatial noise mask; subclasses add shaped masks.
[AddComponentMenu("Mudaac/Influencers/Noise")]
public class MudaacTileInfluencer : MonoBehaviour
{
    [Range(0f, 1f)] public float weight = 1f;
    [Range(0f, 1f)] public float randomness;
    [Min(0f)] public float randomScale = 1f;
    public float randomSpeed;
    public MudaacNoiseSettings noise;
    public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    public virtual float GetWeightForTile(MudaacTile tile, MudaacTileController controller, float time)
        => Evaluate(GetRandomness(tile.RestWorldCenter, time));

    protected float Evaluate(float value) => Mathf.Clamp01(weight * (animationCurve != null
        ? animationCurve.Evaluate(value) : 1f - value));

    protected float GetRandomness(Vector3 worldPosition, float time)
    {
        return randomness > 0f ? noise.Sample01(worldPosition, randomScale, time, randomSpeed) * randomness : 0f;
    }
}
