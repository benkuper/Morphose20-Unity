using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MudaacTileSelectorModifier)), CanEditMultipleObjects]
public sealed class MudaacTileSelectorModifierEditor : Editor
{
    private static MudaacTileSelectorModifierEditor activePicker;
    private readonly List<Renderer> candidates = new List<Renderer>();
    private readonly List<MudaacTileController> controllers = new List<MudaacTileController>();
    private GameObject[] pickObjects = Array.Empty<GameObject>();
    private Vector2 scroll;
    private string search = "";
    private bool showTiles = true;
    private bool showReferences;
    private int capturedControl;
    private Renderer hovered;

    private MudaacTileSelectorModifier Modifier => (MudaacTileSelectorModifier)target;

    private void OnEnable()
    {
        Undo.undoRedoPerformed += Refresh;
        EditorApplication.hierarchyChanged += Refresh;
    }

    private void OnDisable()
    {
        StopPicking();
        Undo.undoRedoPerformed -= Refresh;
        EditorApplication.hierarchyChanged -= Refresh;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        if (serializedObject.isEditingMultipleObjects)
        {
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Select one Tile Selector to use the tile picker.", MessageType.Info);
            return;
        }
        RefreshCandidates();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mode"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("selectionThreshold"));
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(candidates.Count == 0 || EditorUtility.IsPersistent(target)))
            if (GUILayout.Button(activePicker == this ? "Done Selecting Tiles" : "Select Tiles", GUILayout.Height(28f)))
            {
                if (activePicker == this) StopPicking(); else StartPicking();
            }
        if (activePicker == this)
            EditorGUILayout.HelpBox("Click tiles in Scene view to toggle them. Shift adds, Ctrl/Cmd removes. "
                + "Esc finishes. This selector's visibility effect is paused while picking; the Inspector stays here.", MessageType.Info);
        else if (candidates.Count == 0)
            EditorGUILayout.HelpBox("No included tiles found. Add this modifier to a tile controller, or assign its GameObject "
                + "as the controller's Global Modifiers, and include the desired tile groups.", MessageType.Info);

        int count = Modifier.selectedTiles != null ? Modifier.selectedTiles.Count : 0;
        EditorGUILayout.LabelField("Manual selection", count + " tiles");
        if (count == 0 && Modifier.selectAllWhenEmpty)
            EditorGUILayout.HelpBox("No manual filter: all included tiles qualify. Pick a tile to start a selection, "
                + "or use None for an empty selection.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("All")) SetSelection(candidates, false);
            if (GUILayout.Button("None")) SetSelection(Array.Empty<Renderer>(), false);
            if (GUILayout.Button("Influencers Only")) SetSelection(Array.Empty<Renderer>(), true);
        }
        showTiles = EditorGUILayout.Foldout(showTiles, "Browse Tiles (" + candidates.Count + ")", true);
        if (showTiles)
        {
            search = EditorGUILayout.TextField("Search", search);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(220f));
            foreach (Renderer renderer in candidates)
            {
                if (!string.IsNullOrEmpty(search) && renderer.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                bool selected = IsSelected(renderer);
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool next = EditorGUILayout.ToggleLeft(renderer.name, selected);
                    if (next != selected) SetTileSelected(renderer, next);
                    if (GUILayout.Button("Frame", GUILayout.Width(48f)) && SceneView.lastActiveSceneView != null)
                        SceneView.lastActiveSceneView.Frame(renderer.bounds, false);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        serializedObject.Update();
        EditorGUILayout.Space();
        DrawPropertiesExcluding(serializedObject, "m_Script", "mode", "selectionThreshold", "selectedTiles", "selectAllWhenEmpty");
        showReferences = EditorGUILayout.Foldout(showReferences, "Manual References", true);
        if (showReferences)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("selectedTiles"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("selectAllWhenEmpty"));
        }
        if (serializedObject.ApplyModifiedProperties()) Refresh();
    }

    private void RefreshCandidates()
    {
        controllers.Clear();
        candidates.Clear();
        var local = Modifier.GetComponent<MudaacTileController>();
        if (local != null) controllers.Add(local);
        foreach (var controller in UnityEngine.Object.FindObjectsByType<MudaacTileController>(
            FindObjectsInactive.Include))
            if (controller != local && controller.globalModifiers == Modifier.gameObject)
                controllers.Add(controller);
        var seen = new HashSet<Renderer>();
        foreach (var controller in controllers)
            foreach (var group in controller.TileGroups)
                if (group.included)
                    foreach (var tile in group.tiles)
                        if (tile.included && tile.renderer != null && seen.Add(tile.renderer))
                            candidates.Add(tile.renderer);
        pickObjects = new GameObject[candidates.Count];
        for (int i = 0; i < candidates.Count; i++) pickObjects[i] = candidates[i].gameObject;
    }

