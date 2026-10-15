using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Mudaac/Modifiers/Tile Selector")]
public class MudaacTileSelectorModifier : MudaacTileModifier
{
    public enum VisibilityMode { ShowSelectedOnly, HideSelected }

    [Header("Tile Selection")]
    public VisibilityMode mode = VisibilityMode.ShowSelectedOnly;
    [Tooltip("Manual tile filter. Select All When Empty determines whether an empty list considers all included tiles or none.")]
    public List<Renderer> selectedTiles = new List<Renderer>();
    [Tooltip("Let influencers select all included tiles when the manual list is empty. The tile picker disables this for an explicit empty selection.")]
    public bool selectAllWhenEmpty = true;
    [Tooltip("Tiles are selected when their combined modifier/influencer weight is positive and reaches this threshold.")]
    [Range(0f, 1f)] public float selectionThreshold = 0.5f;

#if UNITY_EDITOR
    // Transient authoring mode; never saved to the scene or included in players.
    public bool IsEditingSelection { get; set; }
#endif

    protected override void UpdateTile(MudaacTile tile, MudaacTileController controller, float weight, float time)
    {
#if UNITY_EDITOR
        if (IsEditingSelection)
            return;
#endif
        bool selected = weight > 0f && weight >= Mathf.Clamp01(selectionThreshold)
            && (selectedTiles == null || selectedTiles.Count == 0
                ? selectAllWhenEmpty : selectedTiles.Contains(tile.Renderer));
        if (mode == VisibilityMode.ShowSelectedOnly)
            tile.Visible = selected;
        else if (selected)
            tile.Visible = false;
    }
}
