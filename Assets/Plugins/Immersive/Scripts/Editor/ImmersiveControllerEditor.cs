using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ImmersiveController)), CanEditMultipleObjects]
public sealed class ImmersiveControllerEditor : Editor
{
    private const float MinimumDimension = 0.01f;
    private static GUIStyle dimensionLabelStyle;
    private bool editWallEdges = true;

    private void OnEnable()
    {
        Undo.undoRedoPerformed += RefreshScene;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= RefreshScene;
    }

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        editWallEdges = EditorGUILayout.Toggle("Edit Wall Edges", editWallEdges);
        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }

        if (editWallEdges)
        {
            EditorGUILayout.HelpBox("Drag a wall edge or its square handle in Scene view to resize the room. "
                + "The opposite edge stays fixed. Hold Ctrl (Cmd on Mac) to use the Move snap increment. "
                + "Only enabled surfaces have handles; adjoining walls share the room dimensions.", MessageType.Info);
        }

        if (DrawDefaultInspector())
        {
            RefreshScene();
        }
    }

    private void OnSceneGUI()
    {
        var controller = (ImmersiveController)target;
        if (!editWallEdges || !controller.isActiveAndEnabled)
        {
            return;
        }

        // Each selected controller has its own handles and serialized changes.
        using (var data = new SerializedObject(controller))
        {
            var bounds = GetRoomBounds(data);
            DrawSurface(data, controller.transform, bounds, "frontWall", "Front", 2, true);
            DrawSurface(data, controller.transform, bounds, "backWall", "Back", 2, false);
            DrawSurface(data, controller.transform, bounds, "leftWall", "Left", 0, false);
            DrawSurface(data, controller.transform, bounds, "rightWall", "Right", 0, true);
            DrawSurface(data, controller.transform, bounds, "floor", "Floor", 1, false);
            DrawSurface(data, controller.transform, bounds, "ceiling", "Ceiling", 1, true);
        }
    }

    private static Bounds GetRoomBounds(SerializedObject data)
    {
        var size = new Vector3(data.FindProperty("roomWidth").floatValue,
            data.FindProperty("roomHeight").floatValue, data.FindProperty("roomDepth").floatValue);
        var offset = data.FindProperty("roomOffset").vector3Value;
        return new Bounds(offset + new Vector3(0f, size.y * 0.5f, -size.z * 0.5f), size);
    }

    private static void DrawSurface(SerializedObject data, Transform room, Bounds bounds,
        string enabledProperty, string label, int normalAxis, bool maximum)
    {
        if (!data.FindProperty(enabledProperty).boolValue)
        {
            return;
        }

        int horizontal = (normalAxis + 1) % 3;
        int vertical = (normalAxis + 2) % 3;
        var a = bounds.min;
        a[normalAxis] = maximum ? bounds.max[normalAxis] : bounds.min[normalAxis];
        var b = a;
        b[horizontal] = bounds.max[horizontal];
        var c = b;
        c[vertical] = bounds.max[vertical];
        var d = a;
        d[vertical] = bounds.max[vertical];
        var center = (a + c) * 0.5f;

        if (dimensionLabelStyle == null)
        {
            dimensionLabelStyle = new GUIStyle(EditorStyles.helpBox);
            dimensionLabelStyle.normal.textColor = Color.white;
        }

        using (new Handles.DrawingScope(new Color(0.3f, 0.85f, 1f, 0.9f), Matrix4x4.identity))
        {
            Handles.DrawAAPolyLine(2f, room.TransformPoint(a), room.TransformPoint(b),
                room.TransformPoint(c), room.TransformPoint(d), room.TransformPoint(a));
            Handles.Label(room.TransformPoint(center), label + "  "
                + bounds.size[horizontal].ToString("0.###") + " x "
                + bounds.size[vertical].ToString("0.###") + " m", dimensionLabelStyle);

            DrawEdge(data, room, bounds, a, b, center, vertical, false);
            DrawEdge(data, room, bounds, b, c, center, horizontal, true);
            DrawEdge(data, room, bounds, c, d, center, vertical, true);
            DrawEdge(data, room, bounds, d, a, center, horizontal, false);
        }
    }

    private static void DrawEdge(SerializedObject data, Transform room, Bounds bounds,
        Vector3 start, Vector3 end, Vector3 faceCenter, int axis, bool maximum)
    {
        var startWorld = room.TransformPoint(start);
        var endWorld = room.TransformPoint(end);
        var midpoint = (startWorld + endWorld) * 0.5f;
        var inward = room.TransformPoint(faceCenter) - midpoint;
        float handleSize = HandleUtility.GetHandleSize(midpoint) * 0.045f;
        // Separate controls on adjoining faces so both resize directions remain accessible.
        var handleOffset = inward.normalized * Mathf.Min(handleSize * 2.5f, inward.magnitude * 0.25f);
        var localDirection = Vector3.zero;
        localDirection[axis] = 1f;
        var worldDirection = room.TransformVector(localDirection);
        if (worldDirection.sqrMagnitude < 0.000001f)
        {
            return;
        }

        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        var color = axis == 0 ? Handles.xAxisColor : axis == 1 ? Handles.yAxisColor : Handles.zAxisColor;
        var view = SceneView.currentDrawingSceneView;
        var rotation = view != null ? view.camera.transform.rotation : Quaternion.identity;
        Handles.CapFunction cap = (id, position, ignoredRotation, size, eventType) =>
        {
            if (eventType == EventType.Layout)
            {
                HandleUtility.AddControl(id, HandleUtility.DistanceToLine(startWorld, endWorld));
            }

            bool highlighted = GUIUtility.hotControl == id || HandleUtility.nearestControl == id;
            using (new Handles.DrawingScope(highlighted ? Handles.selectedColor : color))
            {
                if (eventType == EventType.Repaint)
                {
                    Handles.DrawLine(midpoint, midpoint + handleOffset);
                    if (highlighted)
                    {
                        Handles.DrawAAPolyLine(4f, startWorld, endWorld);
                    }
                }

                Handles.RectangleHandleCap(id, position, rotation, size, eventType);
            }
        };

        EditorGUI.BeginChangeCheck();
        var moved = Handles.Slider(controlId, midpoint, handleOffset, worldDirection.normalized,
            handleSize, cap, EditorSnapSettings.move[axis] * worldDirection.magnitude);
        if (EditorGUI.EndChangeCheck())
        {
            float coordinate = room.InverseTransformPoint(moved)[axis];
            ApplyRoomBounds(data, ResizeBounds(bounds, axis, maximum, coordinate));
        }
    }

    private static Bounds ResizeBounds(Bounds bounds, int axis, bool maximum, float coordinate)
    {
        var min = bounds.min;
        var max = bounds.max;
        if (maximum)
        {
            max[axis] = Mathf.Max(min[axis] + MinimumDimension, coordinate);
        }
        else
        {
            min[axis] = Mathf.Min(max[axis] - MinimumDimension, coordinate);
        }

        bounds.SetMinMax(min, max);
        return bounds;
    }

    private static void ApplyRoomBounds(SerializedObject data, Bounds bounds)
    {
        Undo.SetCurrentGroupName("Resize Immersive Wall");
        data.FindProperty("roomWidth").floatValue = bounds.size.x;
        data.FindProperty("roomHeight").floatValue = bounds.size.y;
        data.FindProperty("roomDepth").floatValue = bounds.size.z;
        data.FindProperty("roomOffset").vector3Value =
            new Vector3(bounds.center.x, bounds.min.y, bounds.max.z);
        if (data.ApplyModifiedProperties())
        {
            RefreshScene();
        }
    }

    private static void RefreshScene()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }
}
