// Execute through Unity MCP execute_code in Edit Mode. Uses an isolated additive
// scene and restores the production scene without saving it.
var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
var root = new GameObject("Tile selector checks");
string savePath = "Assets/__MudaacTileSelectorChecks.unity";
bool savedScene = false;
var report = new System.Text.StringBuilder();
int passed = 0;
System.Action<bool, string> check = (condition, name) => {
    if (!condition) throw new System.Exception("FAIL: " + name);
    passed++; report.AppendLine("PASS: " + name);
};
try
{
    var panels = new Renderer[3];
    for (int i = 0; i < panels.Length; i++)
    {
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Concrete panel selector " + i;
        panel.transform.SetParent(root.transform, false);
        panel.transform.localPosition = Vector3.right * i * 2f;
        panels[i] = panel.GetComponent<Renderer>();
    }
    panels[2].enabled = false;
    var ignored = GameObject.CreatePrimitive(PrimitiveType.Cube);
    ignored.name = "Window frame"; ignored.transform.SetParent(root.transform, false);
    var undercroft = GameObject.CreatePrimitive(PrimitiveType.Cube);
    undercroft.name = "Undercroft panel selector"; undercroft.transform.SetParent(root.transform, false);
    var controller = root.AddComponent<MudaacTileController>();
    var selector = root.AddComponent<MudaacTileSelectorModifier>();
    System.Func<MudaacTileGroup> tileGroup = () => {
        foreach (var group in controller.TileGroups)
            if (group.kind == MudaacTileGroup.Kind.Other) return group;
        throw new System.Exception("Missing test tile group");
    };
    System.Func<bool> restored = () => panels[0].enabled && panels[1].enabled && !panels[2].enabled;
    check(controller.Tiles.Count == 3 && restored(), "Discovery captures enabled and disabled renderers");
    selector.selectedTiles.Add(panels[0]);
    controller.EvaluateTiles(0f);
    check(panels[0].enabled && !panels[1].enabled && !panels[2].enabled,
        "Show Selected Only hides every unselected included tile");
    check(ignored.GetComponent<Renderer>().enabled && undercroft.GetComponent<Renderer>().enabled,
        "Non-tiles and excluded groups are unaffected");
    check(panels[1].gameObject.activeSelf && panels[1].GetComponent<Collider>().enabled,
        "Hiding leaves GameObjects and colliders active");
    selector.mode = MudaacTileSelectorModifier.VisibilityMode.HideSelected;
    controller.EvaluateTiles(0f);
    check(!panels[0].enabled && panels[1].enabled && !panels[2].enabled,
        "Hide Selected preserves the authored state of unselected tiles");
    selector.selectedTiles.Clear(); selector.selectedTiles.Add(panels[1]);
    controller.EvaluateTiles(0f);
    check(panels[0].enabled && !panels[1].enabled, "Changing selection restores previously hidden tiles next frame");
    selector.modifierWeight = 0f; controller.EvaluateTiles(0f);
    check(restored(), "Zero modifier weight restores authored visibility");
    selector.modifierWeight = 1f; controller.EvaluateTiles(0f);
    selector.enabled = false; controller.EvaluateTiles(0f);
    check(restored(), "Disabling a selector restores visibility on the next evaluation");
    selector.enabled = true; controller.EvaluateTiles(0f);
    UnityEngine.Object.DestroyImmediate(selector); controller.EvaluateTiles(0f);
    check(restored(), "Removing a selector restores visibility on the next evaluation");
    selector = root.AddComponent<MudaacTileSelectorModifier>();
    selector.selectedTiles.Add(panels[2]); controller.EvaluateTiles(0f);
    check(!panels[0].enabled && !panels[1].enabled && panels[2].enabled,
        "Show Selected Only can reveal an authored hidden renderer temporarily");
    controller.RestoreTiles(); check(restored(), "Restoration preserves authored hidden renderers");
    selector.selectedTiles.Clear(); selector.selectedTiles.Add(panels[0]); selector.selectedTiles.Add(panels[1]);
    var second = root.AddComponent<MudaacTileSelectorModifier>();
    second.mode = MudaacTileSelectorModifier.VisibilityMode.HideSelected;
    second.selectedTiles.Add(panels[0]); controller.EvaluateTiles(0f);
    check(!panels[0].enabled && panels[1].enabled && !panels[2].enabled,
        "Multiple selectors compose in component order");
    second.enabled = false; controller.EvaluateTiles(0f);
    check(panels[0].enabled && panels[1].enabled, "Disabling one selector retains the other selector's output");
    selector.enabled = false;
    var shared = new GameObject("Shared selector"); shared.transform.SetParent(root.transform, false);
    var global = shared.AddComponent<MudaacTileSelectorModifier>();
    global.mode = MudaacTileSelectorModifier.VisibilityMode.HideSelected;
    global.selectedTiles.Add(panels[1]); controller.globalModifiers = shared; controller.EvaluateTiles(0f);
    check(panels[0].enabled && !panels[1].enabled, "Global modifiers support tile selection");
    global.enabled = false; controller.EvaluateTiles(0f);
    check(restored(), "Disabling a global selector restores authored visibility");
    selector.enabled = true; selector.selectedTiles.Clear();
    var progressionObject = new GameObject("Selection progression"); progressionObject.transform.SetParent(root.transform, false);
    var progression = progressionObject.AddComponent<MudaacTileProgressionInfluencer>();
    selector.influencers.Add(progression);
    progression.progression = 0f; controller.EvaluateTiles(0f);
    check(!panels[0].enabled && !panels[1].enabled && !panels[2].enabled,
        "An empty manual list lets influencers select tiles; progression zero selects none");
    progression.progression = 1f; controller.EvaluateTiles(0f);
    check(panels[0].enabled && panels[1].enabled && panels[2].enabled, "Progression one selects all included tiles");
    progression.progression = 0.5f; controller.EvaluateTiles(0f);
    check(panels[0].enabled && !panels[2].enabled, "Progression selects by authored tile position");
    selector.selectedTiles.Add(panels[2]); controller.EvaluateTiles(0f);
    check(!panels[0].enabled && !panels[1].enabled && !panels[2].enabled,
        "The manual list and influencers intersect");
    selector.selectedTiles.Clear(); progression.progression = 1f; progression.weight = 0.4f;
    controller.EvaluateTiles(0f);
    check(!panels[0].enabled && !panels[1].enabled && !panels[2].enabled, "Weights below the selection threshold are excluded");
    progression.weight = 0.5f; controller.EvaluateTiles(0f);
    check(panels[0].enabled && panels[2].enabled, "Weights at the selection threshold are included");
    progression.weight = 0f; selector.selectionThreshold = 0f; controller.EvaluateTiles(0f);
    check(!panels[0].enabled && !panels[2].enabled, "Zero influence remains unselected even at threshold zero");
    selector.selectionThreshold = 0.5f; progression.enabled = false; controller.EvaluateTiles(0f);
    check(panels[0].enabled && panels[2].enabled, "Disabled influencers give full coverage like other modifiers");
    selector.influencers.Clear(); selector.selectedTiles.Add(panels[0]);
    controller.EvaluateTiles(0f); controller.RefreshTiles();
    check(restored(), "Refresh restores visibility before rebuilding bindings");
    controller.EvaluateTiles(0f); tileGroup().tiles[1].included = false; controller.EvaluateTiles(0f);
    check(panels[1].enabled, "Excluding an individual hidden tile restores its original visibility");
    tileGroup().tiles[1].included = true; controller.EvaluateTiles(0f);
    tileGroup().included = false; controller.EvaluateTiles(0f);
    check(restored() && controller.Tiles.Count == 0, "Excluding a group restores its renderer visibility");
    tileGroup().included = true; controller.EvaluateTiles(0f);
    controller.enabled = false; check(restored(), "Disabling the controller immediately restores visibility");
    controller.enabled = true; controller.EvaluateTiles(0f);
    root.SetActive(false); check(restored(), "Deactivating the controller GameObject restores visibility"); root.SetActive(true);
    var editorUpdate = typeof(MudaacTileController).GetMethod("EditorUpdate",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    controller.previewInEditMode = true; editorUpdate.Invoke(controller, null);
    controller.previewInEditMode = false; editorUpdate.Invoke(controller, null);
    check(restored(), "Stopping edit-mode preview restores visibility");
    controller.EvaluateTiles(0f);
    Undo.IncrementCurrentGroup(); Undo.RecordObject(panels[1], "Tile visibility snapshot check");
    panels[1].enabled = true; Undo.FlushUndoRecordObjects(); controller.RestoreTiles(); Undo.PerformUndo();
    check(restored(), "Undo cannot reinstate hidden preview visibility"); Undo.ClearUndo(panels[1]);
    controller.EvaluateTiles(0f);
    string snapshot = JsonUtility.ToJson(controller);
    var tilesField = typeof(MudaacTileController).GetField("tiles",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    ((System.Collections.Generic.List<MudaacTile>)tilesField.GetValue(controller)).Clear();
    JsonUtility.FromJsonOverwrite(snapshot, controller); tileGroup().included = false;
    controller.enabled = false;
    check(restored(), "Serialized snapshots restore excluded tiles after transient bindings are lost");
    controller.enabled = true;
    panels[1].enabled = false; controller.CaptureCurrentTileLayout();
    tileGroup().included = true; selector.selectedTiles.Add(panels[1]); controller.EvaluateTiles(0f);
    check(panels[1].enabled, "A selector can show an intentionally hidden tile after layout capture");
    controller.RestoreTiles(); check(!panels[1].enabled, "Layout capture accepts intentional visibility edits on excluded tiles");
    panels[1].enabled = true; controller.CaptureCurrentTileLayout();
    check(restored(), "The original visibility can be recaptured explicitly");
    check(!System.IO.File.Exists(savePath), "Temporary scene-save path is unused");
    selector.selectedTiles.Clear(); selector.selectedTiles.Add(panels[0]);
    controller.previewInEditMode = true; controller.EvaluateTiles(0f);
    savedScene = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, savePath);
    check(savedScene && restored(), "Scene save restores renderer visibility before serialization");
    controller.EvaluateTiles(1f); UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
    scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(savePath,
        UnityEditor.SceneManagement.OpenSceneMode.Additive);
    foreach (var item in scene.GetRootGameObjects())
        if (item.name == "Tile selector checks") { root = item; break; }
    controller = root.GetComponent<MudaacTileController>(); controller.enabled = false;
    for (int i = 0; i < panels.Length; i++) panels[i] = root.transform.Find("Concrete panel selector " + i).GetComponent<Renderer>();
    check(restored(), "Scene reload retains authored visible and hidden states");
    report.AppendLine("All " + passed + " tile selector checks passed.");
    return report.ToString();
}
finally
{
    if (root != null) UnityEngine.Object.DestroyImmediate(root);
    if (scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(activeScene);
    if (savedScene) UnityEditor.AssetDatabase.DeleteAsset(savePath);
}
