// Execute this method body with Unity MCP execute_code (CodeDom/C# 6 compatible).
// Renders an isolated preview scene; does not change the user's scene or materials.
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var root = new UnityEngine.GameObject("Unlit shadow receiver checks");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
var report = new System.Text.StringBuilder();
int passed = 0;
System.Action<bool, string> check = (condition, message) => {
    if (!condition) throw new System.Exception("FAIL: " + message + "\n" + report);
    passed++; report.AppendLine("PASS: " + message);
};
UnityEngine.Material receiverMaterial = null, casterMaterial = null;
UnityEngine.RenderTexture target = null;
UnityEngine.Texture2D readback = null;
var previousTarget = UnityEngine.RenderTexture.active;
try
{
    var shader = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>(
        "Assets/Shaders/UnlitShadowReceiver.shadergraph");
    check(shader != null && shader.isSupported && !UnityEditor.ShaderUtil.ShaderHasError(shader),
        "The HDRP shader imports and compiles");
    receiverMaterial = new UnityEngine.Material(shader);
    UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(receiverMaterial);
    check(receiverMaterial.HasProperty("_BaseColor") && receiverMaterial.HasProperty("_ShadowColor")
        && receiverMaterial.HasProperty("_ShadowIntensity"), "The requested material controls are exposed");

    var receiver = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Plane);
    receiver.transform.SetParent(root.transform, false);
    receiver.GetComponent<UnityEngine.Renderer>().sharedMaterial = receiverMaterial;
    receiver.GetComponent<UnityEngine.Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    var caster = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
    caster.transform.SetParent(root.transform, false);
    caster.transform.localPosition = new UnityEngine.Vector3(0f, 1f, 0f);
    caster.transform.localScale = UnityEngine.Vector3.one * 1.5f;
    // Keep the caster's own color constant so only receiver pixels can change in comparisons.
    casterMaterial = new UnityEngine.Material(UnityEngine.Shader.Find("HDRP/Unlit"));
    casterMaterial.SetColor("_UnlitColor", UnityEngine.Color.gray);
    UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(casterMaterial);
    caster.GetComponent<UnityEngine.Renderer>().sharedMaterial = casterMaterial;

    var lightObject = new UnityEngine.GameObject("Shadow light");
    lightObject.transform.SetParent(root.transform, false);
    lightObject.transform.rotation = UnityEngine.Quaternion.Euler(55f, -40f, 0f);
    var light = lightObject.AddComponent<UnityEngine.Light>();
    light.type = UnityEngine.LightType.Directional;
    light.intensity = 10000f;
    light.color = UnityEngine.Color.blue;
    light.shadows = UnityEngine.LightShadows.Soft;
    var lightData = lightObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
    lightData.EnableShadows(true);

    var cameraObject = new UnityEngine.GameObject("Test camera");
    cameraObject.transform.SetParent(root.transform, false);
    var camera = cameraObject.AddComponent<UnityEngine.Camera>();
    camera.enabled = false;
    camera.transform.position = new UnityEngine.Vector3(0f, 6f, -7f);
    camera.transform.LookAt(UnityEngine.Vector3.zero);
    camera.orthographic = true;
    camera.orthographicSize = 4f;
    camera.nearClipPlane = 0.1f;
    camera.farClipPlane = 30f;
    camera.overrideSceneCullingMask = UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(scene);
    var cameraData = cameraObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
    cameraData.clearColorMode = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.ClearColorMode.Color;
    cameraData.backgroundColorHDR = UnityEngine.Color.black;
    cameraData.volumeLayerMask = 0;
    cameraData.customRenderingSettings = true;
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

    target = new UnityEngine.RenderTexture(256, 256, 24, UnityEngine.RenderTextureFormat.ARGBHalf,
        UnityEngine.RenderTextureReadWrite.Linear);
    target.Create();
    readback = new UnityEngine.Texture2D(256, 256, UnityEngine.TextureFormat.RGBAFloat, false, true);
    var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest();
    request.destination = target;
    System.Func<UnityEngine.Color[]> render = () => {
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
        UnityEngine.RenderTexture.active = target;
        readback.ReadPixels(new UnityEngine.Rect(0, 0, 256, 256), 0, 0);
        readback.Apply();
        return readback.GetPixels();
    };

    receiverMaterial.SetFloat("_ShadowIntensity", 0f);
    render();
    var white = render();
    receiverMaterial.SetFloat("_ShadowIntensity", 1f);
    render();
    var shadow = render();
    int shadowPixel = -1, whitePixel = -1, darkenedPixels = 0;
    float largestDifference = 0f;
    for (int i = 0; i < white.Length; i++)
    {
        if (white[i].r < 0.99f || white[i].g < 0.99f || white[i].b < 0.99f) continue;
        float difference = white[i].r - shadow[i].r;
        if (difference > 0.15f) darkenedPixels++;
        if (difference > largestDifference) { largestDifference = difference; shadowPixel = i; }
        if (difference < 0.001f && shadow[i].r > 0.99f) whitePixel = i;
    }
    report.AppendLine("Shadowed receiver pixels: " + darkenedPixels + "; maximum contrast: " + largestDifference);
    check(whitePixel >= 0, "Unshadowed regions remain full white under a bright blue light");
    check(shadowPixel >= 0 && darkenedPixels > 100, "The unlit material receives the cube's real-time shadow");
    receiverMaterial.SetFloat("_ShadowIntensity", 0.5f);
    var half = render();
    float expected = (white[shadowPixel].r + shadow[shadowPixel].r) * 0.5f;
    check(UnityEngine.Mathf.Abs(half[shadowPixel].r - expected) < 0.03f,
        "Half intensity blends the shadow halfway toward the unlit base color");
    light.intensity = 0.01f;
    light.color = UnityEngine.Color.red;
    var dim = render();
    check(UnityEngine.Mathf.Abs(dim[whitePixel].r - white[whitePixel].r) < 0.01f
        && UnityEngine.Mathf.Abs(dim[whitePixel].g - white[whitePixel].g) < 0.01f,
        "Changing the light's intensity and color does not tint or dim the base color");
    light.enabled = false;
    var unlit = render();
    check(unlit[shadowPixel].r > 0.99f && unlit[shadowPixel].g > 0.99f && unlit[shadowPixel].b > 0.99f,
        "The receiver stays white with no light and no shadow");

    light.enabled = true;
    light.intensity = 10000f;
    receiverMaterial.SetFloat("_ShadowIntensity", 1f);
    receiverMaterial.SetColor("_ShadowColor", UnityEngine.Color.red);
    var tinted = render();
    check(tinted[shadowPixel].r > 0.95f && tinted[shadowPixel].g < tinted[shadowPixel].r - 0.15f,
        "Shadow Color controls the received shadow tint");

    var output = System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Temp/UnlitShadowReceiver");
    System.IO.Directory.CreateDirectory(output);
    var image = new UnityEngine.Texture2D(256, 256, UnityEngine.TextureFormat.RGBA32, false, true);
    try
    {
        foreach (var capture in new[] { new System.Tuple<string, UnityEngine.Color[]>("zero", white),
            new System.Tuple<string, UnityEngine.Color[]>("half", half),
            new System.Tuple<string, UnityEngine.Color[]>("full", shadow) })
        {
            image.SetPixels(capture.Item2); image.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(output, capture.Item1 + ".png"), image.EncodeToPNG());
        }
    }
    finally { UnityEngine.Object.DestroyImmediate(image); }
    check(!UnityEditor.ShaderUtil.ShaderHasError(shader), "The rendered HDRP shader variants compile without errors");
    return passed + " checks passed.\n" + report;
}
finally
{
    UnityEngine.RenderTexture.active = previousTarget;
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
    if (receiverMaterial != null) UnityEngine.Object.DestroyImmediate(receiverMaterial);
    if (casterMaterial != null) UnityEngine.Object.DestroyImmediate(casterMaterial);
    if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
    if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
}
