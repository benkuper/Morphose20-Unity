using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(PhotoCameraCalibration))]
public class PhotoCameraCalibrationInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("Open Photo Calibration"))
            PhotoCameraCalibrationWindow.Open((PhotoCameraCalibration)target);
    }
}

public class PhotoCameraCalibrationWindow : EditorWindow
{
    [SerializeField] private PhotoCameraCalibration calibration;
    [SerializeField] private int selectedPoint = -1;
    private bool pickingModelPoint;
    private Vector2 listScroll;
    private string feedback;
    private MessageType feedbackType;

    [MenuItem("Tools/Camera/Photo Calibration")]
    private static void OpenFromMenu()
    {
        Open(Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<PhotoCameraCalibration>() : null);
    }

    public static void Open(PhotoCameraCalibration component)
    {
        var window = GetWindow<PhotoCameraCalibrationWindow>("Photo Calibration");
        window.minSize = new Vector2(740, 600);
        if (component != null)
        {
            window.calibration = component;
            window.selectedPoint = -1;
            window.pickingModelPoint = false;
        }
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += DuringSceneGUI;
        Undo.undoRedoPerformed += Repaint;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
        Undo.undoRedoPerformed -= Repaint;
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        calibration = (PhotoCameraCalibration)EditorGUILayout.ObjectField("Calibration", calibration, typeof(PhotoCameraCalibration), true);
        if (EditorGUI.EndChangeCheck())
        {
            selectedPoint = -1;
            pickingModelPoint = false;
            feedback = null;
        }

        if (calibration == null)
        {
            EditorGUILayout.HelpBox("Select your scene Camera, then add calibration. The landmark pairs will be saved on it.", MessageType.Info);
            var selectedCamera = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Camera>() : null;
            using (new EditorGUI.DisabledScope(selectedCamera == null || EditorApplication.isPlaying
                || (selectedCamera != null && EditorUtility.IsPersistent(selectedCamera))))
            {
                if (GUILayout.Button("Add Calibration to Selected Camera"))
                {
                    calibration = selectedCamera.GetComponent<PhotoCameraCalibration>();
                    if (calibration == null)
                        calibration = Undo.AddComponent<PhotoCameraCalibration>(selectedCamera.gameObject);
                }
            }
            return;
        }

        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || EditorUtility.IsPersistent(calibration)))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            DrawControls();
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Click the picture, then click the same feature on the model in the Scene view.", EditorStyles.wordWrappedLabel);
            DrawPicture();
            EditorGUILayout.LabelField("Cyan: reference point   Yellow: camera projection   Line: alignment error", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
        if (EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("Leave Play mode to calibrate and keep the result in the scene.", MessageType.Info);
    }

    private void DrawControls()
    {
        var serialized = new SerializedObject(calibration);
        serialized.Update();
        foreach (string name in new[] { "targetCamera", "modelRoot", "referenceImage", "imageResolution", "fitFieldOfView" })
            EditorGUILayout.PropertyField(serialized.FindProperty(name));
        serialized.ApplyModifiedProperties();

        EditorGUILayout.HelpBox("1. Set Game view to 1280 x 1920 (or your image resolution).\n2. Roughly position the camera using SceneCamSync, then turn following off.\n3. Add 6-12 matching points spread across the image, with varied model depths.\n4. Fit Camera + FOV.", MessageType.Info);

        RawImage reference = calibration.ReferenceImage;
        if (reference != null)
        {
            EditorGUI.BeginChangeCheck();
            float opacity = EditorGUILayout.Slider("Game overlay opacity", reference.color.a, 0, 1);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(reference, "Change Reference Opacity");
                Color color = reference.color;
                color.a = opacity;
                reference.color = color;
                PrefabUtility.RecordPrefabInstancePropertyModifications(reference);
                EditorUtility.SetDirty(reference);
                RefreshViews();
            }
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Landmark"))
        {
            Undo.RecordObject(calibration, "Add Calibration Landmark");
            calibration.Landmarks.Add(new PhotoCameraCalibration.Landmark { label = "Point " + (calibration.Landmarks.Count + 1) });
            selectedPoint = calibration.Landmarks.Count - 1;
            pickingModelPoint = false;
            MarkLandmarksChanged();
        }
        using (new EditorGUI.DisabledScope(!HasSelection()))
        {
            if (GUILayout.Button(pickingModelPoint ? "Cancel Picking" : "Pick Model Vertex"))
            {
                pickingModelPoint = !pickingModelPoint;
                SceneView.RepaintAll();
            }
        }
        EditorGUILayout.EndHorizontal();

        listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.MinHeight(120), GUILayout.ExpandHeight(true));
        for (int i = 0; i < calibration.Landmarks.Count; ++i)
        {
            var point = calibration.Landmarks[i];
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(selectedPoint == i, (i + 1).ToString(), "Button", GUILayout.Width(30)) && selectedPoint != i)
            {
                selectedPoint = i;
                pickingModelPoint = false;
                SceneView.RepaintAll();
            }
            EditorGUI.BeginChangeCheck();
            string label = EditorGUILayout.TextField(point.label);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(calibration, "Rename Calibration Landmark");
                point.label = label;
                MarkLandmarksChanged();
            }
            if (GUILayout.Button("X", GUILayout.Width(22)))
            {
                Undo.RecordObject(calibration, "Remove Calibration Landmark");
                calibration.Landmarks.RemoveAt(i);
                selectedPoint = -1;
                pickingModelPoint = false;
                MarkLandmarksChanged();
                EditorGUILayout.EndHorizontal();
                break;
            }
            EditorGUILayout.EndHorizontal();
            string state = point.IsComplete ? PointError(point) : (point.hasImagePoint ? "Pick model vertex" : "Click picture");
            EditorGUILayout.LabelField(state, EditorStyles.miniLabel);
        }
        EditorGUILayout.EndScrollView();

        if (pickingModelPoint)
            EditorGUILayout.HelpBox("Click the matching model vertex in the Scene view. Zoom in for accuracy. Alt/right mouse still navigate; Esc cancels.", MessageType.Info);
        if (!string.IsNullOrEmpty(feedback))
            EditorGUILayout.HelpBox(feedback, feedbackType);

        using (new EditorGUI.DisabledScope(calibration.TargetCamera == null))
        {
            if (GUILayout.Button(calibration.FitFieldOfView ? "Fit Camera + FOV" : "Fit Camera Pose", GUILayout.Height(32)))
                FitCamera();
        }
    }

    private void DrawPicture()
    {
        Rect available = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        RawImage reference = calibration.ReferenceImage;
        if (reference == null || reference.texture == null)
        {
            GUI.Label(available, "Assign your full-screen Raw Image.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        var resolution = calibration.ImageResolution;
        float aspect = resolution.x > 0 && resolution.y > 0 ? (float)resolution.x / resolution.y : 2f / 3f;
        float width = Mathf.Min(available.width, available.height * aspect);
        Rect picture = new Rect(available.center.x - width / 2, available.center.y - width / aspect / 2, width, width / aspect);
        GUI.DrawTextureWithTexCoords(picture, reference.texture, reference.uvRect, true);

        Event current = Event.current;
        if (GUI.enabled && current.type == EventType.MouseDown && current.button == 0 && picture.Contains(current.mousePosition))
        {
            if (!HasSelection())
            {
                Undo.RecordObject(calibration, "Add Calibration Landmark");
                calibration.Landmarks.Add(new PhotoCameraCalibration.Landmark { label = "Point " + (calibration.Landmarks.Count + 1) });
                selectedPoint = calibration.Landmarks.Count - 1;
            }
            else
                Undo.RecordObject(calibration, "Set Picture Landmark");
            calibration.Landmarks[selectedPoint].imagePoint = new Vector2(
                Mathf.InverseLerp(picture.xMin, picture.xMax, current.mousePosition.x),
                Mathf.InverseLerp(picture.yMax, picture.yMin, current.mousePosition.y));
            calibration.Landmarks[selectedPoint].hasImagePoint = true;
            pickingModelPoint = true;
            MarkLandmarksChanged();
            current.Use();
        }

        Handles.BeginGUI();
        for (int i = 0; i < calibration.Landmarks.Count; ++i)
        {
            var point = calibration.Landmarks[i];
            if (!point.hasImagePoint)
                continue;
            Vector2 observed = PicturePosition(picture, point.imagePoint);
            Handles.color = Color.cyan;
            DrawCross(observed, selectedPoint == i ? 7 : 4);
            GUI.Label(new Rect(observed.x + 6, observed.y - 18, 40, 20), (i + 1).ToString(), EditorStyles.whiteBoldLabel);
            if (point.IsComplete && calibration.TargetCamera != null)
            {
                Vector3 projected = calibration.TargetCamera.WorldToViewportPoint(point.WorldPoint);
                if (projected.z > 0 && projected.x >= 0 && projected.x <= 1 && projected.y >= 0 && projected.y <= 1)
                {
                    Vector2 predicted = PicturePosition(picture, projected);
                    Handles.color = Color.yellow;
                    Handles.DrawLine(observed, predicted);
                    DrawCross(predicted, 4);
                }
            }
        }
        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void DuringSceneGUI(SceneView view)
    {
        if (calibration == null || EditorApplication.isPlaying || EditorUtility.IsPersistent(calibration))
            return;
        for (int i = 0; i < calibration.Landmarks.Count; ++i)
        {
            var point = calibration.Landmarks[i];
            if (point.surface == null)
                continue;
            Vector3 position = point.WorldPoint;
            Handles.color = i == selectedPoint ? Color.yellow : Color.cyan;
            Handles.DotHandleCap(0, position, Quaternion.identity, HandleUtility.GetHandleSize(position) * 0.035f, EventType.Repaint);
            Handles.Label(position, (i + 1) + ": " + point.label);
            if (i == selectedPoint && !pickingModelPoint)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(calibration, "Move Calibration Landmark");
                    point.localPoint = point.surface.InverseTransformPoint(moved);
                    MarkLandmarksChanged();
                }
            }
        }
        Handles.color = Color.white;
        if (!pickingModelPoint || !HasSelection())
            return;

        Event current = Event.current;
        if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
        {
            pickingModelPoint = false;
            current.Use();
            Repaint();
            return;
        }
        if (current.alt || current.button == 1 || current.button == 2)
            return;
        if (current.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (current.type != EventType.MouseDown || current.button != 0)
            return;

        if (calibration.ModelRoot == null)
            ShowFeedback("Assign Model Root before picking vertices.", MessageType.Warning);
        else if (HandleUtility.FindNearestVertex(current.mousePosition,
            calibration.ModelRoot.GetComponentsInChildren<Transform>(), out Vector3 vertex, out GameObject meshObject)
            && Vector2.Distance(HandleUtility.WorldToGUIPoint(vertex), current.mousePosition) < 24)
        {
            Undo.RecordObject(calibration, "Set Model Landmark");
            var point = calibration.Landmarks[selectedPoint];
            point.surface = meshObject.transform;
            point.localPoint = meshObject.transform.InverseTransformPoint(vertex);
            pickingModelPoint = false;
            MarkLandmarksChanged();
            ShowFeedback("Landmark saved. Add another pair, or select an existing pair to refine it.", MessageType.Info);
        }
        else
            ShowFeedback("No model vertex near the click. Zoom in and click directly on the model surface.", MessageType.Warning);
        current.Use();
        view.Repaint();
    }

    private void FitCamera()
    {
        Camera camera = calibration.TargetCamera;
        if (!ValidateFraming(camera, out string framingError))
        {
            ShowFeedback(framingError, MessageType.Error);
            return;
        }

        var pairs = new List<PhotoCameraFit.Correspondence>();
        foreach (var point in calibration.Landmarks)
        {
            if (!point.IsComplete)
                continue;
            if (point.surface != calibration.ModelRoot && !point.surface.IsChildOf(calibration.ModelRoot))
            {
                ShowFeedback("A landmark belongs to a different model. Re-pick it or restore the original Model Root.", MessageType.Error);
                return;
            }
            Vector3 world = point.WorldPoint;
            pairs.Add(new PhotoCameraFit.Correspondence(world.x, world.y, world.z, point.imagePoint.x, point.imagePoint.y));
        }
        Quaternion initialRotation = camera.transform.rotation;
        Matrix4x4 worldToCamera = Matrix4x4.Rotate(Quaternion.Inverse(initialRotation));
        var rotation = new double[3, 3];
        for (int row = 0; row < 3; ++row)
            for (int column = 0; column < 3; ++column)
                rotation[row, column] = worldToCamera[row, column];
        Vector3 start = camera.transform.position;
        var size = calibration.ImageResolution;
        if (!PhotoCameraFit.TryFit(pairs, new double[] { start.x, start.y, start.z }, rotation,
            camera.fieldOfView, size.x, size.y, calibration.FitFieldOfView, out var result, out string error))
        {
            ShowFeedback(error, MessageType.Error);
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Fit Camera to Picture");
        StopSceneViewFollowing(camera);
        Undo.RecordObjects(new Object[] { camera, camera.transform }, "Fit Camera to Picture");
        Vector3 delta = new Vector3((float)result.RotationVector[0], (float)result.RotationVector[1], (float)result.RotationVector[2]);
        Quaternion change = delta.sqrMagnitude > 1e-16f ? Quaternion.AngleAxis(delta.magnitude * Mathf.Rad2Deg, delta.normalized) : Quaternion.identity;
        camera.transform.SetPositionAndRotation(new Vector3((float)result.Position[0], (float)result.Position[1], (float)result.Position[2]), change * initialRotation);
        camera.usePhysicalProperties = false;
        camera.orthographic = false;
        camera.lensShift = Vector2.zero;
        camera.fieldOfView = (float)result.FieldOfView;
        camera.ResetProjectionMatrix();
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        EditorUtility.SetDirty(camera);
        EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
        Undo.CollapseUndoOperations(undoGroup);
        pickingModelPoint = false;
        ShowFeedback($"Fitted {pairs.Count} pairs. RMS error: {result.RmsPixels:F2} px at {size.x} x {size.y}. Vertical FOV: {result.FieldOfView:F3} degrees."
            + (result.AtFovLimit ? " FOV reached the 5-150 degree limit: check landmark depths and model proportions." : "")
            + "\nYellow projections should meet cyan targets. Remaining error can come from model geometry or landmark placement. Undo restores the camera.",
            result.AtFovLimit ? MessageType.Warning : MessageType.Info);
        RefreshViews();
    }

    private bool ValidateFraming(Camera camera, out string error)
    {
        error = null;
        RawImage reference = calibration.ReferenceImage;
        if (camera == null || calibration.ModelRoot == null || reference == null || reference.texture == null)
            error = "Assign the target Camera, Model Root, and reference Raw Image with a texture.";
        else if (EditorUtility.IsPersistent(camera) || EditorUtility.IsPersistent(calibration.ModelRoot))
            error = "Use a scene Camera and scene model instance, rather than prefab assets.";
        else if (calibration.ImageResolution.x <= 0 || calibration.ImageResolution.y <= 0)
            error = "Image resolution must be positive.";
        else if (camera.targetTexture != null || !FullRect(camera.rect))
            error = "Calibration expects a Camera rendering the full Game view (Viewport Rect 0,0,1,1, no target texture).";
        else if (Mathf.Abs(camera.aspect - (float)calibration.ImageResolution.x / calibration.ImageResolution.y) > 0.001f)
            error = "Set the Game view to the image resolution before fitting, so the Camera aspect ratio matches the picture preview.";
        else
        {
            Canvas canvas = reference.canvas != null ? reference.canvas.rootCanvas : null;
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace || canvas.targetDisplay != camera.targetDisplay)
                error = "Use a screen-space Canvas on the Camera's display for the reference Raw Image.";
            else if (canvas.renderMode == RenderMode.ScreenSpaceCamera && (canvas.worldCamera == null || !FullRect(canvas.worldCamera.rect)))
                error = "The reference Canvas camera must render the full viewport.";
            else
            {
                var corners = new Vector3[4];
                reference.rectTransform.GetWorldCorners(corners);
                var root = (RectTransform)canvas.transform;
                Rect rect = root.rect;
                var expected = new[] { new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax),
                    new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMin) };
                for (int i = 0; i < 4; ++i)
                    if (Vector2.Distance(root.InverseTransformPoint(corners[i]), expected[i]) > Mathf.Max(rect.width, rect.height) * 0.001f)
                    {
                        error = "Stretch the reference Raw Image to fill the Canvas without margins or rotation. The picture preview must match the full Game view.";
                        break;
                    }
            }
        }
        return error == null;
    }

    private static void StopSceneViewFollowing(Camera camera)
    {
        foreach (var sync in Object.FindObjectsByType<SceneCamSync>(FindObjectsInactive.Exclude))
        {
            var serialized = new SerializedObject(sync);
            var target = (Camera)serialized.FindProperty("targetCamera").objectReferenceValue;
            if ((target != null ? target : sync.GetComponent<Camera>()) != camera)
                continue;
            serialized.FindProperty("followSceneView").boolValue = false;
            serialized.ApplyModifiedProperties();
        }
    }

    private string PointError(PhotoCameraCalibration.Landmark point)
    {
        Camera camera = calibration.TargetCamera;
        if (camera == null)
            return "Pair complete";
        Vector3 projected = camera.WorldToViewportPoint(point.WorldPoint);
        if (projected.z <= 0)
            return "Behind camera";
        Vector2 size = calibration.ImageResolution;
        float pixels = Vector2.Scale((Vector2)projected - point.imagePoint, size).magnitude;
        return $"Complete - error {pixels:F1} px";
    }

    private bool HasSelection() => selectedPoint >= 0 && selectedPoint < calibration.Landmarks.Count;
    private static bool FullRect(Rect rect) => Mathf.Abs(rect.x) < 0.001f && Mathf.Abs(rect.y) < 0.001f
        && Mathf.Abs(rect.width - 1) < 0.001f && Mathf.Abs(rect.height - 1) < 0.001f;
    private static Vector2 PicturePosition(Rect rect, Vector2 point) => new Vector2(rect.x + point.x * rect.width, rect.yMax - point.y * rect.height);

    private static void DrawCross(Vector2 point, float radius)
    {
        Handles.DrawLine(point - Vector2.right * radius, point + Vector2.right * radius);
        Handles.DrawLine(point - Vector2.up * radius, point + Vector2.up * radius);
    }

    private void MarkLandmarksChanged()
    {
        EditorUtility.SetDirty(calibration);
        PrefabUtility.RecordPrefabInstancePropertyModifications(calibration);
        if (calibration.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(calibration.gameObject.scene);
        Repaint();
        SceneView.RepaintAll();
    }

    private void ShowFeedback(string message, MessageType type)
    {
        feedback = message;
        feedbackType = type;
        Repaint();
    }

    private static void RefreshViews()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
}
