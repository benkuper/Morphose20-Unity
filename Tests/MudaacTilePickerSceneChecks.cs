// Execute through Unity MCP execute_code in Edit Mode with a Scene view open.
// Sends Scene GUI events and restores the user's selection, view and active scene.
var view = UnityEditor.SceneView.lastActiveSceneView;
if (view == null) throw new System.Exception("Open a Scene view before running picker input checks.");
var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var selection = UnityEditor.Selection.objects;
var pivot = view.pivot; var rotation = view.rotation; float size = view.size;
bool orthographic = view.orthographic;
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
var root = new GameObject("Tile picker input checks");
UnityEditor.Editor editor = null;
System.Action<UnityEditor.SceneView> handler = null;
var report = new System.Text.StringBuilder();
int passed = 0;
System.Action<bool, string> check = (condition, name) => {
    if (!condition) throw new System.Exception("FAIL: " + name);
    passed++; report.AppendLine("PASS: " + name);
};
try
{
    root.transform.position = Vector3.one * 10000f;
    var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
    panel.name = "Concrete panel picker input";
    panel.transform.SetParent(root.transform, false);
    panel.transform.localPosition = Vector3.right * 2f;
    var renderer = panel.GetComponent<Renderer>();
    var controller = root.AddComponent<MudaacTileController>();
    var shared = new GameObject("Shared picker input"); shared.transform.SetParent(root.transform, false);
    var modifier = shared.AddComponent<MudaacTileSelectorModifier>(); controller.globalModifiers = shared;
    UnityEditor.Selection.activeGameObject = root;
    editor = UnityEditor.Editor.CreateEditor(modifier);
    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    var onSceneGUI = editor.GetType().GetMethod("OnSceneGUI", flags);
    editor.GetType().GetMethod("StartPicking", flags).Invoke(editor, null);
    controller.EvaluateTiles(0f);
    view.LookAt(panel.transform.position, Quaternion.identity, 4f, false, true);
    Vector2 point = Vector2.zero;
    int consumedClicks = 0;
    handler = sceneView => {
        if (sceneView != view) return;
        var guiPoint = UnityEditor.HandleUtility.WorldToGUIPoint(renderer.bounds.center);
        point = GUIUtility.GUIToScreenPoint(guiPoint) - view.position.position;
        var eventType = Event.current.type;
        onSceneGUI.Invoke(editor, null);
        if (eventType == EventType.MouseDown && Event.current.type == EventType.Used) consumedClicks++;
    };
    UnityEditor.SceneView.duringSceneGui += handler;
    view.SendEvent(new Event { type = EventType.Layout });
    view.SendEvent(new Event { type = EventType.Repaint });
    System.Action<EventModifiers> click = modifiers => {
        view.SendEvent(new Event { type = EventType.Layout, mousePosition = point });
        view.SendEvent(new Event { type = EventType.MouseMove, mousePosition = point });
        view.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = point, modifiers = modifiers });
        view.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = point, modifiers = modifiers });
    };
    click(EventModifiers.None);
    check(modifier.selectedTiles.Contains(renderer) && UnityEditor.Selection.activeGameObject == root && consumedClicks == 1,
        "A Scene click selects a tile, consumes the event and keeps the Inspector target");
    click(EventModifiers.None);
    check(modifier.selectedTiles.Count == 0 && !modifier.selectAllWhenEmpty, "A second Scene click toggles the tile off");
    click(EventModifiers.Shift); click(EventModifiers.Shift);
    check(modifier.selectedTiles.Count == 1, "Shift-click adds without toggling an existing selection off");
    click(EventModifiers.Control);
    check(modifier.selectedTiles.Count == 0, "Ctrl-click removes a tile");
    renderer.enabled = false;
    click(EventModifiers.None);
    check(modifier.selectedTiles.Contains(renderer), "Hidden tile bounds remain selectable");
    view.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
    check(!modifier.IsEditingSelection, "Escape ends the Scene picker");
    Undo.ClearUndo(modifier);
    report.AppendLine("All " + passed + " Scene picker input checks passed.");
    return report.ToString();
}
finally
{
    if (handler != null) UnityEditor.SceneView.duringSceneGui -= handler;
    if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
    view.LookAt(pivot, rotation, size, orthographic, true);
    UnityEngine.Object.DestroyImmediate(root);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(activeScene);
    UnityEditor.Selection.objects = selection;
}
