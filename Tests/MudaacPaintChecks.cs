// Execute this method body with Unity MCP execute_code. Uses an isolated additive scene.
var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
var root = new GameObject("Paint checks");
var report = new System.Text.StringBuilder();
int passed = 0;
System.Action<bool, string> check = (condition, name) => {
    if (!condition) throw new System.Exception("FAIL: " + name + "\n" + report);
    passed++; report.AppendLine("PASS: " + name);
};
Material source = null;
string savePath = "Assets/__MudaacPaintChecks.unity";
try
{
    var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacPaint.mat");
    check(material != null && material.HasProperty("_PaintSpread") && !ShaderUtil.ShaderHasError(material.shader),
        "The simplified HDRP paint shader imports without errors");
    source = new Material(Shader.Find("HDRP/Lit")); source.SetColor("_BaseColor", Color.white);
    var renderers = new System.Collections.Generic.List<Renderer>();
    for (int i = 0; i < 8; i++)
    {
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Concrete panel paint check " + i;
        panel.transform.SetParent(root.transform, false);
        panel.transform.position = new Vector3(i * 2, 0, 0);
        panel.transform.localScale = new Vector3(0.9f, 1, 0.1f);
        var renderer = panel.GetComponent<Renderer>(); renderer.sharedMaterials = new[] { source, source };
        renderers.Add(renderer);
    }
    var controller = root.AddComponent<MudaacTileController>();
    var graph = new MudaacPaintPropagation();
    var origins = new[] { Vector3.zero };
    var seed = new MudaacNoiseSettings { seed = 7 };
    graph.Rebuild(controller.Tiles, origins, seed);
    check(graph.Surfaces.Count == 8 && graph.MaximumDistance > 14f,
        "Continuous spread covers selected surfaces across empty gaps without architectural link settings");
    check(graph.Surfaces[0].RandomDistance == 0f, "The origin tile starts first at the randomized end of the slider");
    float randomDistance = graph.Surfaces[3].RandomDistance;
    graph.Rebuild(controller.Tiles, origins, seed);
    check(randomDistance == graph.Surfaces[3].RandomDistance, "Tile randomization is deterministic for the seed and authored pose");
    seed.seed++;
    graph.Rebuild(controller.Tiles, origins, seed);
    check(randomDistance != graph.Surfaces[3].RandomDistance, "Changing the spread seed changes tile randomization");
    root.transform.localScale = Vector3.one * 2;
    check(!graph.Matches(controller.Tiles), "Moving or scaling the facade invalidates cached geometry");
    root.transform.localScale = Vector3.one;
    var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", Color.blue);
    block.SetFloat("_PaintSentinel", 42); renderers[0].SetPropertyBlock(block);
    block.SetColor("_BaseColor", Color.green); block.SetFloat("_PaintSentinel", 17); renderers[0].SetPropertyBlock(block, 1);
    controller.RefreshTiles();
    var paint = root.AddComponent<MudaacTilePaintModifier>(); paint.paintMaterial = material;
    paint.automaticSpread = false; paint.progress = 1; paint.originInFacadeBounds = Vector3.zero;
    controller.EvaluateTiles(0);
    check(renderers[7].sharedMaterial == material, "Full progress fills even an isolated selected tile");
    check(renderers[0].sharedMaterials.Length == 2 && renderers[0].sharedMaterials[1] == material,
        "Paint handles multiple material slots");
    renderers[0].GetPropertyBlock(block, 0);
    check(block.GetColor("_BaseColor") == Color.blue, "Renderer colour overrides remain the substrate");
    renderers[0].GetPropertyBlock(block, 1);
    check(block.GetColor("_BaseColor") == Color.green, "Material-slot colour overrides remain independent");
    paint.tileRandomization = 0.4f; paint.spreadNoiseOctaves = 5; paint.spreadNoiseScale = 1.3f;
    controller.EvaluateTiles(0.1f); renderers[0].GetPropertyBlock(block, 0);
    check(Mathf.Abs(block.GetVector("_PaintSpread").x - 0.4f) < 0.001f
        && block.GetVector("_PaintNoise").w == 5f, "The blend slider and spread detail controls update live without a refresh");
    float before = paint.AnimationTime; controller.EvaluateTiles(0.2f);
    check(paint.AnimationTime > before && paint.CurrentProgress == 1,
        "Pigment time continues after full coverage");
    paint.paused = true; before = paint.AnimationTime; controller.EvaluateTiles(0.3f);
    check(paint.AnimationTime == before, "Pause freezes the paint clock"); paint.paused = false;
    paint.progress = 0; controller.EvaluateTiles(0.4f);
    check(renderers[0].sharedMaterial == source, "Scrubbing to zero restores a clean facade");
    paint.progress = 1; controller.EvaluateTiles(0.5f); controller.RestoreTiles();
    check(renderers[0].sharedMaterial == source && renderers[0].sharedMaterials[1] == source,
        "Stopping preview restores every original shared material slot");
    renderers[0].GetPropertyBlock(block);
    check(block.GetFloat("_PaintSentinel") == 42 && block.GetColor("_BaseColor") == Color.blue,
        "Stopping preview restores the renderer property block");
    renderers[0].GetPropertyBlock(block, 1);
    check(block.GetFloat("_PaintSentinel") == 17 && block.GetColor("_BaseColor") == Color.green,
        "Stopping preview restores material-slot property blocks");
    controller.EvaluateTiles(0.6f); paint.enabled = false;
    check(renderers[0].sharedMaterial == source, "Disabling paint immediately restores original materials");
    paint.enabled = true; paint.modifierWeight = 0; controller.EvaluateTiles(0.7f);
    check(renderers[0].sharedMaterial == source, "Zero modifier weight is neutral");
    paint.modifierWeight = 1; controller.EvaluateTiles(0.8f);
    controller.TileGroups[0].tiles[0].included = false; controller.EvaluateTiles(0.9f);
    check(renderers[0].sharedMaterial == source, "Excluding a tile restores its original material");
    controller.TileGroups[0].tiles[0].included = true; controller.EvaluateTiles(1f);
    check(renderers[0].sharedMaterial == material, "Re-inclusion captures the original material rather than a paint snapshot");
    controller.previewInEditMode = true;
    check(UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, savePath), "The previewing scene can be saved");
    string saved = System.IO.File.ReadAllText(savePath);
    string guid = AssetDatabase.AssetPathToGUID("Assets/Materials/MudaacPaint.mat");
    var sections = saved.Split(new[] { "MeshRenderer:" }, System.StringSplitOptions.None);
    bool clean = true;
    for (int i = 1; i < sections.Length; i++)
        clean &= !sections[i].Split(new[] { "--- !u!" }, System.StringSplitOptions.None)[0].Contains(guid);
    check(clean, "Saving never serializes temporary paint replacements into facade renderers");
    controller.previewInEditMode = false; controller.RestoreTiles();
    return "Passed " + passed + " paint checks.\n" + report;
}
finally
{
    if (root != null) UnityEngine.Object.DestroyImmediate(root);
    if (source != null) UnityEngine.Object.DestroyImmediate(source);
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(savePath) != null) AssetDatabase.DeleteAsset(savePath);
}
