// Execute through Unity MCP execute_code in Edit Mode. All objects live in a preview scene.
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var root = new GameObject("Liquid seams checks");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
Material source = null;
var report = new System.Text.StringBuilder();
int passed = 0;
System.Action<bool, string> check = (condition, name) => {
    if (!condition) throw new System.Exception("FAIL: " + name + "\n" + report);
    passed++; report.AppendLine("PASS: " + name);
};
try
{
    var effect = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacLiquidSeams.mat");
    check(effect != null && effect.HasProperty("_SeamShape") && !ShaderUtil.ShaderHasError(effect.shader),
        "HDRP liquid material imports without shader errors");
    source = new Material(Shader.Find("HDRP/Lit")); source.SetColor("_BaseColor", Color.white);
    var renderers = new System.Collections.Generic.List<Renderer>();
    for (int i = 0; i < 2; i++)
    {
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Concrete panel seams " + i;
        panel.transform.SetParent(root.transform, false);
        panel.transform.localPosition = new Vector3(i * 2 - 1, 0, 0);
        panel.transform.localScale = new Vector3(2, 2, 0.1f);
        var renderer = panel.GetComponent<Renderer>();
        renderer.sharedMaterials = new[] { source, source }; renderers.Add(renderer);
    }
    var block = new MaterialPropertyBlock();
    block.SetColor("_BaseColor", Color.blue); block.SetFloat("_SeamSentinel", 42);
    renderers[0].SetPropertyBlock(block);
    block.SetColor("_BaseColor", Color.red); block.SetFloat("_SeamSentinel", 17);
    renderers[0].SetPropertyBlock(block, 1);
    var controller = root.AddComponent<MudaacTileController>();
    var seams = root.AddComponent<MudaacTileLiquidSeamsModifier>(); seams.seamMaterial = effect;
    controller.EvaluateTiles(10);
    check(renderers[0].sharedMaterials[0] == effect && renderers[0].sharedMaterials[1] == effect,
        "All source material slots use the shared effect material");
    renderers[0].GetPropertyBlock(block, 0);
    check(block.GetColor("_BaseColor") == Color.blue, "Renderer colour override remains the substrate");
    renderers[0].GetPropertyBlock(block, 1);
    check(block.GetColor("_BaseColor") == Color.red, "Per-slot substrate colours remain independent");
    check(block.GetVector("_SeamRect") == new Vector4(-1, 0, 1, 1), "Seams use the real first tile footprint");
    var other = new MaterialPropertyBlock(); renderers[1].GetPropertyBlock(other, 0);
    check(other.GetVector("_SeamRect") == new Vector4(1, 0, 1, 1), "Neighbor footprint meets at the authored shared edge");
    check(block.GetVector("_SeamFlow") == other.GetVector("_SeamFlow"), "Neighbor tiles share the flow field and phase");
    check(block.GetVector("_SeamNetwork").x >= 4 && block.GetVectorArray("_SeamEdges").Length == MudaacLiquidSeamNetwork.MaximumEdges,
        "Tiles receive the actual neighboring seam segments");
    seams.interiorCellCount = 7; seams.interiorLineWidth = 0.03f;
    seams.interiorCurvature = 0.2f; seams.interiorMotion = 0.8f;
    controller.EvaluateTiles(10);
    renderers[0].GetPropertyBlock(block, 0);
    check(block.GetVector("_SeamInteriorLines") == new Vector4(7, 0.03f, 0.2f, 0.8f),
        "Parametric interior line controls reach the shader");
    check(seams.FlowTime == 0, "Initial evaluation does not jump to Editor uptime");
    controller.EvaluateTiles(10.1f); float running = seams.FlowTime;
    check(running > 0, "Flow advances with controller time");
    seams.paused = true; controller.EvaluateTiles(10.2f);
    check(seams.FlowTime == running, "Pause freezes the flow");
    seams.paused = false; seams.flowSpeed = 0; controller.EvaluateTiles(10.3f);
    check(seams.FlowTime == running, "Zero speed freezes the field");
    seams.flowSpeed = 1; controller.RestoreTiles(); controller.EvaluateTiles(1000);
    check(seams.FlowTime == running, "Stopping preview does not accumulate a time jump");
    seams.RestartFlow(); controller.EvaluateTiles(1001);
    check(seams.FlowTime == 0, "Restart begins at the same deterministic phase");
    seams.seamWidth = 0.07f; seams.seed = 91; seams.interiorBlend = 0;
    controller.EvaluateTiles(1001.1f); renderers[0].GetPropertyBlock(block, 0);
    check(block.GetVector("_SeamShape").x == 0.07f && block.GetVector("_SeamFlow").z == 91,
        "Shape and seed edits take effect immediately");
    check(block.GetVector("_SeamSurface").z == 0, "Original-colour mode reaches the shader");
    var rest = block.GetVector("_SeamRestOrigin");
    var tile = controller.Tiles[0]; tile.LocalPosition += Vector3.up * 4; tile.Apply();
    controller.EvaluateTiles(1001.2f); renderers[0].GetPropertyBlock(block, 0);
    check(block.GetVector("_SeamRestOrigin") == rest, "Other tile motion cannot slide the seam coordinates");
    root.transform.position = new Vector3(5, 3, 0); controller.EvaluateTiles(1001.3f);
    renderers[0].GetPropertyBlock(block, 0);
    check(block.GetVector("_SeamRect") == new Vector4(4, 3, 1, 1), "Moving the controller rebuilds footprints");
    controller.RestoreTiles();
    check(renderers[0].sharedMaterials[0] == source && renderers[0].sharedMaterials[1] == source,
        "Restoration returns every original material reference");
    renderers[0].GetPropertyBlock(block);
    check(block.GetColor("_BaseColor") == Color.blue && block.GetFloat("_SeamSentinel") == 42,
        "Restoration preserves renderer property blocks");
    renderers[0].GetPropertyBlock(block, 1);
    check(block.GetColor("_BaseColor") == Color.red && block.GetFloat("_SeamSentinel") == 17,
        "Restoration preserves per-slot property blocks");
    seams.modifierWeight = 0; controller.EvaluateTiles(1001.4f);
    check(renderers[0].sharedMaterial == source, "Zero weight leaves the original material in place");
    seams.modifierWeight = 1; seams.seamMaterial = null; controller.EvaluateTiles(1001.5f);
    check(renderers[0].sharedMaterial == source, "Missing material safely leaves tiles unchanged");
    seams.seamMaterial = effect; controller.EvaluateTiles(1001.6f); seams.enabled = false;
    check(renderers[0].sharedMaterial == source, "Disabling the modifier immediately restores materials");
    seams.enabled = true; controller.EvaluateTiles(1001.7f);
    controller.TileGroups[0].tiles[0].included = false; controller.EvaluateTiles(1001.8f);
    check(renderers[0].sharedMaterial == source && controller.Tiles.Count == 1,
        "Excluded tiles restore while remaining tiles keep their seams");
    return passed + " integration checks passed\n" + report;
}
finally
{
    UnityEngine.Object.DestroyImmediate(root);
    if (source != null) UnityEngine.Object.DestroyImmediate(source);
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
