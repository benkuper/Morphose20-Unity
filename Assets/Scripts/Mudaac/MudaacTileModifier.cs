using System.Collections.Generic;
using UnityEngine;

public abstract class MudaacTileModifier : MonoBehaviour
{
    public enum WeightMode { Average, Multiply, Max }
    public enum CoordinateSpace { Tile, Controller, World }

    public List<MudaacTileInfluencer> influencers = new List<MudaacTileInfluencer>();
    [Range(0f, 1f)] public float modifierWeight = 1f;
    [Range(0f, 1f)] public float influencerWeight = 1f;
    public WeightMode weightMode = WeightMode.Max;

    public float GetWeight(MudaacTile tile, MudaacTileController controller, float time)
    {
        int count = 0;
        float total = weightMode == WeightMode.Multiply ? 1f : 0f;
        if (influencers != null)
            foreach (MudaacTileInfluencer influencer in influencers)
            {
                if (influencer == null || !influencer.isActiveAndEnabled)
                    continue;
                float weight = Mathf.Clamp01(influencer.GetWeightForTile(tile, controller, time));
                count++;
                switch (weightMode)
                {
                    case WeightMode.Average: total += weight; break;
                    case WeightMode.Multiply: total *= weight; break;
                    case WeightMode.Max: total = Mathf.Max(total, weight); break;
                }
            }
        if (count == 0)
            total = 1f;
        else if (weightMode == WeightMode.Average)
            total /= count;
        return Mathf.Clamp01(modifierWeight) * Mathf.Lerp(1f, total, Mathf.Clamp01(influencerWeight));
    }

    public virtual void UpdateTiles(MudaacTileController controller, IReadOnlyList<MudaacTile> tiles, float time)
    {
        if (modifierWeight <= 0f)
            return;
        for (int i = 0; i < tiles.Count; i++)
        {
            MudaacTile tile = tiles[i];
            if (tile.IsValid)
                UpdateTile(tile, controller, GetWeight(tile, controller, time), time);
        }
    }

    // Stateful visuals can release transient output when the controller stops previewing.
    public virtual void OnTilesRestored(MudaacTileController controller) { }


    protected static Vector3 ToWorldDirection(MudaacTile tile, MudaacTileController controller,
        Vector3 vector, CoordinateSpace space)
    {
        switch (space)
        {
            case CoordinateSpace.Tile: return tile.RestWorldRotation * vector;
            case CoordinateSpace.Controller: return controller.transform.TransformDirection(vector);
            default: return vector;
        }
    }

    protected abstract void UpdateTile(MudaacTile tile, MudaacTileController controller, float weight, float time);
}
