// Execute this method body with Unity MCP execute_code (CodeDom/C# 6 compatible).
// Creates and reopens an isolated test scene. After running, remove the generated
// Assets/__MudaacObjectChecks_8e1c93.unity and its .meta, then refresh assets.
var report = new System.Text.StringBuilder();
int passed = 0;
System.Action<bool, string> check = (condition, name) => {
    if (!condition) throw new System.Exception("FAIL: " + name);
    passed++; report.AppendLine("PASS: " + name);
};
var liveRoot = GameObject.Find("Mudaac Model");
var live = liveRoot.GetComponent<MudaacObjectManager>();
check(live.Groups.Count == 21, "Live model exposes 21 semantic visibility groups");
var liveStates = new System.Collections.Generic.Dictionary<GameObject, bool>();
foreach (Transform t in liveRoot.GetComponentsInChildren<Transform>(true)) liveStates.Add(t.gameObject, t.gameObject.activeSelf);
live.RefreshGroups();
foreach (var state in liveStates)
    if (state.Key.activeSelf != state.Value) throw new System.Exception("Discovery changed " + state.Key.name);
check(true, "Discovery preserves all existing active states, including hidden garden objects");
var owners = new System.Collections.Generic.Dictionary<GameObject, int>();
foreach (var group in live.Groups)
    foreach (GameObject item in group.objects)
    {
        int count; owners.TryGetValue(item, out count); owners[item] = count + 1;
        if (item == liveRoot || live.transform.IsChildOf(item.transform)) throw new System.Exception("Manager ancestor is a target");
    }
int covered = 0;
foreach (Renderer renderer in liveRoot.GetComponentsInChildren<Renderer>(true))
{
    int ownerCount = 0;
    for (Transform t = renderer.transform; t != null && t != liveRoot.transform; t = t.parent)
    {
        int count; if (owners.TryGetValue(t.gameObject, out count)) ownerCount += count;
    }
    if (ownerCount != 1) throw new System.Exception("Incorrect visibility ownership: " + renderer.name + " owners=" + ownerCount);
    covered++;
}
check(covered == 903, "All 903 model renderers belong to exactly one visibility group");

