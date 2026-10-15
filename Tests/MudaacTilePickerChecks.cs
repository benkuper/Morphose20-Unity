// Execute through Unity MCP execute_code in Edit Mode. Exercises the custom
// Inspector's editing commands in a preview scene without saving any assets.
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var root = new GameObject("Tile picker checks");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
var report = new System.Text.StringBuilder();
int passed = 0;
System.Action<bool, string> check = (condition, name) => {
    if (!condition) throw new System.Exception("FAIL: " + name);
    passed++; report.AppendLine("PASS: " + name);
};
UnityEditor.Editor editor = null;
UnityEditor.Editor otherEditor = null;
var selection = UnityEditor.Selection.objects;
try
{
    var panels = new Renderer[3];
    for (int i = 0; i < panels.Length; i++)
    {
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(panel, scene);
        panel.name = "Concrete panel picker " + i;
        panel.transform.SetParent(root.transform, false);
        panels[i] = panel.GetComponent<Renderer>();
    }
    var controller = root.AddComponent<MudaacTileController>();
    controller.TileGroups[0].tiles[2].included = false; controller.RefreshTiles();
    var modifier = root.AddComponent<MudaacTileSelectorModifier>();
    editor = UnityEditor.Editor.CreateEditor(modifier);
    var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
    var type = editor.GetType();
    System.Action refresh = () => type.GetMethod("RefreshCandidates", flags).Invoke(editor, null);
    System.Action start = () => type.GetMethod("StartPicking", flags).Invoke(editor, null);
    System.Action stop = () => type.GetMethod("StopPicking", flags).Invoke(editor, null);
    System.Action<Renderer, bool> setTile = (renderer, value) => type.GetMethod("SetTileSelected", flags)
        .Invoke(editor, new object[] { renderer, value });
    System.Action<Renderer[], bool> setSelection = (renderers, all) => type.GetMethod("SetSelection", flags)
        .Invoke(editor, new object[] { renderers, all });
    refresh();
    var candidates = (System.Collections.Generic.List<Renderer>)type.GetField("candidates", flags).GetValue(editor);
    check(candidates.Count == 2 && !candidates.Contains(panels[2]), "Browser offers only controller-included tiles");
    controller.EvaluateTiles(0f);
    check(panels[0].enabled && panels[1].enabled, "Existing empty lists retain automatic full selection");
    setTile(panels[0], true); controller.EvaluateTiles(0f);
    check(modifier.selectedTiles.Count == 1 && panels[0].enabled && !panels[1].enabled,
        "The first pick starts a manual selection");
    start(); controller.EvaluateTiles(0f);
    check(modifier.IsEditingSelection && panels[0].enabled && panels[1].enabled,
        "Picking pauses the selector effect so unselected tiles can be seen");
    var currentSelection = UnityEditor.Selection.objects;
    bool unchanged = selection.Length == currentSelection.Length;
    for (int i = 0; unchanged && i < selection.Length; i++) unchanged = selection[i] == currentSelection[i];
    check(unchanged, "Picker commands leave Unity's Inspector selection unchanged");
    setTile(panels[1], true); setTile(panels[1], true);
    check(modifier.selectedTiles.Count == 2, "Adding the same tile twice does not create duplicates");
    setTile(panels[2], true);
    check(!modifier.selectedTiles.Contains(panels[2]), "Excluded tiles cannot be added by the picker");
    stop(); controller.EvaluateTiles(0f);
    check(!modifier.IsEditingSelection && panels[0].enabled && panels[1].enabled,
        "Finishing picking resumes the selected visibility effect");
    Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup();
    setTile(panels[1], false); Undo.FlushUndoRecordObjects();
    check(!modifier.selectedTiles.Contains(panels[1]), "Removing a picked tile updates membership");
    Undo.PerformUndo();
    check(modifier.selectedTiles.Contains(panels[1]), "Undo restores picked tile membership");
    Undo.PerformRedo();
    check(!modifier.selectedTiles.Contains(panels[1]), "Redo reapplies tile removal");
    Undo.ClearUndo(modifier);
    setSelection(new Renderer[0], false); controller.EvaluateTiles(0f);
    check(modifier.selectedTiles.Count == 0 && !panels[0].enabled && !panels[1].enabled,
        "None hides all included tiles in Show Selected Only mode");
    modifier.mode = MudaacTileSelectorModifier.VisibilityMode.HideSelected; controller.EvaluateTiles(0f);
    check(panels[0].enabled && panels[1].enabled, "An explicit empty selection hides nothing in Hide Selected mode");
    setSelection(new Renderer[0], true);
    modifier.mode = MudaacTileSelectorModifier.VisibilityMode.ShowSelectedOnly; controller.EvaluateTiles(0f);
    check(panels[0].enabled && panels[1].enabled && modifier.selectAllWhenEmpty,
        "Influencers Only restores automatic selection");
    setSelection(candidates.ToArray(), false);
    check(modifier.selectedTiles.Count == 2 && !modifier.selectAllWhenEmpty, "All selects exactly the eligible tiles");
    var other = root.AddComponent<MudaacTileSelectorModifier>();
    otherEditor = UnityEditor.Editor.CreateEditor(other);
    start(); otherEditor.GetType().GetMethod("StartPicking", flags).Invoke(otherEditor, null);
    check(!modifier.IsEditingSelection && other.IsEditingSelection, "Only one selector picks at a time");
    UnityEngine.Object.DestroyImmediate(otherEditor); otherEditor = null;
    check(!other.IsEditingSelection, "Closing the picking Inspector releases its preview pause");
    start();
    check(!JsonUtility.ToJson(modifier).Contains("IsEditingSelection"), "Picking state is never serialized");
    UnityEngine.Object.DestroyImmediate(editor); editor = null;
    check(!modifier.IsEditingSelection, "Destroying the editor releases the picker and callbacks");
    Undo.ClearUndo(modifier);
    report.AppendLine("All " + passed + " tile picker checks passed.");
    return report.ToString();
}
finally
{
    if (otherEditor != null) UnityEngine.Object.DestroyImmediate(otherEditor);
    if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
    UnityEngine.Object.DestroyImmediate(root);
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
