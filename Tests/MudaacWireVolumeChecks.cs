// Execute this method body through Unity MCP execute_code in Edit Mode.
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var root = new GameObject("Wire volume checks");
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
    panel.name = "Concrete panel wire checks";
    panel.transform.SetParent(root.transform, false);
    panel.transform.localScale = new Vector3(.02f, .03f, .01f);
    panel.transform.localRotation = Quaternion.Euler(90, 0, 0);
    var renderer = panel.GetComponent<Renderer>();
    source = new Material(Shader.Find("HDRP/Lit"));
    renderer.sharedMaterials = new[] { source, source };
    var block = new MaterialPropertyBlock();
    block.SetColor("_BaseColor", Color.blue); block.SetFloat("_WireSentinel", 42);
    renderer.SetPropertyBlock(block);
    block.SetColor("_BaseColor", Color.red); block.SetFloat("_WireSentinel", 17);
    renderer.SetPropertyBlock(block, 1);
    var controller = root.AddComponent<MudaacTileController>();
    var wire = root.AddComponent<MudaacTileWireVolumeModifier>();
    wire.wireMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacWireVolume.mat");
    check(wire.wireMaterial != null && wire.wireMaterial.shader.isSupported
        && !ShaderUtil.ShaderHasError(wire.wireMaterial.shader), "HDRP wire material imports and is supported");
    check(wire.wireMaterial.renderQueue == 3000 && wire.wireMaterial.GetFloat("_ZWrite") == 0,
        "The effect uses transparency without an opaque depth write");
    controller.EvaluateTiles(1);
    check(renderer.sharedMaterials[0] == wire.wireMaterial && renderer.sharedMaterials[1] == wire.wireMaterial,
        "One shared material replaces every source slot");
    renderer.GetPropertyBlock(block, 0);
    check(block.GetColor("_BaseColor") == Color.blue, "Renderer colour overrides are retained");
    check(Vector3.Distance((Vector3)block.GetVector("_WireHalfSize"), renderer.localBounds.extents) < .0001f,
        "Source bounds are available for final-transform volume rendering");
    check(Vector3.Distance(renderer.bounds.size, new Vector3(2, 1, 3)) < .0001f,
        "The effect preserves imported scale and rotation");
    renderer.GetPropertyBlock(block, 1);
    check(block.GetColor("_BaseColor") == Color.red, "Material-slot colours remain independent");
    wire.modifierWeight = .4f; controller.EvaluateTiles(2); renderer.GetPropertyBlock(block, 0);
    check(Mathf.Abs(block.GetVector("_WireSurface").w - .4f) < .0001f, "Modifier weight reaches the per-tile effect");
    wire.modifierWeight = 1;
    var sphereObject = new GameObject("Wire influencer"); sphereObject.transform.SetParent(root.transform, false);
    sphereObject.transform.localScale = Vector3.one * .01f;
    var sphere = sphereObject.AddComponent<MudaacTileSphereInfluencer>();
    wire.influencers.Add(sphere);
    sphereObject.transform.position = Vector3.right * 1000; controller.EvaluateTiles(2);
    check(renderer.sharedMaterial == source, "Tiles outside the influencer retain their original material");
    sphereObject.transform.position = controller.Tiles[0].RestWorldCenter; controller.EvaluateTiles(2);
    check(renderer.sharedMaterial == wire.wireMaterial, "An influencer can reveal the wire effect");
    wire.influencers.Clear();
    wire.modifierWeight = 0; controller.EvaluateTiles(3);
    check(renderer.sharedMaterial == source, "Zero modifier weight restores the original material");
    renderer.GetPropertyBlock(block, 1);
    check(block.GetFloat("_WireSentinel") == 17 && block.GetColor("_BaseColor") == Color.red,
        "Zero weight restores the original slot block");
    wire.modifierWeight = 1; wire.enabled = false; controller.EvaluateTiles(4);
    check(renderer.sharedMaterial == source, "A disabled modifier leaves source materials intact");
    wire.enabled = true; controller.EvaluateTiles(5);
    controller.TileGroups[0].tiles[0].included = false; controller.EvaluateTiles(6);
    check(renderer.sharedMaterial == source, "Excluding a tile restores its materials");
    controller.TileGroups[0].tiles[0].included = true; controller.EvaluateTiles(7);
    check(renderer.sharedMaterial == wire.wireMaterial, "Re-inclusion reapplies the effect");
    controller.RefreshTiles();
    check(renderer.sharedMaterial == source, "Refreshing restores material references before binding");
    controller.EvaluateTiles(8); controller.RestoreTiles();
    renderer.GetPropertyBlock(block);
    check(block.GetFloat("_WireSentinel") == 42 && block.GetColor("_BaseColor") == Color.blue,
        "Stopping restores the renderer property block");
    renderer.GetPropertyBlock(block, 1);
    check(block.GetFloat("_WireSentinel") == 17, "Stopping restores per-slot blocks");
    wire.wireMaterial = source; controller.EvaluateTiles(9);
    check(renderer.sharedMaterial == source, "An incompatible effect material is ignored");
    wire.wireMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacWireVolume.mat");
    var shared = new GameObject("Shared Wire Volume"); shared.transform.SetParent(root.transform, false);
    var globalWire = shared.AddComponent<MudaacTileWireVolumeModifier>();
    globalWire.wireMaterial = wire.wireMaterial;
    wire.enabled = false; controller.globalModifiers = shared; controller.EvaluateTiles(9);
    check(renderer.sharedMaterial == globalWire.wireMaterial, "Global Modifiers can supply the stateless wire effect");
    controller.globalModifiers = null; controller.EvaluateTiles(9);
    check(renderer.sharedMaterial == source, "Removing Global Modifiers restores original materials");
    wire.enabled = true;
    controller.EvaluateTiles(10); controller.enabled = false;
    check(renderer.sharedMaterial == source, "Disabling the controller restores materials immediately");
    return "Passed " + passed + " wire volume integration checks.\n" + report;
}
finally
{
    UnityEngine.Object.DestroyImmediate(root);
    if (source != null) UnityEngine.Object.DestroyImmediate(source);
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
