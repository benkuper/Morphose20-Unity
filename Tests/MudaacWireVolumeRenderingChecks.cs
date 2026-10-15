// Execute through Unity MCP execute_code in Edit Mode. All objects live in a preview scene.
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var root = new GameObject("Wire volume render checks");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
var previous = RenderTexture.active;
RenderTexture rt = null;
Texture2D readback = null;
Material source = null;
Mesh scaledMesh = null;
UnityEngine.Rendering.VolumeProfile exposureProfile = null;
int passed = 0;
var report = new System.Text.StringBuilder();
System.Action<bool, string> check = (condition, name) => {
    if (!condition) throw new System.Exception("FAIL: " + name + "\n" + report);
    passed++; report.AppendLine("PASS: " + name);
};
try
{
    var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
    panel.name = "Concrete panel wire rendering";
    panel.transform.SetParent(root.transform, false);
    panel.transform.localScale = Vector3.one * 2;
    source = new Material(Shader.Find("HDRP/Lit"));
    panel.GetComponent<Renderer>().sharedMaterial = source;
    var controller = root.AddComponent<MudaacTileController>();
    var wire = root.AddComponent<MudaacTileWireVolumeModifier>();
    wire.wireMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MudaacWireVolume.mat");
    wire.drift = Vector3.zero;

    var cameraObject = new GameObject("Wire check camera"); cameraObject.transform.SetParent(root.transform, false);
    var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
    camera.transform.position = new Vector3(3.2f, 2.3f, -4.5f); camera.transform.LookAt(Vector3.zero);
    camera.orthographic = true; camera.orthographicSize = 1.8f; camera.aspect = 1;
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
    rt = new RenderTexture(320, 320, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear); rt.Create();
    readback = new Texture2D(320, 320, TextureFormat.RGBAFloat, false, true);
    var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest(); request.destination = rt;
    System.Func<Color[]> render = () => {
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
        RenderTexture.active = rt; readback.ReadPixels(new Rect(0, 0, 320, 320), 0, 0); readback.Apply();
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
    string folder = System.IO.Path.Combine(Application.dataPath, "../Temp/wire-volume-renders");
    System.IO.Directory.CreateDirectory(folder);
    System.Action<string> save = name => {
        var pixels = readback.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(pixels[i].r, pixels[i].g, pixels[i].b, 1).gamma;
        var preview = new Texture2D(320, 320, TextureFormat.RGBA32, false, true);
        try { preview.SetPixels(pixels); preview.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), preview.EncodeToPNG()); }
        finally { UnityEngine.Object.DestroyImmediate(preview); }
    };
    controller.EvaluateTiles(0); var reference = render(); save("00-reference");
    check(energy(reference) > 1000, "The translucent cube renders visible wire and haze");
    var volumeObject = new GameObject("Exposure regression"); volumeObject.transform.SetParent(root.transform, false);
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
    check(energy(exposed) > energy(reference) * .8 && energy(exposed) < energy(reference) * 1.2,
        "The scene's 11.1 EV exposure preserves Unlit brightness without double compensation");
    frame.SetEnabled(UnityEngine.Rendering.HighDefinition.FrameSettingsField.ExposureControl, false);
    frameField.SetValue(cameraData, frame); cameraData.volumeLayerMask = 0;
    wire.volumeDensity = 0; wire.faceOpacity = 0; wire.glowStrength = 0;
    controller.EvaluateTiles(0); var clean = render(); save("01-clean-wire");
    check(changed(reference, clean) > 10000, "Volume density adds visible interior haze");
    check(clean[160 * 320 + 160].maxColorComponent < .01f, "Wire-only mode keeps the face centre transparent");
    wire.rearEdgeOpacity = 0; controller.EvaluateTiles(0); var noRear = render();
    check(changed(clean, noRear) > 100, "Rear edge opacity controls visible far-side wires");
    wire.edgeWidth = .06f; controller.EvaluateTiles(0); var thick = render(); save("02-thick-wire");
    check(energy(thick) > energy(noRear) * 1.5, "Increasing metre edge width visibly thickens wires");
    wire.edgeWidth = .012f; wire.glowStrength = 1; wire.glowWidth = .08f; controller.EvaluateTiles(0);
    check(changed(noRear, render()) > 1000, "Glow width and strength create a soft edge halo");
    wire.edgeWidth = 0; wire.faceOpacity = .2f; wire.grainStrength = 0; controller.EvaluateTiles(0); var faces = render();
    check(faces[160 * 320 + 160].maxColorComponent > .02f, "Face opacity adds a translucent surface independently of fog");
    wire.grainStrength = 1; wire.grainScale = 90; controller.EvaluateTiles(0);
    check(changed(faces, render()) > 1000, "Grain strength gives the faces visible texture");
    wire.faceOpacity = 0; wire.grainStrength = 0; wire.volumeDensity = .3f;
    wire.noiseStrength = 0; wire.bottomDensity = 0; controller.EvaluateTiles(0); var uniform = render();
    wire.bottomDensity = 8; controller.EvaluateTiles(0); var bottom = render(); save("03-bottom-mist");
    check(changed(uniform, bottom) > 10000 && energy(bottom) > energy(uniform), "Bottom density visibly concentrates interior mist");
    var tile = controller.Tiles[0];
    tile.SetRotationAndScale(Quaternion.Euler(0, 0, 180), tile.LocalScale); tile.Apply();
    check(changed(bottom, render()) < 100, "Bottom mist stays in world down after a later tile rotation");
    controller.EvaluateTiles(0);
    wire.grainStrength = .8f; controller.EvaluateTiles(0);
    check(changed(bottom, render()) > 1000, "Grain textures the interior fog as well as faces");
    wire.grainStrength = 0;
    wire.noiseStrength = 1; wire.noiseScale = 3; controller.EvaluateTiles(0); var noisy = render();
    wire.seed = 91; controller.EvaluateTiles(0);
    check(changed(noisy, render()) > 1000, "Seed changes the three-dimensional mist pattern");
    wire.drift = new Vector3(.2f, .3f, .1f); controller.EvaluateTiles(1); var moving = render();
    controller.EvaluateTiles(3);
    check(changed(moving, render()) > 1000, "Drift animates interior mist");
    wire.drift = Vector3.zero; controller.EvaluateTiles(4); var frozen = render(); controller.EvaluateTiles(8);
    check(changed(frozen, render()) == 0, "Zero drift freezes the rendered texture");
    wire.opacity = 0; controller.EvaluateTiles(0);
    check(energy(render()) < .01, "Zero opacity makes the entire effect transparent");
    wire.opacity = 1; wire.modifierWeight = .5f; controller.EvaluateTiles(0); var blend = render();
    wire.modifierWeight = 1; controller.EvaluateTiles(0);
    check(changed(blend, render()) > 10000, "Modifier weight visibly blends from the solid substrate");
    wire.volumeDensity = 0; wire.faceOpacity = 0; wire.glowStrength = 0; wire.edgeWidth = .025f;
    controller.EvaluateTiles(0);
    var pendingScale = new Vector3(1.8f, .6f, 1.3f);
    tile.LocalScale = Vector3.Scale(tile.LocalScale, pendingScale); tile.Apply(); var scaledAfterWire = render();
    var meshFilter = panel.GetComponent<MeshFilter>(); var cubeMesh = meshFilter.sharedMesh;
    scaledMesh = UnityEngine.Object.Instantiate(cubeMesh);
    var vertices = scaledMesh.vertices;
    for (int i = 0; i < vertices.Length; i++) vertices[i] = Vector3.Scale(vertices[i], pendingScale);
    scaledMesh.vertices = vertices; scaledMesh.RecalculateBounds(); meshFilter.sharedMesh = scaledMesh;
    controller.EvaluateTiles(0);
    check(changed(scaledAfterWire, render()) < 100,
        "Metre wire widths match baked geometry when nonuniform scale runs after the wire modifier");
    meshFilter.sharedMesh = cubeMesh; controller.EvaluateTiles(0);
    wire.volumeDensity = 1000; wire.heightFalloff = .1f; wire.volumeSamples = 12;
    wire.noiseScale = .01f; wire.edgeWidth = 100; wire.edgeBrightness = 100; controller.EvaluateTiles(0);
    bool finite = true;
    foreach (var p in render()) finite &= !float.IsNaN(p.r) && !float.IsInfinity(p.r)
        && !float.IsNaN(p.g) && !float.IsInfinity(p.g) && !float.IsNaN(p.b) && !float.IsInfinity(p.b);
    check(finite, "Extreme control values render finite pixels");
    // Exercise the actual imported bevelled panel without touching the live scene.
    var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Mudaac Model.fbx");
    MeshFilter imported = null;
    foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
        if (filter.name == "Concrete panel 0000") { imported = filter; break; }
    check(imported != null, "The representative imported facade panel is available");
    controller.RestoreTiles();
    panel.GetComponent<MeshFilter>().sharedMesh = imported.sharedMesh;
    panel.transform.localScale = Vector3.one * 100;
    panel.transform.localRotation = Quaternion.Euler(-90, 180, 0);
    panel.transform.localPosition = -panel.transform.TransformVector(imported.sharedMesh.bounds.center);
    controller.CaptureCurrentTileLayout();
    wire.edgeWidth = .006f; wire.edgeBrightness = 1; wire.edgeOpacity = .75f;
    wire.rearEdgeOpacity = .35f; wire.glowWidth = .02f; wire.glowStrength = .12f;
    wire.volumeDensity = .15f; wire.bottomDensity = 3; wire.heightFalloff = 4; wire.volumeSamples = 6;
    wire.faceOpacity = .025f; wire.noiseStrength = .65f; wire.noiseScale = 2.5f;
    wire.grainStrength = .65f; wire.grainScale = 140; wire.seed = 7;
    controller.EvaluateTiles(0); var actual = render(); save("04-imported-panel");
    check(energy(actual) > 100, "The effect renders on the imported bevelled facade geometry");
    check(!ShaderUtil.ShaderHasError(wire.wireMaterial.shader), "Exercised HDRP passes compile without shader errors");
    return "Passed " + passed + " wire volume rendering checks.\n" + report;
}
finally
{
    RenderTexture.active = previous;
    if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
    if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
    UnityEngine.Object.DestroyImmediate(root);
    if (source != null) UnityEngine.Object.DestroyImmediate(source);
    if (scaledMesh != null) UnityEngine.Object.DestroyImmediate(scaledMesh);
    if (exposureProfile != null) UnityEngine.Object.DestroyImmediate(exposureProfile);
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
