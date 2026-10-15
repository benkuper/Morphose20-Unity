// Execute this method body with Unity MCP execute_code (CodeDom/C# 6 compatible).
// Uses an isolated temporary scene and restores the active scene.
// Delete Temp/MudaacTileChecks.unity after running; no Assets files are written.
var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var live = GameObject.Find("Mudaac Model").GetComponent<MudaacTileController>();
var report = new System.Text.StringBuilder();
int passed = 0;
System.Action<bool, string> check = (condition, name) => {
    if (!condition) throw new System.Exception("FAIL: " + name);
    passed++; report.AppendLine("PASS: " + name);
};
check(live.Tiles.Count == 222, "Live building selects 222 panels across the enabled architectural groups");
foreach (var tile in live.Tiles)
    if (tile.Renderer.name.StartsWith("Undercroft panel"))
        throw new System.Exception("Unexpected building renderer: " + tile.Renderer.name);
check(live.TileGroups.Count == 10, "Live building exposes ten architectural groups");

var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
var root = new GameObject("Mudaac checks");
Material material = null;
Mesh mesh = null;
try
{
    var parent = new GameObject("Imported parent").transform;
    parent.SetParent(root.transform, false);
    parent.localScale = Vector3.one * 100f;
    parent.localRotation = Quaternion.Euler(-90f, 0f, 0f);
    var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
    panel.name = "Concrete panel test";
    panel.transform.SetParent(parent, false);
    panel.transform.localPosition = new Vector3(0.02f, 0.04f, 0.08f);
    panel.transform.localRotation = Quaternion.Euler(7f, 13f, 9f);
    panel.transform.localScale = new Vector3(0.02f, 0.01f, 0.03f);
    mesh = UnityEngine.Object.Instantiate(panel.GetComponent<MeshFilter>().sharedMesh);
    var vertices = mesh.vertices;
    for (int i = 0; i < vertices.Length; i++) vertices[i] += new Vector3(0.7f, 0.3f, -0.2f);
    mesh.vertices = vertices; mesh.RecalculateBounds();
    panel.GetComponent<MeshFilter>().sharedMesh = mesh;
    var renderer = panel.GetComponent<Renderer>();
    material = new Material(live.Tiles[0].Renderer.sharedMaterial);
    renderer.sharedMaterials = new[] { material, material };
    int colorId = Shader.PropertyToID("_BaseColor");
    int customId = Shader.PropertyToID("_MudaacTestSentinel");
    var initial = new MaterialPropertyBlock();
    initial.SetFloat(customId, 42f);
    initial.SetColor(colorId, Color.blue);
    renderer.SetPropertyBlock(initial);
    var slot = new MaterialPropertyBlock();
    slot.SetColor(colorId, Color.green);
    slot.SetFloat(customId, 17f);
    renderer.SetPropertyBlock(slot, 1);
    var ignored = GameObject.CreatePrimitive(PrimitiveType.Cube);
    ignored.name = "Window frame"; ignored.transform.SetParent(parent, false);
    Vector3 ignoredPosition = ignored.transform.localPosition;

    var rounded = GameObject.CreatePrimitive(PrimitiveType.Cube);
    rounded.name = "Left lower rounded wall | test"; rounded.transform.SetParent(parent, false);
    var cantilever = GameObject.CreatePrimitive(PrimitiveType.Cube);
    cantilever.name = "Left upper cantilever | test"; cantilever.transform.SetParent(parent, false);
    var undercroft = GameObject.CreatePrimitive(PrimitiveType.Cube);
    undercroft.name = "Undercroft panel test"; undercroft.transform.SetParent(parent, false);
    Vector3 undercroftPosition = undercroft.transform.localPosition;

    var controller = root.AddComponent<MudaacTileController>();
    check(controller.Tiles.Count == 3, "Defaults include rounded wall and cantilever, excluding undercroft and non-panels");
    foreach (var group in controller.TileGroups)
        if (group.kind == MudaacTileGroup.Kind.LeftRoundedWall || group.kind == MudaacTileGroup.Kind.LeftCantilever)
            group.included = false;
    controller.RefreshTiles();
    check(controller.Tiles.Count == 1, "Group checkboxes select the controlled panels");
    var tile = controller.Tiles[0];
    Vector3 position = panel.transform.localPosition;
    Quaternion rotation = panel.transform.localRotation;
    Vector3 scale = panel.transform.localScale;
    Vector3 centre = renderer.bounds.center;
    var offset = root.AddComponent<MudaacTileOffsetModifier>();
    offset.space = MudaacTileModifier.CoordinateSpace.World;
    offset.offset = new Vector3(0.5f, -0.2f, 1.2f);
    controller.EvaluateTiles(0f);
    check(Vector3.Distance(renderer.bounds.center, centre + offset.offset) < 0.0001f,
        "Offsets use metres under a rotated parent scaled by 100");
    for (int i = 0; i < 200; i++) controller.EvaluateTiles(i);
    check(Vector3.Distance(renderer.bounds.center, centre + offset.offset) < 0.0001f,
        "200 evaluations do not accumulate offset");
    check(ignored.transform.localPosition == ignoredPosition, "Non-panel layout stays unchanged");
    check(undercroft.transform.localPosition == undercroftPosition, "Excluded undercroft stays in its authored position");
    var otherGroup = controller.TileGroups[controller.TileGroups.Count - 1];
    otherGroup.included = false;
    controller.RefreshTiles();
    check(controller.Tiles.Count == 0 && panel.transform.localPosition == position,
        "Unchecking a group restores its affected panels immediately");
    otherGroup.included = true;
    otherGroup.tiles[0].included = false;
    controller.RefreshTiles();
    check(controller.Tiles.Count == 0, "Individual panel checkboxes exclude a panel within an enabled group");
    otherGroup.tiles[0].included = true;
    controller.RefreshTiles();
    check(controller.Tiles.Count == 1, "Individual panels can be included again");
    controller.RefreshTiles();
    check(Vector3.Distance(panel.transform.localPosition, position) < 0.00001f, "Refresh restores before recapturing");
    check(!controller.TileGroups[0].included && !controller.TileGroups[1].included,
        "Refresh preserves unchecked architectural groups");
    tile = controller.Tiles[0];

    offset.enabled = false;
    var scaling = root.AddComponent<MudaacTileScaleModifier>();
    scaling.scale = new Vector3(0.4f, 0.8f, 0.5f);
    var rotating = root.AddComponent<MudaacTileRotationModifier>();
    rotating.rotation = new Vector3(23f, 91f, -17f);
    controller.EvaluateTiles(1f);
    check(Vector3.Distance(renderer.bounds.center, centre) < 0.0001f,
        "Scale and rotation preserve an off-centre mesh pivot");
    check(Vector3.Distance(panel.transform.localScale, Vector3.Scale(scale, scaling.scale)) < 0.00001f,
        "Scale multiplies the authored nonuniform scale");
    scaling.modifierWeight = 0f; rotating.modifierWeight = 0f;
    controller.EvaluateTiles(2f);
    check(panel.transform.localPosition == position && Quaternion.Angle(panel.transform.localRotation, rotation) < 0.001f
        && panel.transform.localScale == scale, "Zero transform weights restore authored pose");
    scaling.enabled = false; rotating.enabled = false;

    var maskObject = new GameObject("Masks"); maskObject.transform.SetParent(root.transform, false);
    var maskA = maskObject.AddComponent<MudaacTileInfluencer>(); maskA.weight = 0.25f;
    var maskB = maskObject.AddComponent<MudaacTileInfluencer>(); maskB.weight = 0.75f;
    offset.influencers.Add(maskA); offset.influencers.Add(maskB);
    offset.weightMode = MudaacTileModifier.WeightMode.Max;
    check(Mathf.Abs(offset.GetWeight(tile, controller, 0f) - 0.75f) < 0.00001f, "Max influencer weights");
    offset.weightMode = MudaacTileModifier.WeightMode.Average;
    check(Mathf.Abs(offset.GetWeight(tile, controller, 0f) - 0.5f) < 0.00001f, "Average influencer weights");
    offset.weightMode = MudaacTileModifier.WeightMode.Multiply;
    check(Mathf.Abs(offset.GetWeight(tile, controller, 0f) - 0.1875f) < 0.00001f, "Multiply influencer weights");
    maskA.enabled = false; maskB.enabled = false;
    check(offset.GetWeight(tile, controller, 0f) == 1f, "Disabled influencers fall back to full coverage");
    maskA.enabled = true; offset.influencerWeight = 0.5f; offset.modifierWeight = 0.8f;
    check(Mathf.Abs(offset.GetWeight(tile, controller, 0f) - 0.5f) < 0.00001f, "Modifier and influencer weight blending");
    offset.influencers.Clear(); offset.influencerWeight = 1f; offset.modifierWeight = 1f;

    var sphere = maskObject.AddComponent<MudaacTileSphereInfluencer>(); sphere.radius = 2f;
    maskObject.transform.position = tile.RestWorldCenter;
    check(Mathf.Abs(sphere.GetWeightForTile(tile, controller, 0f) - 1f) < 0.00001f, "Sphere centre has full weight");
    offset.enabled = true; offset.influencers.Add(sphere); controller.EvaluateTiles(3f);
    check(Mathf.Abs(sphere.GetWeightForTile(tile, controller, 0f) - 1f) < 0.00001f,
        "Influencers sample authored centres after movement");
    maskObject.transform.position += Vector3.right * 3f;
    check(sphere.GetWeightForTile(tile, controller, 0f) == 0f, "Sphere excludes distant panels");
    offset.enabled = false;
    var axial = maskObject.AddComponent<MudaacTileAxialInfluencer>(); axial.radius = 2f;
    maskObject.transform.position = tile.RestWorldCenter + Vector3.up * 50f;
    check(Mathf.Abs(axial.GetWeightForTile(tile, controller, 0f) - 1f) < 0.00001f, "Axial influence ignores height");
    var sweep = maskObject.AddComponent<MudaacTileProgressionInfluencer>();
    foreach (float fade in new[] { 0f, 0.1f, 1f })
    {
        sweep.fade = fade; sweep.progression = 0f;
        check(sweep.GetWeightForTile(tile, controller, 0f) == 0f, "Progression starts empty, fade=" + fade);
        sweep.progression = 1f;
        check(sweep.GetWeightForTile(tile, controller, 0f) == 1f, "Progression ends full, fade=" + fade);
    }

    controller.RestoreTiles();
    var paint = root.AddComponent<MudaacTileMaterialModifier>();
    paint.colorOverWeight.SetKeys(new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.red, 1f) },
        new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
    paint.modifierWeight = 0.5f;
    Color sharedColor = material.GetColor(colorId);
    for (int i = 0; i < 10; i++) controller.EvaluateTiles(i);
    var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block, 0);
    check(block.GetColor(colorId) == Color.Lerp(Color.blue, Color.red, 0.5f),
        "Material blends from original renderer property block without drift");
    check(block.GetFloat(customId) == 42f, "Material preserves unrelated renderer properties");
    renderer.GetPropertyBlock(block, 1);
    check(block.GetColor(colorId) == Color.Lerp(Color.green, Color.red, 0.5f) && block.GetFloat(customId) == 17f,
        "Material preserves and blends each material-slot override");
    check(material.GetColor(colorId) == sharedColor, "Shared material is untouched");
    paint.modifierWeight = 0f; controller.EvaluateTiles(1f);
    renderer.GetPropertyBlock(block, 0);
    check(block.isEmpty, "Zero material weight removes temporary slot block");
    renderer.GetPropertyBlock(block);
    check(block.GetColor(colorId) == Color.blue && block.GetFloat(customId) == 42f,
        "Zero material weight restores original renderer block");
    paint.enabled = false;

    var globals = new GameObject("Shared modifiers"); globals.transform.SetParent(root.transform, false);
    var sharedOffset = globals.AddComponent<MudaacTileOffsetModifier>();
    sharedOffset.space = MudaacTileModifier.CoordinateSpace.World; sharedOffset.offset = Vector3.up * 0.3f;
    controller.globalModifiers = globals;
    offset.enabled = true; offset.influencers.Clear();
    controller.EvaluateTiles(2f);
    check(Vector3.Distance(renderer.bounds.center, centre + offset.offset + sharedOffset.offset) < 0.0001f,
        "Local and shared modifiers stack");
    controller.enabled = false;
    check(panel.transform.localPosition == position && panel.transform.localScale == scale,
        "Disabling controller restores the authored layout");
    controller.enabled = true;
    check(controller.Tiles.Count == 1 && !controller.TileGroups[0].included,
        "Disable and re-enable preserves group selections");
    controller.EvaluateTiles(4f);
    controller.previewInEditMode = true;
    string savePath = "Temp/MudaacTileChecks.unity";
    check(UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, savePath), "Temporary scene saves successfully");
    check(panel.transform.localPosition == position, "Scene saving restores authored poses before serialization");
    string savedScene = System.IO.File.ReadAllText(savePath);
    check(savedScene.Contains("tileGroups:") && savedScene.Contains("included: 0"),
        "Group and panel selections are serialized with the scene");
    controller.previewInEditMode = false;

    controller.globalModifiers = null; offset.enabled = false;
    controller.explicitTiles.Add(ignored.GetComponent<Renderer>());
    controller.explicitTiles.Add(ignored.GetComponent<Renderer>());
    controller.explicitTiles.Add(null);
    controller.RefreshTiles();
    check(controller.Tiles.Count == 1 && controller.Tiles[0].Renderer == ignored.GetComponent<Renderer>(),
        "Explicit selection replaces discovery, ignores nulls and deduplicates");
    UnityEngine.Object.DestroyImmediate(ignored); controller.EvaluateTiles(0f); controller.RestoreTiles();
    check(true, "Deleted bound tiles are safely skipped");
    report.AppendLine("All " + passed + " checks passed.");
    return report.ToString();
}
finally
{
    UnityEngine.Object.DestroyImmediate(root);
    if (material != null) UnityEngine.Object.DestroyImmediate(material);
    if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(activeScene);
}
