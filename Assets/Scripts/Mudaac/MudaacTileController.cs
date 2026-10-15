using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

[ExecuteAlways, DisallowMultipleComponent]
public class MudaacTileController : MonoBehaviour
{
    [Header("Existing Tiles")]
    [Tooltip("Search below this root. Defaults to this GameObject; no objects are generated.")]
    public Transform tilesRoot;
    [Tooltip("Only renderer names starting with these prefixes are included. An empty list selects all renderers below the root.")]
    public List<string> tileNamePrefixes = new List<string>
    {
        "Concrete panel", "Undercroft panel", "Left low wall",
        "Left lower rounded wall", "Left upper cantilever"
    };
    [Tooltip("Optional discovery override. When nonempty, only these renderers appear in the group checkboxes.")]
    public List<Renderer> explicitTiles = new List<Renderer>();

    [SerializeField, HideInInspector] private List<MudaacTileGroup> tileGroups = new List<MudaacTileGroup>();
    [SerializeField, HideInInspector] private int tileGroupingVersion;

    [Header("Modifiers")]
    [Tooltip("Optional shared modifiers, applied after components on this GameObject, in component order.")]
    public GameObject globalModifiers;
    [Tooltip("Preview effects outside Play Mode. Saving the scene saves the original tile layout.")]
    public bool previewInEditMode;

    private readonly List<MudaacTile> tiles = new List<MudaacTile>();
    private readonly List<MudaacTileModifier> modifiers = new List<MudaacTileModifier>();
    private readonly List<MudaacTileModifier> sharedModifiers = new List<MudaacTileModifier>();
    private bool refreshRequested;
    private bool hasApplied;

    public IReadOnlyList<MudaacTile> Tiles => tiles;
    public IReadOnlyList<MudaacTileGroup> TileGroups => tileGroups;
    public Bounds LocalBounds { get; private set; }

    private void OnEnable()
    {
        RefreshTiles();
#if UNITY_EDITOR
        EditorApplication.update += EditorUpdate;
        EditorSceneManager.sceneSaving += BeforeSceneSave;
        AssemblyReloadEvents.beforeAssemblyReload += RestoreTiles;
        Undo.undoRedoPerformed += OnUndoRedo;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
        EditorSceneManager.sceneSaving -= BeforeSceneSave;
        AssemblyReloadEvents.beforeAssemblyReload -= RestoreTiles;
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
        RestoreTiles();
    }

    private void OnValidate() => refreshRequested = true;

