// Execute this method body with Unity MCP execute_code. Uses an isolated HDRP preview scene.
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var root = new GameObject("Paint render checks");
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
    panel.name = "Concrete panel paint rendering";
    panel.transform.SetParent(root.transform, false);
    panel.transform.localScale = new Vector3(8, 4, 0.1f);
    source = new Material(Shader.Find("HDRP/Lit"));
    source.SetColor("_BaseColor", Color.white);
    UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(source);
    panel.GetComponent<Renderer>().sharedMaterial = source;
    var controller = root.AddComponent<MudaacTileController>();
    var paint = root.AddComponent<MudaacTilePaintModifier>();
    paint.paintMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacPaint.mat");
    paint.automaticSpread = false;
    paint.strokePropagation = 0f;
    paint.originInFacadeBounds = new Vector3(0, 0.5f, 0);
    // A single tile's controller bounds have no extent; use a real source handle on its left edge.
    var origin = new GameObject("Origin"); origin.transform.SetParent(root.transform, false);
    origin.transform.position = new Vector3(-4, 0, -0.05f); paint.origin = origin.transform;
    paint.drySmoothness = paint.wetSmoothness = 0.3f;
    paint.spreadNoiseAmount = 0.1f;
    paint.spreadNoiseSpeed = 0f;

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
    paint.progress = 0; controller.EvaluateTiles(0); var clean = render();
    paint.progress = 0.25f; controller.EvaluateTiles(0.1f); var partial = render();
    int earlyPixels = changedPixels(clean, partial);
    check(earlyPixels > 100 && earlyPixels < 12000, "The shader paints a portion of a single panel instead of switching its whole colour");
    paint.progress = 0.75f; controller.EvaluateTiles(0.2f); var later = render();
    int laterPixels = changedPixels(clean, later);
    check(laterPixels > earlyPixels + 1000, "Increasing progress visibly expands coverage across the panel surface");
    paint.progress = 1; controller.EvaluateTiles(0.3f); var filled = render();
    for (int i = 4; i < 44; i++) controller.EvaluateTiles(i * 0.1f);
    var evolved = render();
    check(changedPixels(filled, evolved) > 500, "Rendered pigment visibly evolves after coverage reaches 100 percent");
    paint.motionSpeed = 0; controller.EvaluateTiles(4.4f); var frozen = render();
    for (int i = 45; i < 65; i++) controller.EvaluateTiles(i * 0.1f);
    var still = render();
    check(changedPixels(frozen, still) < 20, "Zero motion speed visibly freezes the filled pigment field");
    paint.paused = true; controller.EvaluateTiles(6.5f); var paused = render();
    controller.EvaluateTiles(9f);
    check(changedPixels(paused, render()) < 20, "Pause freezes the rendered effect");
    controller.RestoreTiles();
    check(changedPixels(clean, render()) < 20, "Restoration reproduces the original rendered facade");
    paint.paused = false; paint.motionSpeed = 0; paint.grain = 0;
    paint.brushStrength = 0f;
    paint.paintA = paint.paintB = paint.paintC = Color.blue;
    paint.spreadNoiseAmount = 0; paint.edgeSoftness = 0.01f;
    paint.tileRandomization = 0; paint.progress = 0.35f;
    paint.RestartPaint(); controller.EvaluateTiles(0); var continuousSingle = render();
    controller.RestoreTiles();
    panel.transform.localScale = new Vector3(4, 4, 0.1f); panel.transform.localPosition = new Vector3(-2, 0, 0);
    var second = GameObject.CreatePrimitive(PrimitiveType.Cube); second.name = "Concrete panel second";
    second.transform.SetParent(root.transform, false); second.transform.localScale = new Vector3(4, 4, 0.1f);
    second.transform.localPosition = new Vector3(2, 0, 0); second.GetComponent<Renderer>().sharedMaterial = source;
    controller.CaptureCurrentTileLayout(); paint.RestartPaint(); controller.EvaluateTiles(0);
    var continuousSplit = render();
    check(changedPixels(continuousSingle, continuousSplit) < 50,
        "Continuous spread is visually unchanged when the same facade is split into separate tiles");
    int leftA = 64 * 256 + 40, leftB = 64 * 256 + 115, rightA = 64 * 256 + 145, rightB = 64 * 256 + 215;
    check(Mathf.Abs(continuousSplit[leftA].r - continuousSplit[leftB].r) > 0.02f,
        "The continuous end can partially fill a tile");
    paint.tileRandomization = 1; controller.EvaluateTiles(0.1f); var randomized = render();
    check(Mathf.Abs(randomized[leftA].r - randomized[leftB].r) < 0.01f
        && Mathf.Abs(randomized[rightA].r - randomized[rightB].r) < 0.01f,
        "The randomized end fills each tile uniformly");
    paint.tileRandomization = 0.5f; controller.EvaluateTiles(0.2f);
    check(changedPixels(continuousSplit, render()) > 100,
        "Intermediate slider values interpolate continuous spread and tile arrival randomization");
    paint.tileRandomization = 0; paint.progress = 0.35f; paint.spreadNoiseAmount = 2;
    paint.spreadNoiseSpeed = 0; controller.EvaluateTiles(0.3f); var noiseBase = render();
    paint.spreadNoiseStretch = new Vector3(0.3f, 3, 1); paint.spreadNoiseOctaves = 5;
    paint.spreadNoiseLacunarity = 2.7f; paint.spreadNoiseWarp = 2;
    controller.EvaluateTiles(0.4f);
    check(changedPixels(noiseBase, render()) > 100, "Noise stretching, detail and warp visibly reshape the spreading edge");
    var previousSeed = render(); paint.noise.seed = 91; paint.noise.offset = new Vector3(1, 2, 3);
    controller.EvaluateTiles(0.5f);
    check(changedPixels(previousSeed, render()) > 100, "Spread seed and offset visibly change the edge independently of pigment");
    var staticNoise = render();
    for (int i = 6; i < 26; i++) controller.EvaluateTiles(i * 0.1f);
    check(changedPixels(staticNoise, render()) < 20, "Zero spread noise speed freezes the edge field");
    paint.spreadNoiseSpeed = 1;
    for (int i = 26; i < 66; i++) controller.EvaluateTiles(i * 0.1f);
    check(changedPixels(staticNoise, render()) > 100, "Spread noise can evolve while pigment motion is frozen");
    paint.spreadNoiseAmount = 0; paint.spreadNoiseSpeed = 0;
    paint.strokePropagation = 0; paint.progress = 0.15f;
    controller.EvaluateTiles(6.6f); var roundFront = render();
    paint.strokePropagation = 1; paint.strokeStretch = 8; paint.strokeSpeedVariation = 0;
    paint.strokeCurvature = 0; paint.strokeDirection = Vector3.right;
    controller.EvaluateTiles(6.7f); var horizontal = render();
    check(changedPixels(roundFront, horizontal) > 500, "Stroke propagation produces an elongated front instead of a spherical front");
    paint.strokeDirection = Vector3.up; controller.EvaluateTiles(6.8f); var vertical = render();
    check(changedPixels(horizontal, vertical) > 500, "Changing stroke direction rotates the visible growth pattern");
    paint.strokeDirection = Vector3.right; paint.strokeSpeedVariation = 3;
    controller.EvaluateTiles(6.9f); var variedSpeeds = render();
    check(changedPixels(horizontal, variedSpeeds) > 500, "Different stroke lanes advance at visibly different speeds");
    paint.strokeCurvature = 2; paint.strokeBranching = 6;
    controller.EvaluateTiles(7f);
    check(changedPixels(variedSpeeds, render()) > 100, "Curvature and branching bend the advancing stroke lanes");
    paint.progress = 1; paint.strokeStretch = 40; paint.strokeSpeedVariation = 8;
    paint.strokeCurvature = 20; paint.strokeBranching = 16; paint.strokeSharpness = 16;
    paint.spreadNoiseOctaves = 8; paint.spreadNoiseLacunarity = 8; paint.spreadNoiseContrast = 16;
    paint.spreadNoiseAmount = 20; paint.spreadNoiseWarp = 64;
    controller.EvaluateTiles(7.1f); var fullStrokes = render();
    check(changedPixels(clean, fullStrokes) > 19000, "Full progress fills the entire surface with extreme stroke and spread noise settings");
    paint.paintA = new Color(0.035f, 0.14f, 0.3f); paint.paintB = new Color(0.05f, 0.7f, 0.65f);
    paint.paintC = new Color(1, 0.38f, 0.13f); paint.motionSpeed = 0;
    paint.strokePropagation = 0; paint.psychedelic = 0; paint.swirlStrength = 0; paint.pigmentDistortion = 0;
    paint.brushStrength = 0; paint.grain = 0; controller.EvaluateTiles(7.2f); var plainPigment = render();
    paint.psychedelic = 2.5f; paint.swirlStrength = 5; paint.pigmentDistortion = 7;
    paint.brushStrength = 2; paint.brushRoughness = 1.8f; paint.marbling = 2; paint.grain = 0.7f;
    controller.EvaluateTiles(7.3f); var psychedelicBrush = render();
    check(changedPixels(plainPigment, psychedelicBrush) > 1000, "Psychedelic distortion and bristles visibly transform the painted texture");
    paint.brushAngle = 90; controller.EvaluateTiles(7.4f);
    check(changedPixels(psychedelicBrush, render()) > 1000, "Brush angle rotates the textured bristle marks");
    paint.psychedelic = 8; paint.swirlStrength = 32; paint.pigmentDistortion = 64;
    paint.brushStrength = 8; paint.brushStretch = 100; paint.brushRoughness = 8;
    paint.marbling = 16; paint.grain = 4; paint.pigmentContrast = 8; paint.colorSaturation = 4;
    controller.EvaluateTiles(7.5f); var extreme = render(); bool finite = true;
    foreach (var pixel in extreme)
        finite &= !float.IsNaN(pixel.r) && !float.IsInfinity(pixel.r) && !float.IsNaN(pixel.g)
            && !float.IsInfinity(pixel.g) && !float.IsNaN(pixel.b) && !float.IsInfinity(pixel.b);
    check(finite, "Extreme psychedelic and brush settings render finite pixels without shader errors");
    report.AppendLine("Changed pixels: partial=" + earlyPixels + ", expanded=" + laterPixels);
    check(!ShaderUtil.ShaderHasError(paint.paintMaterial.shader), "Actual HDRP render passes compile without shader errors");
    return "Passed " + passed + " rendering checks.\n" + report;
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
