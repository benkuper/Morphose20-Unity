// Execute this method body through Unity MCP execute_code in Edit Mode.
// All renderers and materials are temporary, in an isolated preview scene.
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var root = new GameObject("Paint reveal checks");
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
    var effect = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacPaintReveal.mat");
    check(effect != null && effect.HasProperty("_RevealProgress") && !ShaderUtil.ShaderHasError(effect.shader),
        "The HDRP reveal material imports without shader errors");
    source = new Material(Shader.Find("HDRP/Lit"));
    source.SetColor("_BaseColor", Color.white);
    var renderers = new System.Collections.Generic.List<Renderer>();
    for (int i = 0; i < 2; i++)
    {
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Concrete panel reveal " + i;
        panel.transform.SetParent(root.transform, false);
        panel.transform.localPosition = new Vector3(i * 4 - 2, 0, 0);
        panel.transform.localScale = new Vector3(4, 4, 0.1f);
        var renderer = panel.GetComponent<Renderer>();
        renderer.sharedMaterials = new[] { source, source };
        renderers.Add(renderer);
    }
    var block = new MaterialPropertyBlock();
    block.SetColor("_BaseColor", Color.blue); block.SetFloat("_RevealSentinel", 42);
    renderers[0].SetPropertyBlock(block);
    block.SetColor("_BaseColor", Color.red); block.SetFloat("_RevealSentinel", 17);
    renderers[0].SetPropertyBlock(block, 1);
    var controller = root.AddComponent<MudaacTileController>();
    var reveal = root.AddComponent<MudaacTilePaintRevealModifier>();
    reveal.revealMaterial = effect; reveal.automaticReveal = false; reveal.progress = 0;
    controller.EvaluateTiles(0);
    check(renderers[0].sharedMaterial == source, "Zero progress keeps the original material");
    reveal.progress = 0.5f; controller.EvaluateTiles(0.1f);
    check(renderers[0].sharedMaterials[0] == effect && renderers[0].sharedMaterials[1] == effect,
        "The reveal supports multiple original material slots");
    renderers[0].GetPropertyBlock(block, 0);
    check(block.GetColor("_BaseColor") == Color.blue, "Renderer colour overrides remain the substrate");
    renderers[0].GetPropertyBlock(block, 1);
    check(block.GetColor("_BaseColor") == Color.red, "Per-slot overrides remain independent");
    check(reveal.RevealProgress == 0.5f && reveal.EraseProgress == 0,
        "Manual progress is exact and does not erase");
    reveal.strokeCount = 10; reveal.brushAngle = 90; reveal.seed = 91;
    controller.EvaluateTiles(0.2f); renderers[0].GetPropertyBlock(block, 0);
    check(block.GetVector("_RevealProgress").y == 10 && block.GetVector("_RevealStyle").w == 91,
        "Brush controls update without a manual refresh");
    Vector4 axis = block.GetVector("_RevealAxisY");
    check(Vector3.Dot(new Vector3(axis.x, axis.y, axis.z), Vector3.left) > 0.99f,
        "Brush angle rotates the facade projection");
    var rest = block.GetVector("_RevealRestOrigin");
    var moved = controller.Tiles[0]; moved.LocalPosition += Vector3.up * 2; moved.Apply();
    controller.EvaluateTiles(0.3f); renderers[0].GetPropertyBlock(block, 0);
    check(block.GetVector("_RevealRestOrigin") == rest, "Tile animation cannot move the authored paint coordinates");
    controller.RestoreTiles();
    check(renderers[0].sharedMaterials[0] == source && renderers[0].sharedMaterials[1] == source,
        "Stopping the effect restores every original material slot");
    renderers[0].GetPropertyBlock(block);
    check(block.GetColor("_BaseColor") == Color.blue && block.GetFloat("_RevealSentinel") == 42,
        "Restoration preserves the renderer property block");
    renderers[0].GetPropertyBlock(block, 1);
    check(block.GetColor("_BaseColor") == Color.red && block.GetFloat("_RevealSentinel") == 17,
        "Restoration preserves material-slot property blocks");
    controller.EvaluateTiles(0.4f); reveal.enabled = false;
    check(renderers[0].sharedMaterial == source, "Disabling the modifier immediately restores materials");
    reveal.enabled = true; reveal.modifierWeight = 0; controller.EvaluateTiles(0.5f);
    check(renderers[0].sharedMaterial == source, "Zero modifier weight is neutral");
    reveal.modifierWeight = 1; controller.EvaluateTiles(0.6f);
    controller.TileGroups[0].tiles[0].included = false; controller.EvaluateTiles(0.7f);
    check(renderers[0].sharedMaterial == source, "Excluded tiles restore their original material");
    controller.TileGroups[0].tiles[0].included = true; controller.EvaluateTiles(0.8f);
    check(renderers[0].sharedMaterial == effect, "Re-inclusion binds the original substrate");
    reveal.automaticReveal = true; reveal.loopMode = MudaacTilePaintRevealModifier.LoopMode.PaintAndErase;
    reveal.revealDuration = 0.4f; reveal.paintedHoldTime = 0.2f;
    reveal.eraseDuration = 0.4f; reveal.emptyHoldTime = 0.2f;
    reveal.RestartReveal(); controller.EvaluateTiles(0);
    for (int i = 1; i <= 5; i++) controller.EvaluateTiles(i * 0.1f);
    check(reveal.RevealProgress == 1 && reveal.EraseProgress == 0, "Automatic playback holds the completed paint");
    for (int i = 6; i <= 8; i++) controller.EvaluateTiles(i * 0.1f);
    check(reveal.RevealProgress == 1 && reveal.EraseProgress > 0.3f && reveal.EraseProgress < 0.7f,
        "Paint And Erase wipes across the fully painted surface");
    reveal.paused = true; float before = reveal.EraseProgress;
    controller.EvaluateTiles(2);
    check(reveal.EraseProgress == before, "Pause freezes automatic playback");
    reveal.paused = false;
    for (int i = 21; i <= 23; i++) controller.EvaluateTiles(i * 0.1f);
    check(reveal.EraseProgress == 1 && renderers[0].sharedMaterial == source,
        "The empty hold restores original materials after erasing");
    reveal.loopMode = MudaacTilePaintRevealModifier.LoopMode.Once; reveal.RestartReveal();
    controller.EvaluateTiles(0);
    for (int i = 1; i <= 12; i++) controller.EvaluateTiles(i * 0.1f);
    check(reveal.RevealProgress == 1 && reveal.EraseProgress == 0,
        "Once remains fully painted without looping");
    reveal.loopMode = MudaacTilePaintRevealModifier.LoopMode.Restart; reveal.RestartReveal();
    controller.EvaluateTiles(0);
    for (int i = 1; i <= 7; i++) controller.EvaluateTiles(i * 0.1f);
    check(reveal.RevealProgress == 0 && renderers[0].sharedMaterial == source,
        "Restart mode has a clean empty hold between cycles");
    return "Passed " + passed + " paint reveal checks.\n" + report;
}
finally
{
    UnityEngine.Object.DestroyImmediate(root);
    if (source != null) UnityEngine.Object.DestroyImmediate(source);
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
