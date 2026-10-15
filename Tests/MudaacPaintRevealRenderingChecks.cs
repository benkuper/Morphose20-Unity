// Execute this method body with Unity MCP execute_code. Uses an isolated HDRP preview scene.
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var root = new GameObject("Paint reveal render checks");
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
    var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
    panel.name = "Concrete panel reveal rendering";
    panel.transform.SetParent(root.transform, false);
    panel.transform.localScale = new Vector3(8, 4, 0.1f);
    source = new Material(Shader.Find("HDRP/Lit"));
    source.SetColor("_BaseColor", Color.white);
    UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(source);
    panel.GetComponent<Renderer>().sharedMaterial = source;
    var controller = root.AddComponent<MudaacTileController>();
    var paint = root.AddComponent<MudaacTilePaintRevealModifier>();
    paint.revealMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacPaintReveal.mat");
    paint.automaticReveal = false;
    paint.strokeCount = 6; paint.strokeOverlap = 0; paint.edgeRoughness = 0;
    paint.strokeSpeedVariation = 0; paint.dryBrush = 0; paint.bristleStrength = 0;
    paint.paintColor = Color.green; paint.paintSmoothness = 0.3f;

    var lightObject = new GameObject("Render light"); lightObject.transform.SetParent(root.transform, false);
    lightObject.transform.rotation = Quaternion.Euler(15, -15, 0);
    var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 10000;
    lightObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
    var cameraObject = new GameObject("Render camera"); cameraObject.transform.SetParent(root.transform, false);
    var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
    camera.transform.position = new Vector3(0, 0, -10); camera.transform.LookAt(Vector3.zero);
    camera.orthographic = true; camera.orthographicSize = 2.5f; camera.aspect = 2f;
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
    rt = new RenderTexture(256, 128, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear); rt.Create();
    readback = new Texture2D(256, 128, TextureFormat.RGBAFloat, false, true);
    var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest(); request.destination = rt;
    System.Func<Color[]> render = () => {
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
        RenderTexture.active = rt; readback.ReadPixels(new Rect(0, 0, 256, 128), 0, 0); readback.Apply();
        return readback.GetPixels();
    };
    System.Func<Color[], Color[], int> changedPixels = (a, b) => {
        int count = 0;
        for (int i = 0; i < a.Length; i++)
            if (Mathf.Abs(a[i].r-b[i].r) + Mathf.Abs(a[i].g-b[i].g) + Mathf.Abs(a[i].b-b[i].b) > 0.015f) count++;
        return count;
    };
    System.Func<Color[], Color[], float, float, bool> changedAt = (a, b, x, y) => {
        int index = Mathf.RoundToInt((0.1f + y * 0.8f) * 127) * 256 + Mathf.RoundToInt((0.1f + x * 0.8f) * 255);
        return Mathf.Abs(a[index].r-b[index].r) + Mathf.Abs(a[index].g-b[index].g) + Mathf.Abs(a[index].b-b[index].b) > 0.02f;
    };
    string folder = System.IO.Path.Combine(Application.dataPath, "../Temp/paint-reveal-renders");
    System.IO.Directory.CreateDirectory(folder);
    // The check camera returns scene-linear HDR values without production exposure.
    // Tone-map the saved previews so their colours can be inspected as ordinary PNGs.
    float imageExposure = 0f;
    System.Action<string> save = name => {
        if (imageExposure == 0f)
            imageExposure = 1f / Mathf.Max(0.001f, readback.GetPixel(128, 64).maxColorComponent);
        var pixels = readback.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i] * imageExposure;
            pixels[i] = new Color(c.r / (1f + c.r), c.g / (1f + c.g), c.b / (1f + c.b), 1).gamma;
        }
        var preview = new Texture2D(256, 128, TextureFormat.RGBA32, false, true);
        try
        {
            preview.SetPixels(pixels); preview.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), preview.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(preview); }
    };
    paint.progress = 0; controller.EvaluateTiles(0); var clean = render(); save("00-clean");
    paint.progress = 0.08f; controller.EvaluateTiles(0.1f); var first = render(); save("01-first-swipe");
    check(changedPixels(clean, first) > 200 && changedPixels(clean, first) < 4000,
        "A brush paints only part of one panel at early progress");
    check(changedAt(clean, first, 0.92f, 0.2f) && !changedAt(clean, first, 0.92f, 0.8f),
        "The first swipe starts at the bottom right");
    paint.progress = 0.25f; controller.EvaluateTiles(0.2f); var secondSwipe = render(); save("02-second-swipe");
    check(changedAt(clean, secondSwipe, 0.75f, 0.8f) && !changedAt(clean, secondSwipe, 0.75f, 0.2f),
        "The second swipe reverses direction and starts at the top");
    paint.alternateDirection = false; controller.EvaluateTiles(0.3f); var sameDirection = render();
    check(changedAt(clean, sameDirection, 0.75f, 0.2f) && !changedAt(clean, sameDirection, 0.75f, 0.8f),
        "Disabling alternating direction makes both swipes travel upwards");
    paint.alternateDirection = true;
    int lastCount = 0;
    for (int i = 1; i <= 10; i++)
    {
        paint.progress = i * 0.1f; controller.EvaluateTiles(0.3f + i * 0.1f);
        int count = changedPixels(clean, render());
        check(count >= lastCount, "Coverage grows monotonically at progress " + paint.progress);
        lastCount = count;
    }
    var full = render(); save("03-filled");
    check(lastCount > 19000, "Full progress covers the entire panel");
    paint.progress = 0.25f; paint.edgeRoughness = 0.28f; paint.strokeOverlap = 0.2f;
    controller.EvaluateTiles(1.4f); var rough = render(); save("04-rough-tips");
    check(changedPixels(secondSwipe, rough) > 200, "Roughness and overlap create broken brush tips and stray bristles");
    paint.progress = 1; paint.bristleStrength = 1; paint.bristleDensity = 90;
    controller.EvaluateTiles(1.5f); var textured = render(); save("05-bristles");
    check(changedPixels(full, textured) > 1000, "Bristles leave visible lengthwise pigment ridges");
    paint.dryBrush = 0.5f; controller.EvaluateTiles(1.6f); var dry = render(); save("06-dry-brush");
    check(changedPixels(textured, dry) > 100, "Dry brush leaves fine gaps showing the substrate");
    paint.progress = 0.3f; paint.dryBrush = 0; paint.bristleStrength = 0;
    controller.EvaluateTiles(1.7f); var vertical = render();
    paint.brushAngle = 90; controller.EvaluateTiles(1.8f);
    check(changedPixels(vertical, render()) > 1000, "Brush angle rotates the visible swipe direction");
    paint.brushAngle = 0; paint.startFromRight = false; controller.EvaluateTiles(1.9f);
    check(changedPixels(vertical, render()) > 1000, "The reveal can start from the opposite side");
    paint.startFromRight = true; paint.progress = 0.25f; paint.strokeSpeedVariation = 0.8f;
    controller.EvaluateTiles(2f); var varied = render();
    paint.seed = 91; controller.EvaluateTiles(2.1f);
    check(changedPixels(varied, render()) > 100, "The seed changes brush-tip detail and individual swipe timing");
    // A surface split into two renderers must keep the same projected reveal.
    paint.seed = 7; paint.strokeSpeedVariation = 0; paint.strokeOverlap = 0;
    paint.edgeRoughness = 0; paint.progress = 0.43f;
    controller.EvaluateTiles(2.2f); var continuous = render();
    controller.RestoreTiles();
    panel.transform.localScale = new Vector3(4, 4, 0.1f); panel.transform.localPosition = new Vector3(-2, 0, 0);
    var other = GameObject.CreatePrimitive(PrimitiveType.Cube); other.name = "Concrete panel reveal split";
    other.transform.SetParent(root.transform, false); other.transform.localScale = new Vector3(4, 4, 0.1f);
    other.transform.localPosition = new Vector3(2, 0, 0); other.GetComponent<Renderer>().sharedMaterial = source;
    controller.CaptureCurrentTileLayout(); controller.EvaluateTiles(2.3f);
    check(changedPixels(continuous, render()) < 50, "The same brush reveal remains continuous across tile boundaries");
    paint.modifierWeight = 0.5f; controller.EvaluateTiles(2.4f); var weighted = render();
    check(changedPixels(continuous, weighted) > 1000, "Modifier weight blends paint with the substrate");
    paint.modifierWeight = 1; paint.progress = 1; paint.edgeRoughness = 0.5f; paint.strokeOverlap = 0.5f;
    paint.strokeSpeedVariation = 0.8f; paint.strokeCount = 32; paint.bristleDensity = 600;
    paint.bristleStrength = 1; controller.EvaluateTiles(2.5f);
    bool finite = true;
    foreach (var pixel in render())
        finite &= !float.IsNaN(pixel.r) && !float.IsInfinity(pixel.r) && !float.IsNaN(pixel.g)
            && !float.IsInfinity(pixel.g) && !float.IsNaN(pixel.b) && !float.IsInfinity(pixel.b);
    check(finite, "Extreme brush settings produce finite rendered pixels");
    paint.automaticReveal = true; paint.loopMode = MudaacTilePaintRevealModifier.LoopMode.PaintAndErase;
    paint.revealDuration = 0.4f; paint.paintedHoldTime = 0.2f; paint.eraseDuration = 0.4f; paint.emptyHoldTime = 0.2f;
    paint.strokeCount = 6; paint.bristleStrength = 0; paint.edgeRoughness = 0;
    paint.strokeOverlap = 0; paint.strokeSpeedVariation = 0; paint.RestartReveal();
    controller.EvaluateTiles(0);
    for (int i = 1; i <= 5; i++) controller.EvaluateTiles(i * 0.1f);
    var beforeErase = render();
    for (int i = 6; i <= 8; i++) controller.EvaluateTiles(i * 0.1f);
    var erased = render(); save("07-wipe-out");
    check(changedPixels(clean, erased) > 1000 && changedPixels(clean, erased) < changedPixels(clean, beforeErase) - 1000,
        "The automatic erase pass visibly wipes paint back to the original substrate");
    paint.paused = true; controller.EvaluateTiles(3f);
    check(changedPixels(erased, render()) < 20, "Pause freezes the actual rendered swipe");
    controller.RestoreTiles();
    check(changedPixels(clean, render()) < 50, "Restoration reproduces the original rendered surface");
    check(!ShaderUtil.ShaderHasError(paint.revealMaterial.shader), "All exercised HDRP render passes compile without errors");
    return "Passed " + passed + " paint reveal rendering checks.\n" + report;
}
finally
{
    RenderTexture.active = previous;
    if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
    if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
    UnityEngine.Object.DestroyImmediate(root);
    if (source != null) UnityEngine.Object.DestroyImmediate(source);
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
