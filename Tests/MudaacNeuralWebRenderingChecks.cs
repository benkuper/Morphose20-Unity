// Execute through Unity MCP execute_code in Edit Mode. Uses an isolated HDRP preview scene.
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var root = new GameObject("Neural web render checks");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
var previous = RenderTexture.active;
RenderTexture rt = null;
Texture2D readback = null;
Material source = null;
UnityEngine.Rendering.VolumeProfile profile = null;
int passed = 0;
var report = new System.Text.StringBuilder();
System.Action<bool, string> check = (condition, name) => {
    if (!condition) throw new System.Exception("FAIL: " + name + "\n" + report);
    passed++; report.AppendLine("PASS: " + name);
};
try
{
    var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
    panel.name = "Concrete panel web rendering";
    panel.transform.SetParent(root.transform, false);
    panel.transform.localScale = new Vector3(8, 10, .1f);
    source = new Material(Shader.Find("HDRP/Lit"));
    source.SetColor("_BaseColor", Color.white);
    panel.GetComponent<Renderer>().sharedMaterial = source;
    var controller = root.AddComponent<MudaacTileController>();
    var web = root.AddComponent<MudaacTileNeuralWebModifier>();
    web.webMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacNeuralWeb.mat");
    web.animationSpeed = 1; web.paused = true;
    // Render requests can pump Editor updates. Keep preview active and the clock frozen during captures.
    controller.previewInEditMode = true;

    var cameraObject = new GameObject("Web check camera"); cameraObject.transform.SetParent(root.transform, false);
    var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
    camera.transform.position = new Vector3(0, 0, -10); camera.transform.LookAt(Vector3.zero);
    camera.orthographic = true; camera.orthographicSize = 5.2f; camera.aspect = 1;
    camera.nearClipPlane = .1f; camera.farClipPlane = 30;
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
    System.Func<Color[], Color[], int> changed = (a, b) => {
        int count = 0;
        for (int i = 0; i < a.Length; i++)
            if (Mathf.Abs(a[i].r-b[i].r) + Mathf.Abs(a[i].g-b[i].g) + Mathf.Abs(a[i].b-b[i].b) > .015f) count++;
        return count;
    };
    System.Func<Color[], double> energy = pixels => {
        double sum = 0; foreach (var p in pixels) sum += p.r + p.g + p.b; return sum;
    };
    string folder = System.IO.Path.GetFullPath("Temp/neural-web-renders"); System.IO.Directory.CreateDirectory(folder);
    System.Action<string> save = name => {
        var pixels = readback.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(pixels[i].r, pixels[i].g, pixels[i].b, 1).gamma;
        var preview = new Texture2D(512, 512, TextureFormat.RGBA32, false, true);
        try { preview.SetPixels(pixels); preview.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), preview.EncodeToPNG()); }
        finally { UnityEngine.Object.DestroyImmediate(preview); }
    };
    controller.EvaluateTiles(0); var reference = render(); save("00-reference");
    check(energy(reference) > 10000, "The shader renders luminous filaments");
    int dark = 0, bright = 0;
    for (int y = 12; y < 500; y++) for (int x = 62; x < 450; x++)
    {
        float value = reference[y * 512 + x].maxColorComponent;
        if (value < .05f) dark++; if (value > .3f) bright++;
    }
    check(dark > 20000 && bright > 2000, "Dark cell interiors contrast with bright strands and junctions");
    web.paused = false;
    for (int i = 1; i <= 30; i++) controller.EvaluateTiles(i * .1f);
    web.paused = true;
    var moving = render(); save("01-animated");
    check(changed(reference, moving) > 10000, "Animation visibly flows and deforms the web");
    web.paused = true; controller.EvaluateTiles(6);
    check(changed(moving, render()) == 0, "Pause freezes rendered pixels");
    web.RestartAnimation(); controller.EvaluateTiles(0);
    check(changed(reference, render()) == 0, "Restart reproduces the initial rendered pattern");
    web.seed = 91; controller.EvaluateTiles(0);
    check(changed(reference, render()) > 10000, "Seed changes the cellular network");
    web.seed = 7; web.layers = 1; controller.EvaluateTiles(0); var single = render(); save("02-single-layer");
    check(changed(reference, single) > 10000, "Nested fine layers add visible interior filaments");
    web.layers = 3; web.fineStrength = 0; controller.EvaluateTiles(0);
    check(changed(single, render()) == 0, "Zero fine strength removes all nested layers");
    web.fineStrength = .32f; web.strandWidth = .075f; controller.EvaluateTiles(0); var thick = render();
    check(energy(thick) > energy(reference) * 1.2, "Strand Width thickens the network");
    web.strandWidth = .025f; web.cellSize = .7f; controller.EvaluateTiles(0);
    check(changed(reference, render()) > 10000, "Cell Size changes the pattern density");
    web.cellSize = 1.4f; web.distortion = 0; controller.EvaluateTiles(0);
    check(changed(reference, render()) > 10000, "Distortion bends the cellular strands");
    web.distortion = .35f; web.junctionStrength = 0; controller.EvaluateTiles(0); var noNodes = render();
    check(energy(reference) > energy(noNodes) * 1.01, "Junction Strength adds bright connecting nodes");
    web.junctionStrength = 1.6f; web.glowStrength = 0; controller.EvaluateTiles(0); var noGlow = render();
    check(energy(reference) > energy(noGlow) * 1.05, "Glow Strength adds soft light around the strands");
    web.glowStrength = .32f; web.modifierWeight = .3f; controller.EvaluateTiles(0);
    check(energy(render()) > energy(reference) * 1.5, "Tile influence blends the complete effect with the substrate");
    web.modifierWeight = 1; web.strandWidth = 0; controller.EvaluateTiles(0); var empty = render();
    check(energy(empty) < energy(reference) * .05, "Zero width suppresses strands, junctions and halo");
    web.strandWidth = .025f;
    // The same field must survive splitting the facade into different tile meshes.
    controller.RestoreTiles();
    panel.transform.localScale = new Vector3(3, 10, .1f); panel.transform.localPosition = new Vector3(-2.5f, 0, 0);
    var other = GameObject.CreatePrimitive(PrimitiveType.Cube); other.name = "Concrete panel web split";
    other.transform.SetParent(root.transform, false); other.transform.localScale = new Vector3(5, 10, .1f);
    other.transform.localPosition = new Vector3(1.5f, 0, 0); other.GetComponent<Renderer>().sharedMaterial = source;
    controller.CaptureCurrentTileLayout(); controller.previewInEditMode = true; controller.EvaluateTiles(0);
    check(changed(reference, render()) < 100, "The pattern is continuous across unequal tile partitions");
    var volumeObject = new GameObject("Production exposure"); volumeObject.transform.SetParent(root.transform, false);
    volumeObject.layer = 31; var volume = volumeObject.AddComponent<UnityEngine.Rendering.Volume>(); volume.isGlobal = true;
    profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>(); volume.sharedProfile = profile;
    var exposure = profile.Add<UnityEngine.Rendering.HighDefinition.Exposure>(true);
    exposure.mode.value = UnityEngine.Rendering.HighDefinition.ExposureMode.Fixed; exposure.fixedExposure.value = 11.1f;
    cameraData.volumeLayerMask = 1 << 31;
    frame.SetEnabled(UnityEngine.Rendering.HighDefinition.FrameSettingsField.ExposureControl, true);
    frameField.SetValue(cameraData, frame);
    var exposed = render();
    check(energy(exposed) > energy(reference) * .8 && energy(exposed) < energy(reference) * 1.2,
        "Production 11.1 EV exposure preserves the Unlit web brightness");
    web.cellSize = .01f; web.strandWidth = 100; web.glowWidth = 100;
    web.distortion = 2; web.brightness = 100; web.junctionStrength = 5; controller.EvaluateTiles(0);
    bool finite = true;
    foreach (var p in render()) finite &= !float.IsNaN(p.r) && !float.IsInfinity(p.r)
        && !float.IsNaN(p.g) && !float.IsInfinity(p.g) && !float.IsNaN(p.b) && !float.IsInfinity(p.b);
    check(finite, "Extreme Inspector settings render finite pixels");
    check(!ShaderUtil.ShaderHasError(web.webMaterial.shader), "Exercised HDRP passes compile without shader errors");
    return "Passed " + passed + " neural web rendering checks.\n" + report + "Renders: " + folder;
}
finally
{
    RenderTexture.active = previous;
    if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
    if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
    UnityEngine.Object.DestroyImmediate(root);
    if (source != null) UnityEngine.Object.DestroyImmediate(source);
    if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
