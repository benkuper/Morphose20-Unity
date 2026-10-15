// Execute through Unity MCP execute_code in Edit Mode. Uses an isolated additive
// scene; the production scene and its controller configuration are untouched.
var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
var root = new GameObject("Tile restore checks");
Material material = null;
string savePath = "Assets/__MudaacTileRestoreChecks.unity";
bool savedScene = false;
var report = new System.Text.StringBuilder();
int passed = 0;
System.Action<bool, string> check = (condition, name) => {
    if (!condition) throw new System.Exception("FAIL: " + name);
    passed++; report.AppendLine("PASS: " + name);
};
try
{
    var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
    panel.name = "Concrete panel 0063";
    panel.transform.SetParent(root.transform, false);
    panel.transform.localPosition = new Vector3(2f, 3f, 4f);
    panel.transform.localRotation = Quaternion.Euler(12f, 23f, 34f);
    panel.transform.localScale = new Vector3(0.5f, 0.8f, 1.2f);
    var renderer = panel.GetComponent<Renderer>();
    var source = GameObject.Find("Mudaac Model").GetComponent<MudaacTileController>();
    material = new Material(source.Tiles[0].Renderer.sharedMaterial);
    renderer.sharedMaterials = new[] { material, material };
    int colorId = Shader.PropertyToID("_BaseColor");
    int sentinelId = Shader.PropertyToID("_TileRestoreSentinel");
    var block = new MaterialPropertyBlock();
    block.SetColor(colorId, Color.blue);
    block.SetFloat(sentinelId, 42f);
    renderer.SetPropertyBlock(block);
    block.Clear(); block.SetColor(colorId, Color.green); block.SetFloat(sentinelId, 17f);
    renderer.SetPropertyBlock(block, 1);
    Vector3 position = panel.transform.localPosition;
    Quaternion rotation = panel.transform.localRotation;
    Vector3 scale = panel.transform.localScale;
    System.Func<bool> restored = () => Vector3.Distance(panel.transform.localPosition, position) < 0.00001f
        && Quaternion.Angle(panel.transform.localRotation, rotation) < 0.001f
        && Vector3.Distance(panel.transform.localScale, scale) < 0.00001f;
    System.Func<bool> materialsRestored = () => {
        renderer.GetPropertyBlock(block, 0);
        if (!block.isEmpty) return false;
        renderer.GetPropertyBlock(block);
        if (block.GetColor(colorId) != Color.blue || block.GetFloat(sentinelId) != 42f) return false;
        renderer.GetPropertyBlock(block, 1);
        return block.GetColor(colorId) == Color.green && block.GetFloat(sentinelId) == 17f;
    };
    var controller = root.AddComponent<MudaacTileController>();
    var offset = root.AddComponent<MudaacTileOffsetModifier>(); offset.offset = Vector3.one;
    var rotating = root.AddComponent<MudaacTileRotationModifier>(); rotating.rotation = new Vector3(19f, 41f, 7f);
    var scaling = root.AddComponent<MudaacTileScaleModifier>(); scaling.scale = new Vector3(0.4f, 2f, 0.6f);
    var paint = root.AddComponent<MudaacTileMaterialModifier>();
    paint.colorOverWeight.SetKeys(new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.red, 1f) },
        new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
    var editorUpdate = typeof(MudaacTileController).GetMethod("EditorUpdate",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    controller.previewInEditMode = true;
    editorUpdate.Invoke(controller, null);
    renderer.GetPropertyBlock(block, 0);
    check(!restored() && block.GetColor(colorId) == Color.red, "Edit-mode preview applies pose and material effects");
    controller.enabled = false;
    check(restored() && materialsRestored(), "Disabling controller restores pose and renderer/slot material blocks");
    editorUpdate.Invoke(controller, null);
    check(restored() && materialsRestored(), "A stale editor callback cannot apply preview while disabled");
    controller.enabled = true;
    controller.EvaluateTiles(0f);
    root.SetActive(false);
    check(restored() && materialsRestored(), "Deactivating the controller GameObject restores tiles");
    root.SetActive(true);
    for (int i = 0; i < 5; i++)
    {
        controller.EvaluateTiles(i);
        controller.TileGroups[0].included = false;
        controller.EvaluateTiles(i);
        check(controller.Tiles.Count == 0 && restored() && materialsRestored(),
            "Group exclusion restores pose and materials without an explicit refresh, cycle " + i);
        controller.TileGroups[0].included = true;
        controller.EvaluateTiles(i);
    }
    controller.TileGroups[0].tiles[0].included = false;
    controller.EvaluateTiles(0f);
    check(controller.Tiles.Count == 0 && restored() && materialsRestored(), "Individual tile exclusion restores all effects");
    controller.TileGroups[0].tiles[0].included = true;
    controller.EvaluateTiles(0f);
    controller.previewInEditMode = false;
    editorUpdate.Invoke(controller, null);
    check(restored() && materialsRestored(), "Turning off edit-mode preview restores all effects");

    // An Undo snapshot may contain a previewed Transform. Restoring it must not
    // turn the preview pose into the next binding's authored pose.
    controller.EvaluateTiles(0f);
    Undo.IncrementCurrentGroup();
    Undo.RecordObject(panel.transform, "Tile preview snapshot check");
    panel.transform.localPosition += Vector3.right;
    Undo.FlushUndoRecordObjects();
    controller.RestoreTiles();
    Undo.PerformUndo();
    check(restored(), "Undo cannot reinstate a preview pose as the authored layout");
    controller.RefreshTiles();
    controller.EvaluateTiles(0f); controller.enabled = false;
    check(restored() && materialsRestored(), "Refresh after Undo keeps the original pose and material blocks");
    Undo.ClearUndo(panel.transform);
    controller.enabled = true;

    // Model transient binding loss across deserialization, including a tile that
    // is now excluded. The serialized selection is the recovery source.
    controller.EvaluateTiles(0f);
    string snapshot = JsonUtility.ToJson(controller);
    controller.RestoreTiles(); // Release the non-serializable material blocks.
    panel.transform.localPosition += Vector3.one * 7f;
    panel.transform.localScale *= 3f;
    var tilesField = typeof(MudaacTileController).GetField("tiles",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    ((System.Collections.Generic.List<MudaacTile>)tilesField.GetValue(controller)).Clear();
    JsonUtility.FromJsonOverwrite(snapshot, controller);
    controller.TileGroups[0].included = false;
    controller.enabled = false;
    check(restored(), "Serialized rest pose restores an excluded tile after transient bindings are lost");
    controller.enabled = true;
    check(controller.Tiles.Count == 0 && restored(), "Re-enable preserves excluded groups and authored poses");
    controller.TileGroups[0].included = true;
    controller.EvaluateTiles(0f); controller.RefreshTiles();
    check(restored(), "Refresh restores active effects before rebuilding bindings");

    // Intentional authoring is explicit, and includes excluded candidates.
    controller.TileGroups[0].included = false;
    controller.RefreshTiles();
    panel.transform.localPosition += Vector3.right * 2f;
    position = panel.transform.localPosition;
    controller.CaptureCurrentTileLayout();
    controller.TileGroups[0].included = true;
    controller.EvaluateTiles(0f); controller.RestoreTiles();
    check(restored(), "Capture Current Tile Layout accepts an authored edit made while excluded");
    controller.EvaluateTiles(0f); controller.CaptureCurrentTileLayout();
    check(!controller.previewInEditMode && restored(), "Capturing while effects are active cannot bake preview transforms");

    check(!System.IO.File.Exists(savePath), "Temporary reload-test path is unused");
    controller.previewInEditMode = true;
    controller.EvaluateTiles(0f);
    check(UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, savePath), "Test scene saves");
    savedScene = true;
    check(restored() && materialsRestored(), "Scene save restores poses and material blocks before serialization");
    controller.EvaluateTiles(1f);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
    scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(savePath,
        UnityEditor.SceneManagement.OpenSceneMode.Additive);
    controller = null;
    foreach (var item in scene.GetRootGameObjects())
        if (item.name == "Tile restore checks") { root = item; controller = root.GetComponent<MudaacTileController>(); break; }
    check(controller != null && controller.TileGroups.Count == 1, "Scene reload retains tile selections and rest poses");
    controller.enabled = false;
    panel = root.GetComponentInChildren<MeshRenderer>().gameObject;
    check(restored(), "Disabling after a scene reload restores the authored layout");
    report.AppendLine("All " + passed + " tile restoration checks passed.");
    return report.ToString();
}
finally
{
    if (root != null) UnityEngine.Object.DestroyImmediate(root);
    if (material != null) UnityEngine.Object.DestroyImmediate(material);
    if (scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(activeScene);
    if (savedScene) UnityEditor.AssetDatabase.DeleteAsset(savePath);
}