    private void LateUpdate()
    {
        if (Application.IsPlaying(gameObject))
            EvaluateTiles(Time.unscaledTime);
    }

#if UNITY_EDITOR
    private void EditorUpdate()
    {
        if (this == null || !isActiveAndEnabled || Application.IsPlaying(gameObject) || EditorApplication.isPlayingOrWillChangePlaymode
            || EditorUtility.IsPersistent(this))
            return;
        if (refreshRequested)
            RefreshTiles();
        if (previewInEditMode)
        {
            EvaluateTiles((float)EditorApplication.timeSinceStartup);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
        else if (hasApplied)
            RestoreTiles();
    }

    private void BeforeSceneSave(Scene scene, string path)
    {
        if (scene == gameObject.scene && !Application.IsPlaying(gameObject))
            RestoreTiles();
    }

    private void OnUndoRedo()
    {
        RestoreTiles();
        refreshRequested = true;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
            RestoreTiles();
    }
#endif

    [ContextMenu("Refresh Existing Tiles")]
    public void RefreshTiles()
    {
        // Restore before recapturing so a refresh never bakes an effect into the building.
        RestoreTiles();
        tiles.Clear();
        MigrateDiscoveryDefaults();
        var seen = new HashSet<Renderer>();
        var candidates = new List<Renderer>();
        if (explicitTiles != null && explicitTiles.Count > 0)
        {
            foreach (Renderer renderer in explicitTiles)
                AddCandidate(renderer, candidates, seen);
        }
        else
        {
            Transform root = tilesRoot != null ? tilesRoot : transform;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (MatchesName(renderer.name))
                    AddCandidate(renderer, candidates, seen);
        }
        SynchronizeGroups(candidates);
        foreach (MudaacTileGroup group in tileGroups)
            if (group.included)
                foreach (MudaacTileGroup.TileSelection selection in group.tiles)
                    if (selection.included && selection.renderer != null)
                        tiles.Add(selection.CreateBinding());
        LocalBounds = new Bounds();
        for (int i = 0; i < tiles.Count; i++)
        {
            Vector3 point = transform.InverseTransformPoint(tiles[i].RestWorldCenter);
            if (i == 0)
                LocalBounds = new Bounds(point, Vector3.zero);
            else
            {
                Bounds bounds = LocalBounds;
                bounds.Encapsulate(point);
                LocalBounds = bounds;
            }
        }
        refreshRequested = false;
    }

    private bool MatchesName(string name)
    {
        if (tileNamePrefixes == null || tileNamePrefixes.Count == 0)
            return true;
        foreach (string prefix in tileNamePrefixes)
            if (!string.IsNullOrEmpty(prefix) && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private void AddCandidate(Renderer renderer, List<Renderer> candidates, HashSet<Renderer> seen)
    {
        // Moving the controller itself (or an ancestor) would move the entire building.
        if (renderer != null && renderer.transform != transform && !transform.IsChildOf(renderer.transform)
            && seen.Add(renderer))
            candidates.Add(renderer);
    }

    private void MigrateDiscoveryDefaults()
    {
        if (tileGroupingVersion != 0)
            return;
        // Upgrade the original default discovery without changing custom filters.
        if (tileNamePrefixes != null && tileNamePrefixes.Count == 2
            && tileNamePrefixes.Contains("Concrete panel") && tileNamePrefixes.Contains("Undercroft panel"))
        {
            tileNamePrefixes.Add("Left low wall");
            tileNamePrefixes.Add("Left lower rounded wall");
            tileNamePrefixes.Add("Left upper cantilever");
        }
        tileGroupingVersion = 1;
    }

    private void SynchronizeGroups(List<Renderer> candidates)
    {
        var groupsByKind = new Dictionary<MudaacTileGroup.Kind, MudaacTileGroup>();
        var selections = new Dictionary<Renderer, MudaacTileGroup.TileSelection>();
        foreach (MudaacTileGroup group in tileGroups)
        {
            if (group == null)
                continue;
            groupsByKind[group.kind] = group;
            foreach (MudaacTileGroup.TileSelection selection in group.tiles)
                if (selection != null && selection.renderer != null)
                    selections[selection.renderer] = selection;
            group.tiles.Clear();
        }
        tileGroups.Clear();
        foreach (Renderer renderer in candidates)
        {
            MudaacTileGroup.Kind kind = MudaacTileGroup.Classify(renderer.name);
            MudaacTileGroup group;
            if (!groupsByKind.TryGetValue(kind, out group))
            {
                group = new MudaacTileGroup(kind);
                groupsByKind.Add(kind, group);
            }
            if (group.tiles.Count == 0)
                tileGroups.Add(group);
            MudaacTileGroup.TileSelection selection;
            if (!selections.TryGetValue(renderer, out selection))
                selection = new MudaacTileGroup.TileSelection(renderer);
            selection.EnsureRestPose();
            group.tiles.Add(selection);
        }
        tileGroups.Sort((left, right) => left.kind.CompareTo(right.kind));
    }

    public void EvaluateTiles(float time)
    {
        if (!isActiveAndEnabled)
            return;
        if (refreshRequested || SelectionChanged())
            RefreshTiles();
        foreach (MudaacTile tile in tiles)
            tile.BeginFrame();
        GetComponents(modifiers);
        ApplyModifiers(modifiers, time);
        if (globalModifiers != null && globalModifiers != gameObject)
        {
            globalModifiers.GetComponents(sharedModifiers);
            ApplyModifiers(sharedModifiers, time);
        }
        foreach (MudaacTile tile in tiles)
            tile.Apply();
        hasApplied = true;
    }

    private bool SelectionChanged()
    {
        int index = 0;
        foreach (MudaacTileGroup group in tileGroups)
            if (group.included)
                foreach (MudaacTileGroup.TileSelection selection in group.tiles)
                    if (selection.included && selection.renderer != null)
                    {
                        if (index >= tiles.Count || tiles[index].Renderer != selection.renderer)
                            return true;
                        index++;
                    }
        return index != tiles.Count;
    }

    private void ApplyModifiers(List<MudaacTileModifier> source, float time)
    {
        foreach (MudaacTileModifier modifier in source)
            if (modifier != null && modifier.isActiveAndEnabled)
                modifier.UpdateTiles(this, tiles, time);
    }

    [ContextMenu("Restore Original Tiles")]
    public void RestoreTiles()
    {
        GetComponents(modifiers);
        foreach (MudaacTileModifier modifier in modifiers)
            if (modifier != null)
                modifier.OnTilesRestored(this);
        if (globalModifiers != null && globalModifiers != gameObject)
        {
            globalModifiers.GetComponents(sharedModifiers);
            foreach (MudaacTileModifier modifier in sharedModifiers)
                if (modifier != null)
                    modifier.OnTilesRestored(this);
        }
        foreach (MudaacTile tile in tiles)
            tile.Restore();
        // Bindings and hasApplied are transient. The serialized poses also restore
        // excluded tiles and survive a reload or Undo that reinstates preview data.
        foreach (MudaacTileGroup group in tileGroups)
            foreach (MudaacTileGroup.TileSelection selection in group.tiles)
                selection.RestoreRestPose();
        hasApplied = false;
    }

    [ContextMenu("Capture Current Tile Layout")]
    public void CaptureCurrentTileLayout()
    {
        previewInEditMode = false;
        if (hasApplied)
            RestoreTiles();
        foreach (MudaacTileGroup group in tileGroups)
            foreach (MudaacTileGroup.TileSelection selection in group.tiles)
                selection.CaptureRestPose();
        RefreshTiles();
    }
}
