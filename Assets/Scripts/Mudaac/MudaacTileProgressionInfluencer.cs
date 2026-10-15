using UnityEngine;

[AddComponentMenu("Mudaac/Influencers/Progression")]
public class MudaacTileProgressionInfluencer : MudaacTileInfluencer
{
    public enum Axis { X, Y, Z }
    [Tooltip("Direction in the controller's local space, normalized over actual panel centres.")]
    public Axis axis = Axis.X;
    public bool reverse;
    [Range(0f, 1f)] public float progression;
    [Range(0f, 1f)] public float fade = 0.1f;

    public override float GetWeightForTile(MudaacTile tile, MudaacTileController controller, float time)
    {
        int index = (int)axis;
        Bounds bounds = controller.LocalBounds;
        float position = controller.transform.InverseTransformPoint(tile.RestWorldCenter)[index];
        float relative = bounds.size[index] > 0.00001f
            ? Mathf.InverseLerp(bounds.min[index], bounds.max[index], position) : 0.5f;
        if (reverse)
            relative = 1f - relative;
        relative = Mathf.Clamp01(relative + GetRandomness(tile.RestWorldCenter, time));
        // Extend past both ends so progression=0 affects none and progression=1 affects all.
        float edge = Mathf.Lerp(-fade, 1f + fade, progression);
        float value = fade > 0.00001f ? Mathf.Clamp01((relative - edge) / fade + 0.5f)
            : (progression >= 1f || relative < edge ? 0f : 1f);
        return Evaluate(value);
    }
}
