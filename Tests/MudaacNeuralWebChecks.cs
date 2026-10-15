// Execute this method body through Unity MCP execute_code in Edit Mode.
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var root = new GameObject("Neural web integration checks");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
Material source = null;
int passed = 0;
var report = new System.Text.StringBuilder();
System.Action<bool, string> check = (condition, name) => {
    if (!condition) throw new System.Exception("FAIL: " + name + "\n" + report);
    passed++; report.AppendLine("PASS: " + name);
};
try
{
    var effect = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacNeuralWeb.mat");
    check(effect != null && effect.HasProperty("_WebShape") && !ShaderUtil.ShaderHasError(effect.shader),
        "The HDRP material and shader import successfully");
    source = new Material(Shader.Find("HDRP/Lit"));
    var renderers = new System.Collections.Generic.List<Renderer>();
    for (int i = 0; i < 2; i++)
    {
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Concrete panel neural " + i;
        panel.transform.SetParent(root.transform, false);
        panel.transform.localPosition = new Vector3(i * 4 - 2, 0, 0);
        panel.transform.localScale = new Vector3(4, 4, .1f);
        var renderer = panel.GetComponent<Renderer>();
        renderer.sharedMaterials = new[] { source, source };
        renderers.Add(renderer);
    }
    var block = new MaterialPropertyBlock();
    block.SetColor("_BaseColor", Color.blue); block.SetFloat("_Sentinel", 42);
    renderers[0].SetPropertyBlock(block);
    block.SetColor("_BaseColor", Color.red); block.SetFloat("_Sentinel", 17);
    renderers[0].SetPropertyBlock(block, 1);
    var controller = root.AddComponent<MudaacTileController>();
    var web = root.AddComponent<MudaacTileNeuralWebModifier>();
    check(web.webMaterial == effect, "Adding the component assigns the ready-to-use material");
    controller.EvaluateTiles(0);
    check(renderers[0].sharedMaterials[0] == effect && renderers[0].sharedMaterials[1] == effect,
        "All material slots use one shared effect material");
    renderers[0].GetPropertyBlock(block, 0);
    check(block.GetColor("_BaseColor") == Color.blue, "Renderer overrides remain the substrate colour");
    renderers[0].GetPropertyBlock(block, 1);
    check(block.GetColor("_BaseColor") == Color.red, "Per-slot substrate overrides remain independent");
    var originalPose = controller.Tiles[0].RestLocalPosition;
    var originalProjection = block.GetVector("_WebRestOrigin");
    var tile = controller.Tiles[0]; tile.LocalPosition += Vector3.up * 2; tile.Apply();
    controller.EvaluateTiles(.1f); renderers[0].GetPropertyBlock(block, 0);
    check(block.GetVector("_WebRestOrigin") == originalProjection,
        "Tile motion does not alter authored pattern coordinates");
    check(tile.Transform.localPosition == originalPose, "Evaluation preserves the authored tile layout");
    check(web.AnimationTime > 0, "Animation advances with the controller clock");
    web.paused = true; float before = web.AnimationTime; controller.EvaluateTiles(10);
    check(web.AnimationTime == before, "Pause freezes animation");
    web.paused = false; web.animationSpeed = 0; controller.EvaluateTiles(10.1f);
    check(web.AnimationTime == before, "Zero animation speed freezes animation");
    web.animationSpeed = 1; controller.EvaluateTiles(10.2f);
    check(web.AnimationTime > before && web.AnimationTime < before + .11f, "Resume does not jump across paused time");
    web.RestartAnimation(); controller.EvaluateTiles(11);
    check(web.AnimationTime == 0, "Restart returns to the deterministic initial pattern");
    web.modifierWeight = .4f; controller.EvaluateTiles(11.1f); renderers[0].GetPropertyBlock(block, 0);
    check(Mathf.Abs(block.GetVector("_WebLight").w - .4f) < .001f, "Modifier Weight blends the whole tile");
    web.modifierWeight = 0; controller.EvaluateTiles(11.2f);
    check(renderers[0].sharedMaterial == source, "Zero weight restores the original material");
    web.modifierWeight = 1;
    var influenceObject = new GameObject("Sphere influence"); influenceObject.transform.SetParent(root.transform, false);
    influenceObject.transform.position = controller.Tiles[0].RestWorldCenter;
    var sphere = influenceObject.AddComponent<MudaacTileSphereInfluencer>(); sphere.radius = 1;
    web.influencers.Add(sphere); controller.EvaluateTiles(12);
    check(renderers[0].sharedMaterial == effect && renderers[1].sharedMaterial == source,
        "A sphere influencer affects whole tiles by their authored centres");
    web.influencers.Clear(); controller.EvaluateTiles(12.1f);
    controller.TileGroups[0].tiles[0].included = false; controller.EvaluateTiles(12.2f);
    check(renderers[0].sharedMaterial == source, "Excluding a tile restores its original material");
    controller.TileGroups[0].tiles[0].included = true; controller.EvaluateTiles(12.3f);
    check(renderers[0].sharedMaterial == effect, "Re-inclusion safely reapplies the effect");
    controller.RestoreTiles();
    check(renderers[0].sharedMaterials[0] == source && renderers[0].sharedMaterials[1] == source,
        "Stopping preview restores every original material slot");
    renderers[0].GetPropertyBlock(block);
    check(block.GetColor("_BaseColor") == Color.blue && block.GetFloat("_Sentinel") == 42,
        "Restoration preserves the renderer property block");
    renderers[0].GetPropertyBlock(block, 1);
    check(block.GetColor("_BaseColor") == Color.red && block.GetFloat("_Sentinel") == 17,
        "Restoration preserves the slot property block");
    var globalObject = new GameObject("Global web"); globalObject.transform.SetParent(root.transform, false);
    web.enabled = false;
    var globalWeb = globalObject.AddComponent<MudaacTileNeuralWebModifier>(); globalWeb.webMaterial = effect;
    controller.globalModifiers = globalObject; controller.EvaluateTiles(13);
    check(renderers[0].sharedMaterial == effect, "The effect also works through Global Modifiers");
    globalWeb.enabled = false; controller.EvaluateTiles(13.1f);
    check(renderers[0].sharedMaterial == source, "Disabling the effect restores materials on evaluation");
    globalWeb.enabled = true; globalWeb.webMaterial = source; controller.EvaluateTiles(13.2f);
    check(renderers[0].sharedMaterial == source, "An incompatible material is ignored safely");
    return "Passed " + passed + " neural web integration checks.\n" + report;
}
finally
{
    UnityEngine.Object.DestroyImmediate(root);
    if (source != null) UnityEngine.Object.DestroyImmediate(source);
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
