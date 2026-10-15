// Execute through Unity MCP execute_code in Edit Mode. Unequal tiles form a T junction.
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var root = new GameObject("Liquid seam rendering checks");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
var previous = RenderTexture.active;
RenderTexture rt = null;
Texture2D readback = null;
Material source = null;
var report = new System.Text.StringBuilder();
int passed = 0;
System.Action<bool, string> check = (condition, name) => {
    if (!condition) throw new System.Exception("FAIL: " + name + "\n" + report);
    passed++; report.AppendLine("PASS: " + name);
};
try
{
    source = new Material(Shader.Find("HDRP/Lit")); source.SetColor("_BaseColor", Color.white);
    UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(source);
    for (int i = 0; i < 3; i++)
    {
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Concrete panel render seams " + i;
        panel.transform.SetParent(root.transform, false);
        panel.transform.localPosition = i == 2 ? new Vector3(1, 0, 0) : new Vector3(-1, i * 2 - 1, 0);
        panel.transform.localScale = new Vector3(2, i == 2 ? 4 : 2, 0.1f);
        panel.GetComponent<Renderer>().sharedMaterial = source;
    }
    var controller = root.AddComponent<MudaacTileController>();
    var seams = root.AddComponent<MudaacTileLiquidSeamsModifier>();
    seams.seamMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacLiquidSeams.mat");
    seams.emission = 4; seams.haloStrength = 0; seams.flowAmount = 0; seams.edgeRoughness = 0;
    seams.interiorCellCount = 0;
    seams.seamWidth = 0.025f; seams.junctionRadius = 0.6f;
    seams.paused = true;
    var lightObject = new GameObject("Seam render light"); lightObject.transform.SetParent(root.transform, false);
    lightObject.transform.rotation = Quaternion.Euler(15, -15, 0);
    var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 10000;
    lightObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
    var cameraObject = new GameObject("Seam render camera"); cameraObject.transform.SetParent(root.transform, false);
    var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
    camera.transform.position = new Vector3(0, 0, -10); camera.transform.LookAt(Vector3.zero);
    camera.orthographic = true; camera.orthographicSize = 2.5f; camera.aspect = 1;
    camera.nearClipPlane = 0.1f; camera.farClipPlane = 30;
    camera.overrideSceneCullingMask = UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(scene);
    var cameraData = cameraObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
    cameraData.clearColorMode = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.ClearColorMode.Color;
    cameraData.backgroundColorHDR = Color.black; cameraData.volumeLayerMask = 0; cameraData.customRenderingSettings = true;
    var frameField = typeof(UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData).GetField(
        "m_RenderingPathCustomFrameSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var frame = (UnityEngine.Rendering.HighDefinition.FrameSettings)frameField.GetValue(cameraData);
    foreach (var field in new[] { UnityEngine.Rendering.HighDefinition.FrameSettingsField.Postprocess,
        UnityEngine.Rendering.HighDefinition.FrameSettingsField.SSAO,
        UnityEngine.Rendering.HighDefinition.FrameSettingsField.ExposureControl })
    {
        frame.SetEnabled(field, false);
        cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
    }
    frameField.SetValue(cameraData, frame);
    rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear); rt.Create();
    readback = new Texture2D(256, 256, TextureFormat.RGBAFloat, false, true);
    var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest(); request.destination = rt;
    System.Func<Color[]> render = () => {
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
        RenderTexture.active = rt; readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); readback.Apply();
        return readback.GetPixels();
    };
    System.Func<Color[], float, float, Color> at = (pixels, x, y) => pixels[
        Mathf.Clamp(Mathf.RoundToInt((y + 2.5f) / 5 * 255), 0, 255) * 256
        + Mathf.Clamp(Mathf.RoundToInt((x + 2.5f) / 5 * 255), 0, 255)];
    System.Func<Color[], Color[], int> changes = (a, b) => {
        int count = 0;
        for (int i = 0; i < a.Length; i++)
            if (Mathf.Abs(a[i].r-b[i].r) + Mathf.Abs(a[i].g-b[i].g) + Mathf.Abs(a[i].b-b[i].b) > 0.02f) count++;
        return count;
    };
    System.Action<string> save = name => {
        var pixels = readback.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            pixels[i] = new Color(c.r/(1+c.r), c.g/(1+c.g), c.b/(1+c.b), 1).gamma;
        }
        var preview = new Texture2D(256, 256, TextureFormat.RGBA32, false, true);
        try {
            preview.SetPixels(pixels); preview.Apply();
            string folder = System.IO.Path.Combine(Application.dataPath, "../Temp/liquid-seam-renders");
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), preview.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(preview); }
    };
    controller.EvaluateTiles(0); var pooled = render(); save("00-junction-pools");
    check(at(pooled, 0, 1).maxColorComponent > 1, "Shared tile seam emits bright liquid");
    check(at(pooled, 1, 1).maxColorComponent < 0.05f, "Tile interiors stay dark");
    check(at(pooled, 0.05f, 0.05f).maxColorComponent > 0.5f, "T-junction pools extend smoothly onto the middle of the larger tile edge");
    check(Mathf.Abs(at(pooled, -0.01f, 1).r - at(pooled, 0.01f, 1).r) < 0.1f,
        "The two sides of a shared seam have matching brightness");
    seams.junctionRadius = 0; controller.EvaluateTiles(0.1f); var square = render();
    check(at(square, 0.05f, 0.05f).maxColorComponent < 0.05f, "Zero radius removes junction pools");
    check(at(square, 0.08f, 1).maxColorComponent < 0.05f, "Fine seams do not fill tile interiors");
    seams.seamWidth = 0.15f; controller.EvaluateTiles(0.2f); var wide = render();
    check(at(wide, 0.08f, 1).maxColorComponent > 1, "Increasing width widens the actual seam");
    seams.liquidColor = Color.red; controller.EvaluateTiles(0.3f); var red = render();
    check(at(red, 0, 1).r > at(red, 0, 1).g + 1, "Liquid colour controls the rendered emission");
    seams.liquidColor = Color.white; seams.seamWidth = 0.025f; seams.junctionRadius = 0.6f;
    seams.flowAmount = 0.2f; seams.edgeRoughness = 0.006f;
    controller.EvaluateTiles(0.4f); var first = render(); save("01-flow-start");
    seams.paused = false; seams.flowSpeed = 1;
    for (int i = 1; i <= 50; i++) controller.EvaluateTiles(0.4f + i * 0.1f);
    var moving = render(); save("02-flow-later");
    check(changes(first, moving) > 100, "Liquid bulges and junction pools animate along the seams");
    seams.paused = true; controller.EvaluateTiles(20); var frozen = render();
    check(changes(moving, frozen) == 0, "Paused flow is visually stable");
    seams.seed += 51; controller.EvaluateTiles(20.1f); var reseeded = render();
    check(changes(frozen, reseeded) > 100, "Seed changes the liquid pattern");
    seams.haloWidth = 0.12f; seams.haloStrength = 0.5f; controller.EvaluateTiles(20.2f); var halo = render();
    check(changes(reseeded, halo) > 100, "Halo controls a soft glow around the liquid");
    seams.interiorBlend = 0; controller.EvaluateTiles(20.3f); var substrate = render();
    check(at(substrate, 1, 1).maxColorComponent > at(halo, 1, 1).maxColorComponent + 0.1f,
        "Original-colour mode keeps the tile substrate visible");
    seams.modifierWeight = 0; controller.EvaluateTiles(20.4f); var original = render();
    check(root.GetComponentsInChildren<Renderer>()[0].sharedMaterial == source,
        "Zero weight restores the original rendering material");
    seams.modifierWeight = 1; seams.interiorBlend = 1;

    seams.seamWidth = 0; seams.flowAmount = 0; seams.junctionRadius = 0;
    seams.edgeRoughness = 0.15f; seams.haloWidth = 0.05f; seams.haloStrength = 0.8f;
    controller.EvaluateTiles(20.45f); var vanished = render(); save("03-zero-width");
    check(vanished.All(c => c.maxColorComponent < 0.05f),
        "Zero width removes all thin seams, even with roughness and halo enabled");
    seams.junctionRadius = 0.25f; controller.EvaluateTiles(20.46f); var poolsOnly = render(); save("04-pools-only");
    check(at(poolsOnly, 0, 1).maxColorComponent < 0.05f,
        "Zero-width straight edges have no antialiased or emissive ghosts");
    check(at(poolsOnly, 0.03f, 0.03f).maxColorComponent > 0.5f,
        "Thick junction pools remain when the thin edges disappear");
    check(Enumerable.Range(1, 20).All(i => {
        Color a = at(poolsOnly, -0.01f, i * 0.015f), b = at(poolsOnly, 0.01f, i * 0.015f);
        return Mathf.Abs(a.r - b.r) / (1 + Mathf.Max(a.r, b.r)) < 0.1f;
    }), "Thick parts have no brightness step across unequal tile boundaries");

    seams.junctionRadius = 0; seams.flowAmount = 0; seams.edgeRoughness = 0; seams.haloStrength = 0;
    seams.interiorCellCount = 4; seams.interiorLineWidth = 0.03f; seams.interiorCurvature = 0.12f;
    seams.seed = 7; seams.RestartFlow(); controller.EvaluateTiles(21);
    var inside = render(); save("05-interior-cells");
    int insideBright = 0;
    for (int i = 0; i < inside.Length; i++)
    {
        float x = ((i % 256) + 0.5f) / 256 * 5 - 2.5f;
        float y = ((i / 256) + 0.5f) / 256 * 5 - 2.5f;
        if (Mathf.Abs(x) > 0.3f && Mathf.Abs(x) < 1.7f && Mathf.Abs(y) > 0.3f
            && Mathf.Abs(y) < 1.7f && inside[i].maxColorComponent > 0.5f) insideBright++;
    }
    check(insideBright > 100, "Parametric lines appear well inside the original tiles");
    seams.interiorCellCount = 1; controller.EvaluateTiles(21.01f); var single = render();
    check(single.All(c => c.maxColorComponent < 0.05f), "One cell leaves each tile undivided");
    seams.interiorCellCount = 4; seams.interiorLineWidth = 0;
    controller.EvaluateTiles(21.02f); var zeroInside = render();
    check(zeroInside.All(c => c.maxColorComponent < 0.05f), "Zero interior width also removes thin internal lines completely");
    seams.interiorLineWidth = 0.03f; seams.interiorCellCount = 8;
    controller.EvaluateTiles(21.03f); var denser = render();
    check(changes(inside, denser) > 400, "Cell count changes the internal subdivision");
    seams.interiorCellCount = 4; seams.interiorCurvature = 0;
    controller.EvaluateTiles(21.04f); var straightInside = render();
    seams.interiorCurvature = 0.4f; controller.EvaluateTiles(21.05f); var curvedInside = render();
    check(changes(straightInside, curvedInside) > 100, "Curvature bends the internal lines");
    seams.junctionRadius = 0.2f; controller.EvaluateTiles(21.055f); var joinedCells = render(); save("07-shared-interior-junctions");
    // Sample identical world positions using each neighbor's shader data. Adjacent pixels can
    // legitimately differ sharply where a sloping liquid boundary crosses the tile boundary.
    var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
    probe.name = "Seam continuity probe"; probe.transform.SetParent(root.transform, false);
    probe.transform.localScale = new Vector3(4, 4, 0.1f);
    var probeRenderer = probe.GetComponent<Renderer>(); probeRenderer.sharedMaterial = seams.seamMaterial;
    foreach (var tile in controller.Tiles) tile.Renderer.enabled = false;
    try
    {
        var sampleBlock = new MaterialPropertyBlock();
        var probeMatrix = probe.transform.localToWorldMatrix;
        System.Action<int> useNeighbor = index => {
            controller.Tiles[index].Renderer.GetPropertyBlock(sampleBlock, 0);
            sampleBlock.SetVector("_SeamRestX", probeMatrix.GetColumn(0));
            sampleBlock.SetVector("_SeamRestY", probeMatrix.GetColumn(1));
            sampleBlock.SetVector("_SeamRestZ", probeMatrix.GetColumn(2));
            sampleBlock.SetVector("_SeamRestOrigin", probeMatrix.GetColumn(3));
            probeRenderer.SetPropertyBlock(sampleBlock, 0);
        };
        useNeighbor(0); var fromLeft = render(); useNeighbor(2); var fromRight = render();
        check(Enumerable.Range(0, 50).All(i => {
            float y = -1.5f + i * 0.02f;
            return Enumerable.Range(0, 10).All(j => {
                float x = -0.1f + j * 0.02f;
                Color a = at(fromLeft, x, y), b = at(fromRight, x, y);
                return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.02f;
            });
        }), "Internal junctions render identically from both neighboring tiles' shader data");
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(probe);
        foreach (var tile in controller.Tiles) tile.Renderer.enabled = true;
    }
    seams.junctionRadius = 0;
    seams.interiorCurvature = 0; seams.interiorMotion = 0; seams.paused = false;
    controller.EvaluateTiles(21.06f); var stillCells = render();
    for (int i = 1; i <= 20; i++) controller.EvaluateTiles(21.06f + i * 0.1f);
    var stillLater = render();
    check(changes(stillCells, stillLater) == 0, "Zero interior motion freezes the cell sites");
    seams.interiorMotion = 1;
    for (int i = 1; i <= 30; i++) controller.EvaluateTiles(23.06f + i * 0.1f);
    var movingCells = render(); save("06-moving-interior-cells");
    check(changes(stillLater, movingCells) > 100, "Internal cells move continuously with the flow clock");
    seams.paused = true; seams.interiorCellCount = 0;
    seams.seamWidth = 100; seams.junctionRadius = 100; seams.flowAmount = 100;
    seams.edgeRoughness = 100; seams.flowScale = 1000; seams.emission = 1000;
    seams.haloWidth = 0; controller.EvaluateTiles(20.5f); var extreme = render();
    check(extreme.All(c => !float.IsNaN(c.r) && !float.IsInfinity(c.r) && !float.IsNaN(c.g)
        && !float.IsInfinity(c.g) && !float.IsNaN(c.b) && !float.IsInfinity(c.b)), "Extreme controls produce finite pixels");
    seams.enabled = false; controller.EvaluateTiles(20.6f); var restored = render();
    check(changes(original, restored) == 0, "Disabling the effect restores the original image");
    return passed + " rendered checks passed\n" + report;
}
finally
{
    RenderTexture.active = previous;
    UnityEngine.Object.DestroyImmediate(root);
    if (source != null) UnityEngine.Object.DestroyImmediate(source);
    if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
    if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