    private bool IsSelected(Renderer renderer) => Modifier.selectedTiles != null && Modifier.selectedTiles.Contains(renderer);

    private void SetTileSelected(Renderer renderer, bool selected)
    {
        if (renderer == null || !candidates.Contains(renderer)) return;
        if (IsSelected(renderer) == selected && !Modifier.selectAllWhenEmpty) return;
        Undo.RecordObject(Modifier, "Select Mudaac Tile");
        if (Modifier.selectedTiles == null) Modifier.selectedTiles = new List<Renderer>();
        Modifier.selectAllWhenEmpty = false;
        if (selected)
        {
            if (!Modifier.selectedTiles.Contains(renderer)) Modifier.selectedTiles.Add(renderer);
        }
        else
            Modifier.selectedTiles.RemoveAll(item => item == renderer);
        SelectionChanged();
    }

    private void SetSelection(IEnumerable<Renderer> renderers, bool allWhenEmpty)
    {
        Undo.RecordObject(Modifier, "Select Mudaac Tiles");
        Modifier.selectedTiles = new List<Renderer>(renderers);
        Modifier.selectAllWhenEmpty = allWhenEmpty;
        SelectionChanged();
    }

    private void SelectionChanged()
    {
        EditorUtility.SetDirty(Modifier);
        PrefabUtility.RecordPrefabInstancePropertyModifications(Modifier);
        serializedObject.Update();
        RepaintViews();
    }

    private void Refresh()
    {
        if (target == null) return;
        RefreshCandidates();
        RepaintViews();
    }

    private void RepaintViews()
    {
        Repaint();
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }

    private void StartPicking()
    {
        if (activePicker != null) activePicker.StopPicking();
        RefreshCandidates();
        if (candidates.Count == 0 || EditorUtility.IsPersistent(target)) return;
        activePicker = this;
        Modifier.IsEditingSelection = true;
        Refresh();
    }

    private void StopPicking()
    {
        if (activePicker != this) return;
        if (capturedControl != 0 && GUIUtility.hotControl == capturedControl) GUIUtility.hotControl = 0;
        capturedControl = 0;
        activePicker = null;
        hovered = null;
        if (target != null) Modifier.IsEditingSelection = false;
        Refresh();
    }

    private void OnSceneGUI()
    {
        if (serializedObject.isEditingMultipleObjects) return;
        Event current = Event.current;
        bool picking = activePicker == this;
        if (picking && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
        {
            StopPicking();
            current.Use();
            return;
        }
        int control = GUIUtility.GetControlID(FocusType.Passive);
        if (picking && !current.alt)
        {
            if (current.type == EventType.Layout) HandleUtility.AddDefaultControl(control);
            if (current.type == EventType.MouseMove || current.type == EventType.MouseDown)
            {
                hovered = PickTile(current.mousePosition);
                SceneView.RepaintAll();
            }
            if (current.type == EventType.MouseDown && current.button == 0 && GUIUtility.hotControl == 0
                && (HandleUtility.nearestControl == control || hovered != null))
            {
                GUIUtility.hotControl = capturedControl = control;
                if (hovered != null)
                    SetTileSelected(hovered, current.control || current.command ? false : current.shift || !IsSelected(hovered));
                current.Use();
            }
        }
        if (current.type == EventType.MouseUp && current.button == 0 && capturedControl != 0
            && GUIUtility.hotControl == capturedControl)
        {
            GUIUtility.hotControl = capturedControl = 0;
            current.Use();
        }
        if (current.type != EventType.Repaint) return;
        var previousDepth = Handles.zTest;
        try
        {
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            foreach (var renderer in candidates)
            {
                if (renderer == null) continue;
                bool selected = IsSelected(renderer);
                if (!picking && !selected) continue;
                Color color = renderer == hovered ? Color.yellow : selected
                    ? new Color(0.1f, 0.9f, 1f, 1f) : new Color(1f, 1f, 1f, 0.2f);
                using (new Handles.DrawingScope(color, renderer.localToWorldMatrix))
                    Handles.DrawWireCube(renderer.localBounds.center, renderer.localBounds.size);
            }
            if (picking && hovered != null)
                Handles.Label(hovered.bounds.center, hovered.name + (IsSelected(hovered) ? " (selected)" : ""));
        }
        finally { Handles.zTest = previousDepth; }
    }

    private Renderer PickTile(Vector2 mousePosition)
    {
        var picked = HandleUtility.PickGameObject(mousePosition, false, null, pickObjects);
        if (picked != null)
            foreach (var renderer in candidates)
                if (renderer != null && renderer.gameObject == picked) return renderer;
        // Hidden renderers cannot be picked by Unity. Their bounds stay selectable.
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        Renderer nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (var renderer in candidates)
            if (renderer != null && renderer.bounds.IntersectRay(ray, out float distance) && distance < nearestDistance)
            {
                nearest = renderer;
                nearestDistance = distance;
            }
        return nearest;
    }
}