var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
string sceneSavePath = "Assets/__MudaacObjectChecks_8e1c93.unity";
check(!System.IO.File.Exists(sceneSavePath), "Temporary scene asset path is unused");
bool tempSceneWritten = false;
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
var root = new GameObject("Object manager checks");
var camera = new GameObject("External camera", typeof(Camera));
System.Func<Transform, string, GameObject> makeMesh = (parent, name) => {
    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name; go.transform.SetParent(parent, false); return go;
};
try
{
    var facade = new GameObject("MUDAAC | facade root | metres"); facade.transform.SetParent(root.transform, false);
    var tile = makeMesh(facade.transform, "Concrete panel 0097");
    var rounded = makeMesh(facade.transform, "Left lower rounded wall | test");
    var cantilever = makeMesh(facade.transform, "Left upper cantilever | test");
    var undercroft = makeMesh(facade.transform, "Undercroft panel 0087");
    var glass = makeMesh(facade.transform, "Glass front | test");
    var frame = makeMesh(facade.transform, "Vault arch rib | test");
    var historic = makeMesh(facade.transform, "Historic connection | wall");
    var unknown = makeMesh(facade.transform, "Unexpected mesh");
    var sculpture = new GameObject("SCULPTURE | hare and fitted pedestal"); sculpture.transform.SetParent(root.transform, false);
    var hare = makeMesh(sculpture.transform, "Hare | body");
    makeMesh(sculpture.transform, "Hare | pedestal");
    var plant = new GameObject("C02a | observed garden plant"); plant.transform.SetParent(root.transform, false);
    makeMesh(plant.transform, "C02a bole"); makeMesh(plant.transform, "C02a foliage"); plant.SetActive(false);
    var ground = makeMesh(root.transform, "Garden ground | fitted"); ground.SetActive(false);
    var controller = root.AddComponent<MudaacTileController>();
    var manager = root.AddComponent<MudaacObjectManager>();
    System.Func<string, MudaacObjectManager.ObjectGroup> find = id => {
        foreach (var g in manager.Groups) if (g.id == id) return g;
        throw new System.Exception("Missing group " + id);
    };
    check(find("glass-entrance").objects.Count == 2, "Glass entrance includes its structural frame");
    check(find("historic-building").objects.Contains(historic), "Neighbouring historic building is separate");
    check(find("sculpture").objects.Count == 1 && find("sculpture").objects[0] == sculpture,
        "Sculpture reuses its existing container");
    check(find("vegetation").objects.Count == 1 && !plant.activeSelf, "Inactive vegetation is discovered without enabling it");
    check(find("other").objects.Contains(unknown), "Unknown model objects remain controllable");
    check(find("tiles:Undercroft").objects.Contains(undercroft), "Undercroft has its own visibility group");

    manager.SetGroupVisible("glass-entrance", false);
    check(!glass.activeSelf && !frame.activeSelf && historic.activeSelf, "Group toggle hides entrance and frame together");
    manager.SetGroupVisible("glass-entrance", true);
    check(glass.activeSelf && frame.activeSelf, "Group toggle shows entrance and frame together");
    manager.SetGroupVisible("glass-entrance", false);
    check(manager.GetVisibleCount(find("glass-entrance")) == 0, "Visibility counts reflect actual object activation");
    int selectedTiles = controller.Tiles.Count;
    var baseline = new System.Collections.Generic.Dictionary<GameObject, bool>();
    foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) baseline[t.gameObject] = t.gameObject.activeSelf;

    manager.IsolateGroup("sculpture");
    check(sculpture.activeInHierarchy && hare.activeInHierarchy && !tile.activeInHierarchy && !historic.activeInHierarchy,
        "Sculpture isolation hides other semantic groups");
    check(root.activeSelf && manager.isActiveAndEnabled && camera.activeInHierarchy,
        "Isolation leaves manager and external camera active");
    check(manager.CanRestoreVisibility, "Isolation captures a restore point");
    manager.SetGroupVisible("glass-entrance", true);
    check(sculpture.activeInHierarchy && glass.activeInHierarchy && !tile.activeInHierarchy,
        "Additional groups can be combined with an isolated group");
    manager.IsolateGroup("historic-building");
    check(historic.activeInHierarchy && !sculpture.activeInHierarchy && !glass.activeInHierarchy,
        "Switching isolation updates the visible group");
    manager.RestoreVisibility();
    foreach (var state in baseline)
        if (state.Key.activeSelf != state.Value) throw new System.Exception("Restore changed " + state.Key.name);
    check(true, "Restore returns to the original setup before the first isolate, including hidden objects");
    check(!manager.CanRestoreVisibility, "Restoring clears the restore point");
    controller.RefreshTiles();
    check(controller.Tiles.Count == selectedTiles, "Visibility changes preserve modifier tile selection");

    manager.IsolateTiles();
    check(tile.activeInHierarchy && rounded.activeInHierarchy && cantilever.activeInHierarchy && undercroft.activeInHierarchy
        && !sculpture.activeInHierarchy && !glass.activeInHierarchy, "All-tiles isolation includes every tile section only");
    manager.SetTilesVisible(false);
    check(!tile.activeInHierarchy && !undercroft.activeInHierarchy && root.activeInHierarchy, "All-tiles toggle handles every tile group");
    manager.RestoreVisibility();
    manager.HideAll();
    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        if (renderer.gameObject.activeInHierarchy) throw new System.Exception("Hide All missed " + renderer.name);
    check(root.activeSelf && camera.activeSelf, "Hide All hides model meshes while keeping controls and camera available");
    facade.SetActive(false);
    manager.SetGroupVisible("glass-entrance", true);
    check(facade.activeSelf && glass.activeInHierarchy && !tile.activeInHierarchy,
        "Showing a group reactivates its hidden intermediate container");
    manager.ShowAll();
    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        if (!renderer.gameObject.activeInHierarchy) throw new System.Exception("Show All missed " + renderer.name);
    check(true, "Show All includes previously hidden vegetation and ground");
    manager.RestoreVisibility();
    check(!plant.activeSelf && !ground.activeSelf && !glass.activeSelf, "Bulk operations retain the original restore point");

    manager.IsolateGroup("unknown-id");
    check(!manager.CanRestoreVisibility && tile.activeSelf, "An invalid isolate ID leaves visibility unchanged");
    manager.IsolateGroup("sculpture");
    manager.RefreshGroups();
    check(manager.CanRestoreVisibility && !tile.activeSelf && sculpture.activeSelf,
        "Refreshing groups preserves visibility and the restore point");
    manager.enabled = false; manager.enabled = true;
    check(!tile.activeSelf && manager.CanRestoreVisibility, "Re-enabling the manager preserves isolation");
    manager.RestoreVisibility();

    var undoObjects = manager.GetVisibilityObjects();
    UnityEditor.Undo.IncrementCurrentGroup();
    UnityEditor.Undo.RecordObjects(undoObjects.ToArray(), "Mudaac visibility test");
    manager.HideAll();
    UnityEditor.Undo.FlushUndoRecordObjects();
    UnityEditor.Undo.IncrementCurrentGroup();
    UnityEditor.Undo.PerformUndo();
    check(tile.activeSelf && sculpture.activeSelf && !plant.activeSelf && !manager.CanRestoreVisibility,
        "Undo restores GameObject activation and manager restore state together");
    UnityEditor.Undo.PerformRedo();
    check(!tile.activeSelf && !sculpture.activeSelf && manager.CanRestoreVisibility,
        "Redo reapplies visibility and its restore point");
    manager.RestoreVisibility();
    foreach (var item in undoObjects) UnityEditor.Undo.ClearUndo(item);

    manager.IsolateGroup("historic-building");
    tempSceneWritten = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, sceneSavePath);
    check(tempSceneWritten, "Isolated scene saves successfully");
    string savedScene = System.IO.File.ReadAllText(sceneSavePath);
    check(savedScene.Contains("historic-building") && savedScene.Contains("previousVisibility:"),
        "Semantic groups and restore state are serialized with the scene");
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
    UnityEditor.AssetDatabase.ImportAsset(sceneSavePath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
    scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(sceneSavePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
    foreach (GameObject loadedRoot in scene.GetRootGameObjects())
    {
        if (loadedRoot.name == "Object manager checks") root = loadedRoot;
        if (loadedRoot.name == "External camera") camera = loadedRoot;
    }
    manager = root.GetComponent<MudaacObjectManager>();
    tile = find("tiles:RightFacade").objects[0];
    sculpture = find("sculpture").objects[0];
    historic = find("historic-building").objects[0];
    plant = find("vegetation").objects[0];
    glass = find("glass-entrance").objects[0];
    unknown = find("other").objects[0];
    check(manager.CanRestoreVisibility && historic.activeInHierarchy && !tile.activeInHierarchy && !sculpture.activeInHierarchy,
        "Reopening a saved scene preserves isolation and its restore point");
    manager.RestoreVisibility();
    check(tile.activeInHierarchy && sculpture.activeInHierarchy && !plant.activeSelf && !glass.activeSelf,
        "Restore after scene reload recovers the original visibility setup");
    UnityEngine.Object.DestroyImmediate(unknown); manager.RefreshGroups();
    bool hasOther = false;
    foreach (var group in manager.Groups) if (group.id == "other") hasOther = true;
    check(!hasOther, "Refreshing removes deleted objects and empty groups");
    manager.IsolateGroup("sculpture");
    UnityEngine.Object.DestroyImmediate(historic);
    manager.RestoreVisibility();
    check(!manager.CanRestoreVisibility && tile.activeSelf, "Restoring safely skips deleted snapshot objects");
    report.AppendLine("All " + passed + " checks passed.");
    return report.ToString();
}
finally
{
    UnityEngine.Object.DestroyImmediate(root);
    UnityEngine.Object.DestroyImmediate(camera);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(activeScene);
}
