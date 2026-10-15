// Execute this method body through Unity MCP execute_code in Edit Mode.
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var root = new GameObject("Organic filaments checks");
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
    root.transform.localScale = Vector3.one * 100;
    var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
    panel.name = "Concrete panel filaments checks";
    panel.transform.SetParent(root.transform, false);
    panel.transform.localScale = new Vector3(.02f, .03f, .01f);
    panel.transform.localRotation = Quaternion.Euler(90, 0, 0);
    var renderer = panel.GetComponent<Renderer>();
    source = new Material(Shader.Find("HDRP/Lit"));
    renderer.sharedMaterials = new[] { source, source };
    var block = new MaterialPropertyBlock();
    block.SetColor("_BaseColor", Color.blue); block.SetFloat("_OrganicSentinel", 42);
    renderer.SetPropertyBlock(block);
    block.SetColor("_BaseColor", Color.red); block.SetFloat("_OrganicSentinel", 17);
    renderer.SetPropertyBlock(block, 1);
    var controller = root.AddComponent<MudaacTileController>();
    var filaments = root.AddComponent<MudaacTileOrganicFilamentsModifier>();
    filaments.filamentMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacOrganicFilaments.mat");
    check(filaments.filamentMaterial != null && filaments.filamentMaterial.shader.isSupported
        && !ShaderUtil.ShaderHasError(filaments.filamentMaterial.shader), "HDRP filaments material imports and is supported");
    check(filaments.filamentMaterial.renderQueue < 2500, "The dark substrate is opaque and keeps normal depth occlusion");
    controller.EvaluateTiles(1);
    check(renderer.sharedMaterials[0] == filaments.filamentMaterial && renderer.sharedMaterials[1] == filaments.filamentMaterial,
        "One shared material replaces every source slot");
    renderer.GetPropertyBlock(block, 0);
    check(block.GetColor("_BaseColor") == Color.blue, "Renderer colour overrides are retained");
    Vector3 restWorld = (Vector3)block.GetVector("_OrganicRestOrigin")
        + (Vector3)block.GetVector("_OrganicRestX") * controller.Tiles[0].LocalCenter.x
        + (Vector3)block.GetVector("_OrganicRestY") * controller.Tiles[0].LocalCenter.y
        + (Vector3)block.GetVector("_OrganicRestZ") * controller.Tiles[0].LocalCenter.z;
    check(Vector3.Distance(restWorld, controller.Tiles[0].RestWorldCenter) < .0001f,
        "Authored coordinates preserve the mesh centre at imported scale and rotation");
    check(Vector3.Distance(renderer.bounds.size, new Vector3(2, 1, 3)) < .0001f,
        "The effect preserves imported scale and rotation");
    renderer.GetPropertyBlock(block, 1);
    check(block.GetColor("_BaseColor") == Color.red, "Material-slot colours remain independent");
    filaments.modifierWeight = .4f; controller.EvaluateTiles(2); renderer.GetPropertyBlock(block, 0);
    check(Mathf.Abs(block.GetVector("_OrganicSurface").x - .4f) < .0001f, "Modifier weight reaches the per-tile effect");
    filaments.modifierWeight = 1;
    filaments.RestartAnimation(); controller.EvaluateTiles(10); controller.EvaluateTiles(10.1f);
    renderer.GetPropertyBlock(block, 0); float running = block.GetVector("_OrganicMotion").x;
    check(running > 0, "Animation advances with the controller clock");
    filaments.paused = true; controller.EvaluateTiles(11); renderer.GetPropertyBlock(block, 0);
    check(block.GetVector("_OrganicMotion").x == running, "Pause freezes the animation clock");
    filaments.paused = false; filaments.speed = 0; controller.EvaluateTiles(12); renderer.GetPropertyBlock(block, 0);
    check(block.GetVector("_OrganicMotion").x == running, "Zero speed freezes the animation clock");
    filaments.speed = 1; filaments.RestartAnimation(); controller.EvaluateTiles(13); renderer.GetPropertyBlock(block, 0);
    check(block.GetVector("_OrganicMotion").x == 0, "Restart returns to the deterministic initial pattern");
    var sphereObject = new GameObject("Organic influencer"); sphereObject.transform.SetParent(root.transform, false);
    sphereObject.transform.localScale = Vector3.one * .01f;
    var sphere = sphereObject.AddComponent<MudaacTileSphereInfluencer>();
    filaments.influencers.Add(sphere);
    sphereObject.transform.position = Vector3.right * 1000; controller.EvaluateTiles(2);
    check(renderer.sharedMaterial == source, "Tiles outside the influencer retain their original material");
    sphereObject.transform.position = controller.Tiles[0].RestWorldCenter; controller.EvaluateTiles(2);
    check(renderer.sharedMaterial == filaments.filamentMaterial, "An influencer can reveal the filaments effect");
    filaments.influencers.Clear();
    filaments.modifierWeight = 0; controller.EvaluateTiles(3);
    check(renderer.sharedMaterial == source, "Zero modifier weight restores the original material");
    renderer.GetPropertyBlock(block, 1);
    check(block.GetFloat("_OrganicSentinel") == 17 && block.GetColor("_BaseColor") == Color.red,
        "Zero weight restores the original slot block");
    filaments.modifierWeight = 1; filaments.enabled = false; controller.EvaluateTiles(4);
    check(renderer.sharedMaterial == source, "A disabled modifier leaves source materials intact");
    filaments.enabled = true; controller.EvaluateTiles(5);
    controller.TileGroups[0].tiles[0].included = false; controller.EvaluateTiles(6);
    check(renderer.sharedMaterial == source, "Excluding a tile restores its materials");
    controller.TileGroups[0].tiles[0].included = true; controller.EvaluateTiles(7);
    check(renderer.sharedMaterial == filaments.filamentMaterial, "Re-inclusion reapplies the effect");
    controller.RefreshTiles();
    check(renderer.sharedMaterial == source, "Refreshing restores material references before binding");
    controller.EvaluateTiles(8); controller.RestoreTiles();
    renderer.GetPropertyBlock(block);
    check(block.GetFloat("_OrganicSentinel") == 42 && block.GetColor("_BaseColor") == Color.blue,
        "Stopping restores the renderer property block");
    renderer.GetPropertyBlock(block, 1);
    check(block.GetFloat("_OrganicSentinel") == 17, "Stopping restores per-slot blocks");
    filaments.filamentMaterial = source; controller.EvaluateTiles(9);
    check(renderer.sharedMaterial == source, "An incompatible effect material is ignored");
    filaments.filamentMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacOrganicFilaments.mat");
    var shared = new GameObject("Shared Organic Filaments"); shared.transform.SetParent(root.transform, false);
    var globalFilaments = shared.AddComponent<MudaacTileOrganicFilamentsModifier>();
    globalFilaments.filamentMaterial = filaments.filamentMaterial;
    filaments.enabled = false; controller.globalModifiers = shared; controller.EvaluateTiles(9);
    check(renderer.sharedMaterial == globalFilaments.filamentMaterial, "Global Modifiers can supply the shared filaments effect");
    controller.globalModifiers = null; controller.EvaluateTiles(9);
    check(renderer.sharedMaterial == source, "Removing Global Modifiers restores original materials");
    filaments.enabled = true;
    controller.EvaluateTiles(10); controller.enabled = false;
    check(renderer.sharedMaterial == source, "Disabling the controller restores materials immediately");
    return "Passed " + passed + " organic filaments integration checks.\n" + report;
}
finally
{
    UnityEngine.Object.DestroyImmediate(root);
    if (source != null) UnityEngine.Object.DestroyImmediate(source);
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
