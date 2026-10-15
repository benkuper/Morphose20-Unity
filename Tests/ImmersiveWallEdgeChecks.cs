// Execute this method body with Unity MCP execute_code (CodeDom/C# 6 compatible).
// Uses a preview scene, leaves the live scene untouched, and writes no Unity assets.
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var root = new UnityEngine.GameObject("Immersive wall edge checks");
root.SetActive(false);
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
var report = new System.Text.StringBuilder();
int passed = 0;
System.Action<bool, string> check = (condition, message) => {
    if (!condition) throw new System.Exception("FAIL: " + message);
    passed++; report.AppendLine("PASS: " + message);
};
UnityEditor.Editor editor = null;
ImmersiveController controller = null;
try
{
    controller = root.AddComponent<ImmersiveController>();
    var data = new UnityEditor.SerializedObject(controller);
    data.FindProperty("desiredResolutionValue").intValue = 32;
    data.FindProperty("enableSpoutSender").boolValue = false;
    data.FindProperty("enableNdiSender").boolValue = false;
    data.FindProperty("previewCamera").intValue = -1;
    data.ApplyModifiedPropertiesWithoutUndo();
    root.transform.SetPositionAndRotation(new UnityEngine.Vector3(7f, 2f, -4f),
        UnityEngine.Quaternion.Euler(0f, 35f, 0f));
    root.SetActive(true);
    editor = UnityEditor.Editor.CreateEditor(controller);
    var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
    var getBounds = editor.GetType().GetMethod("GetRoomBounds", flags);
    var resize = editor.GetType().GetMethod("ResizeBounds", flags);
    var apply = editor.GetType().GetMethod("ApplyRoomBounds", flags);
    var update = typeof(ImmersiveController).GetMethod("Update",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    System.Func<UnityEngine.Bounds> read = () => {
        data.Update(); return (UnityEngine.Bounds)getBounds.Invoke(null, new object[] { data });
    };
    var original = read();
    var rootPosition = root.transform.position;
    var eye = root.transform.Find("Cameras").position;
    check(original.min == new UnityEngine.Vector3(-2.5f, 0f, -5f)
        && original.max == new UnityEngine.Vector3(2.5f, 3f, 0f),
        "Existing rooms retain their geometry with the default zero offset");
    check(root.transform.Find("Walls").childCount == 6, "All enabled surfaces generate wall planes");

    var computeTextureSize = typeof(ImmersiveController).GetMethod("ComputeRenderTextureSize",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var normalSize = (UnityEngine.Vector2Int)computeTextureSize.Invoke(controller, new object[] { 5f, 3f });
    check(normalSize == new UnityEngine.Vector2Int(64, 32),
        "Ordinary output resolutions retain their existing 16-pixel alignment");
    var oversized = (UnityEngine.Vector2Int)computeTextureSize.Invoke(controller,
        new object[] { (float)UnityEngine.SystemInfo.maxTextureSize, 100f });
    check(oversized.x <= UnityEngine.SystemInfo.maxTextureSize
        && oversized.y <= UnityEngine.SystemInfo.maxTextureSize
        && oversized.x % 16 == 0 && oversized.y % 16 == 0,
        "Extreme aspect ratios stay within the GPU texture limit and retain NDI alignment");

    for (int axis = 0; axis < 3; axis++)
    {
        for (int side = 0; side < 2; side++)
        {
            bool maximum = side == 1;
            float coordinate = maximum ? original.max[axis] + 1.25f : original.min[axis] - 1.25f;
            var resized = (UnityEngine.Bounds)resize.Invoke(null, new object[] { original, axis, maximum, coordinate });
            apply.Invoke(null, new object[] { data, resized });
            update.Invoke(controller, null);
            var actual = read();
            check(UnityEngine.Vector3.Distance(actual.center, resized.center) < 0.0001f
                && UnityEngine.Vector3.Distance(actual.size, resized.size) < 0.0001f
                && UnityEngine.Mathf.Abs((maximum ? actual.min[axis] : actual.max[axis])
                    - (maximum ? original.min[axis] : original.max[axis])) < 0.0001f,
                "Resize axis " + axis + " " + (maximum ? "maximum" : "minimum") + " keeps the opposite edge fixed");
            check(root.transform.position == rootPosition && root.transform.Find("Cameras").position == eye,
                "Resize axis " + axis + " " + side + " preserves the controller and camera eye");
            var walls = root.transform.Find("Walls");
            var front = walls.Find("Front_Wall");
            var back = walls.Find("Back_Wall");
            var left = walls.Find("Left_Wall");
            var right = walls.Find("Right_Wall");
            var floor = walls.Find("Floor_Wall");
            var ceiling = walls.Find("Ceiling_Wall");
            check(UnityEngine.Vector3.Distance(front.localPosition,
                    new UnityEngine.Vector3(actual.center.x, actual.center.y, actual.max.z)) < 0.0001f
                && UnityEngine.Mathf.Abs(back.localPosition.z - actual.min.z) < 0.0001f
                && UnityEngine.Mathf.Abs(left.localPosition.x - actual.min.x) < 0.0001f
                && UnityEngine.Mathf.Abs(right.localPosition.x - actual.max.x) < 0.0001f
                && UnityEngine.Mathf.Abs(floor.localPosition.y - actual.min.y) < 0.0001f
                && UnityEngine.Mathf.Abs(ceiling.localPosition.y - actual.max.y) < 0.0001f
                && UnityEngine.Mathf.Abs(front.localScale.x - actual.size.x) < 0.0001f
                && UnityEngine.Mathf.Abs(front.localScale.y - actual.size.y) < 0.0001f
                && UnityEngine.Mathf.Abs(floor.localScale.y - actual.size.z) < 0.0001f,
                "Resize axis " + axis + " " + side + " updates all adjoining wall positions and dimensions");

            float crossing = maximum ? original.min[axis] - 10f : original.max[axis] + 10f;
            var clamped = (UnityEngine.Bounds)resize.Invoke(null, new object[] { original, axis, maximum, crossing });
            check(UnityEngine.Mathf.Abs(clamped.size[axis] - 0.01f) < 0.00001f,
                "Resize axis " + axis + " " + side + " clamps crossed edges to one centimetre");
        }
    }

    apply.Invoke(null, new object[] { data, original });
    update.Invoke(controller, null);
    UnityEditor.Undo.FlushUndoRecordObjects();
    UnityEditor.Undo.IncrementCurrentGroup();
    var changed = (UnityEngine.Bounds)resize.Invoke(null, new object[] { original, 0, false, -4f });
    apply.Invoke(null, new object[] { data, changed });
    update.Invoke(controller, null);
    UnityEditor.Undo.FlushUndoRecordObjects();
    UnityEditor.Undo.PerformUndo();
    update.Invoke(controller, null);
    check(read().center == original.center && read().size == original.size,
        "Undo restores both the room offset and dimensions");
    UnityEditor.Undo.PerformRedo();
    update.Invoke(controller, null);
    check(read().center == changed.center && read().size == changed.size,
        "Redo reapplies the resized room");

    data.Update();
    foreach (string property in new[] { "leftWall", "rightWall", "backWall", "floor", "ceiling" })
        data.FindProperty(property).boolValue = false;
    data.ApplyModifiedPropertiesWithoutUndo();
    update.Invoke(controller, null);
    check(root.transform.Find("Walls").childCount == 1
        && root.transform.Find("Walls/Front_Wall") != null
        && root.transform.Find("Cameras").childCount == 1,
        "Disabled surfaces remove their wall planes and cameras");
    data.Update();
    data.FindProperty("floor").boolValue = true;
    data.FindProperty("showRoomPlanes").boolValue = false;
    data.ApplyModifiedPropertiesWithoutUndo();
    update.Invoke(controller, null);
    check(root.transform.Find("Walls").childCount == 2
        && !root.transform.Find("Walls/Front_Wall").GetComponent<UnityEngine.MeshRenderer>().enabled
        && !root.transform.Find("Walls/Floor_Wall").GetComponent<UnityEngine.MeshRenderer>().enabled
        && root.transform.Find("Cameras/Front_Camera").GetComponent<UnityEngine.Camera>().enabled,
        "Re-enabling a surface works and preview visibility remains independent of camera output");

    var offset = data.FindProperty("roomOffset").vector3Value;
    check(offset != UnityEngine.Vector3.zero
        && UnityEditor.EditorJsonUtility.ToJson(controller).Contains("roomOffset"),
        "Room edge offsets are serialized for scene and prefab persistence");

    foreach (var cameraData in root.transform.Find("Cameras")
        .GetComponentsInChildren<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>())
    {
        check(cameraData.antialiasing == UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData
                .AntialiasingMode.TemporalAntialiasing
            && cameraData.TAAQuality == UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.TAAQualityLevel.High
            && cameraData.dithering,
            cameraData.name + " receives high quality TAA and gradient dithering");
    }
    var frontCamera = root.transform.Find("Cameras/Front_Camera").GetComponent<UnityEngine.Camera>();
    var projection = frontCamera.projectionMatrix;
    var output = frontCamera.targetTexture;
    data.Update();
    data.FindProperty("antialiasing").intValue = (int)UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData
        .AntialiasingMode.SubpixelMorphologicalAntiAliasing;
    data.FindProperty("dithering").boolValue = false;
    data.FindProperty("leftWall").boolValue = true;
    data.ApplyModifiedPropertiesWithoutUndo();
    update.Invoke(controller, null);
    foreach (var cameraData in root.transform.Find("Cameras")
        .GetComponentsInChildren<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>())
    {
        check(cameraData.antialiasing == UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData
                .AntialiasingMode.SubpixelMorphologicalAntiAliasing
            && cameraData.SMAAQuality == UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.SMAAQualityLevel.High
            && !cameraData.dithering,
            cameraData.name + " receives rendering changes, including newly enabled surfaces");
    }
    check(frontCamera.projectionMatrix == projection && frontCamera.targetTexture == output,
        "Changing antialiasing preserves the off-axis projection and existing output texture");
    return passed + " checks passed.\n" + report.ToString();
}
finally
{
    if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
    if (controller != null) UnityEditor.Undo.ClearUndo(controller);
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
