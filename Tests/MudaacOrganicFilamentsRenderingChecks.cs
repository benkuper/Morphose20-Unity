// Execute through Unity MCP execute_code in Edit Mode. Unequal tiles form a T junction.
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var root = new GameObject("Organic filaments rendering checks");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
var previous = RenderTexture.active;
RenderTexture rt = null;
Texture2D readback = null;
Material source = null;
UnityEngine.Rendering.VolumeProfile exposureProfile = null;
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
    var filaments = root.AddComponent<MudaacTileOrganicFilamentsModifier>();
    filaments.filamentMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacOrganicFilaments.mat");
    filaments.paused = true; filaments.tileInfluence = 0;
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
    rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear); rt.Create();
    readback = new Texture2D(512, 512, TextureFormat.RGBAFloat, false, true);
    var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest(); request.destination = rt;
    System.Func<Color[]> render = () => {
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
        RenderTexture.active = rt; readback.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); readback.Apply();
        return readback.GetPixels();
    };
    System.Func<Color[], float, float, Color> at = (pixels, x, y) => pixels[
        Mathf.Clamp(Mathf.RoundToInt((y + 2.5f) / 5 * 511), 0, 511) * 512
        + Mathf.Clamp(Mathf.RoundToInt((x + 2.5f) / 5 * 511), 0, 511)];
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
            pixels[i] = new Color(c.r, c.g, c.b, 1).gamma;
        }
        var preview = new Texture2D(512, 512, TextureFormat.RGBA32, false, true);
        try {
            preview.SetPixels(pixels); preview.Apply();
            string folder = System.IO.Path.Combine(Application.dataPath, "../Temp/organic-filament-renders");
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), preview.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(preview); }
    };
    controller.EvaluateTiles(0); var first = render(); save("00-reference");
    var volumeObject = new GameObject("Organic production exposure"); volumeObject.transform.SetParent(root.transform, false);
    volumeObject.layer = 31;
    var volume = volumeObject.AddComponent<UnityEngine.Rendering.Volume>(); volume.isGlobal = true;
    exposureProfile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
    volume.sharedProfile = exposureProfile;
    var exposure = exposureProfile.Add<UnityEngine.Rendering.HighDefinition.Exposure>(true);
    exposure.mode.value = UnityEngine.Rendering.HighDefinition.ExposureMode.Fixed;
    exposure.fixedExposure.value = 11.1f;
    cameraData.volumeLayerMask = 1 << 31;
    frame.SetEnabled(UnityEngine.Rendering.HighDefinition.FrameSettingsField.ExposureControl, true);
    frameField.SetValue(cameraData, frame);
    var exposed = render();
    double firstEnergy = first.Sum(c => (double)c.r), exposedEnergy = exposed.Sum(c => (double)c.r);
    check(exposedEnergy > firstEnergy * .8 && exposedEnergy < firstEnergy * 1.2,
        "Production 11.1 EV exposure preserves the Unlit filaments' brightness");
    frame.SetEnabled(UnityEngine.Rendering.HighDefinition.FrameSettingsField.ExposureControl, false);
    frameField.SetValue(cameraData, frame); cameraData.volumeLayerMask = 0;
    int insideBright = 0, insideDark = 0;
    for (int i = 0; i < first.Length; i++)
    {
        float x = ((i % 512) + 0.5f) / 512 * 5 - 2.5f;
        float y = ((i / 512) + 0.5f) / 512 * 5 - 2.5f;
        if (Mathf.Abs(x) > .3f && Mathf.Abs(x) < 1.7f && Mathf.Abs(y) > .3f && Mathf.Abs(y) < 1.7f)
        {
            if (first[i].maxColorComponent > .2f) insideBright++;
            if (first[i].maxColorComponent < .04f) insideDark++;
        }
    }
    check(insideBright > 1000, "Filaments appear throughout tile interiors, away from all physical edges");
    check(insideDark > 1000, "Cell interiors retain the reference's dark negative space");
    // Render a common surface using each neighbor's data to test actual shader continuity.
    var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
    probe.name = "Organic continuity probe"; probe.transform.SetParent(root.transform, false);
    probe.transform.localScale = new Vector3(4, 4, .1f);
    var probeRenderer = probe.GetComponent<Renderer>(); probeRenderer.sharedMaterial = filaments.filamentMaterial;
    foreach (var tile in controller.Tiles) tile.Renderer.enabled = false;
    try
    {
        var sampleBlock = new MaterialPropertyBlock();
        var matrix = probe.transform.localToWorldMatrix;
        System.Action<int> useNeighbor = index => {
            controller.Tiles[index].Renderer.GetPropertyBlock(sampleBlock, 0);
            sampleBlock.SetVector("_OrganicRestX", matrix.GetColumn(0));
            sampleBlock.SetVector("_OrganicRestY", matrix.GetColumn(1));
            sampleBlock.SetVector("_OrganicRestZ", matrix.GetColumn(2));
            sampleBlock.SetVector("_OrganicRestOrigin", matrix.GetColumn(3));
            probeRenderer.SetPropertyBlock(sampleBlock, 0);
        };
        useNeighbor(0); var neighborA = render();
        useNeighbor(1); var neighborB = render();
        check(changes(neighborA, neighborB) == 0,
            "Zero Tile Influence produces identical pixels from either unequal neighbor's shader data");
        check(changes(first, neighborA) < 100,
            "A continuous field renders identically on one large surface or three smaller tiles");
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(probe);
        foreach (var tile in controller.Tiles) tile.Renderer.enabled = true;
    }
    filaments.paused = false; filaments.speed = 1;
    for (int i = 1; i <= 40; i++) controller.EvaluateTiles(i * .1f);
    var moving = render(); save("01-moving");
    check(changes(first, moving) > 10000, "Membranes, wisps and junctions animate");
    filaments.paused = true; controller.EvaluateTiles(20);
    check(changes(moving, render()) == 0, "Paused output is visually stable");
    filaments.RestartAnimation(); controller.EvaluateTiles(21);
    check(changes(first, render()) == 0, "Restart exactly reproduces the initial pattern");
    filaments.tileInfluence = 1; controller.EvaluateTiles(22); var influenced = render(); save("02-tile-influence");
    check(changes(first, influenced) > 10000, "Tile Influence varies the field across each whole tile");
    filaments.tileInfluence = 0; filaments.seed += 51; controller.EvaluateTiles(23); var reseeded = render();
    check(changes(first, reseeded) > 10000, "Seed changes the rendered organic pattern");
    filaments.seed = 7; filaments.cellSize = 1.7f; controller.EvaluateTiles(24); var large = render(); save("03-large-cells");
    check(changes(first, large) > 10000, "Cell Size changes the membrane spacing");
    filaments.cellSize = .85f; filaments.distortion = 0; controller.EvaluateTiles(25); var straight = render();
    check(changes(first, straight) > 10000, "Distortion curves the membranes and radial fibres");
    filaments.distortion = .65f; filaments.strandStrength = 0; filaments.mistStrength = 0;
    controller.EvaluateTiles(26); var clean = render(); save("04-membranes-only");
    check(changes(first, clean) > 1000, "Strands and mist add detail away from the membrane cores");
    filaments.membraneWidth = 0; filaments.glowStrength = 0; filaments.junctionStrength = 0;
    controller.EvaluateTiles(27); var empty = render();
    check(empty.All(c => c.maxColorComponent < .005f), "All detail can be independently removed without residual thin lines");
    filaments.strandStrength = .55f; filaments.mistStrength = .12f;
    filaments.membraneWidth = .006f; filaments.glowStrength = .35f; filaments.junctionStrength = 1.4f;
    filaments.filamentColor = Color.red; controller.EvaluateTiles(28); var red = render();
    check(red.Any(c => c.r > .5f) && red.All(c => c.g < .005f && c.b < .005f), "Filament Color tints the entire luminous field");
    filaments.filamentColor = Color.white; filaments.brightness = 0; filaments.backgroundBlend = 0;
    controller.EvaluateTiles(29); var substrate = render();
    check(substrate.Count(c => c.r > .9f && c.g > .9f && c.b > .9f) > 10000,
        "Original tile base colour can remain visible behind the effect");
    filaments.backgroundBlend = 1; filaments.brightness = 1.6f; filaments.modifierWeight = .5f;
    controller.EvaluateTiles(30); var blended = render();
    check(changes(first, blended) > 10000, "Modifier weight blends the source base colour and organic effect");
    filaments.modifierWeight = 0; controller.EvaluateTiles(31);
    check(controller.Tiles.All(tile => tile.Renderer.sharedMaterial == source), "Zero weight restores actual source materials");
    filaments.modifierWeight = 1; filaments.distortion = 2; filaments.cellSize = .02f;
    filaments.strandDensity = 150; filaments.strandStrength = 2; filaments.junctionStrength = 4;
    controller.EvaluateTiles(32); var extreme = render();
    check(extreme.All(c => !float.IsNaN(c.r) && !float.IsInfinity(c.r)), "Extreme supported controls produce finite pixels");

    // Exercise an actual imported bevelled panel, retaining its mesh orientation and scale.
    var production = GameObject.Find("Mudaac Model").GetComponent<MudaacTileController>();
    var productionTile = production.Tiles.First(tile => tile.Renderer.GetComponent<MeshFilter>() != null);
    foreach (var tile in controller.Tiles) tile.Renderer.enabled = false;
    var actualRoot = new GameObject("Imported filament checks"); actualRoot.transform.SetParent(root.transform, false);
    var actualPanel = new GameObject("Concrete panel actual filaments"); actualPanel.transform.SetParent(actualRoot.transform, false);
    actualPanel.AddComponent<MeshFilter>().sharedMesh = productionTile.Renderer.GetComponent<MeshFilter>().sharedMesh;
    var actualRenderer = actualPanel.AddComponent<MeshRenderer>(); actualRenderer.sharedMaterial = source;
    actualPanel.transform.rotation = productionTile.RestWorldRotation;
    actualPanel.transform.localScale = productionTile.Transform.lossyScale;
    actualPanel.transform.position = -actualPanel.transform.TransformVector(actualRenderer.localBounds.center);
    var actualController = actualRoot.AddComponent<MudaacTileController>();
    var actualFilaments = actualRoot.AddComponent<MudaacTileOrganicFilamentsModifier>();
    actualFilaments.filamentMaterial = filaments.filamentMaterial; actualFilaments.paused = true;
    actualController.EvaluateTiles(0);
    camera.orthographicSize = Mathf.Max(actualRenderer.bounds.extents.x, actualRenderer.bounds.extents.y) * 1.2f;
    var actual = render(); save("05-imported-panel");
    check(actual.Count(c => c.maxColorComponent > .2f) > 1000,
        "The effect renders inside an actual bevelled FBX panel at imported scale and orientation");
    check(!ShaderUtil.ShaderHasError(filaments.filamentMaterial.shader), "Rendered HDRP passes compile without shader errors");
    return "Passed " + passed + " organic filaments rendering checks.\n" + report;
}

finally
{
    RenderTexture.active = previous;
    UnityEngine.Object.DestroyImmediate(root);
    if (source != null) UnityEngine.Object.DestroyImmediate(source);
    if (exposureProfile != null) UnityEngine.Object.DestroyImmediate(exposureProfile);
    if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
    if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
