using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MudaacTilePaintModifier))]
public class MudaacTilePaintModifierEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var paint = (MudaacTilePaintModifier)target;
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Psychedelic Brush"))
            {
                Undo.RecordObject(paint, "Psychedelic Paint Style");
                paint.psychedelic = 2.5f; paint.pigmentDistortion = 7f; paint.swirlStrength = 5f;
                paint.marbling = 2f; paint.pigmentContrast = 1.5f; paint.colorSaturation = 1.8f;
                paint.brushStrength = 2f; paint.brushRoughness = 1.8f; paint.grain = 0.7f;
                EditorUtility.SetDirty(paint);
                PrefabUtility.RecordPrefabInstancePropertyModifications(paint);
            }
            if (GUILayout.Button("Organic Strokes"))
            {
                Undo.RecordObject(paint, "Organic Paint Propagation");
                paint.strokePropagation = 1f; paint.strokeStretch = 12f; paint.strokeSpeedVariation = 3f;
                paint.strokeSpacing = 0.6f; paint.strokeCurvature = 2.5f; paint.strokeBranching = 4f;
                paint.strokeSharpness = 3f; paint.spreadNoiseWarp = 4f; paint.spreadNoiseAmount = 1.5f;
                EditorUtility.SetDirty(paint);
                PrefabUtility.RecordPrefabInstancePropertyModifications(paint);
            }
        }
        DrawDefaultInspector();
        var controller = paint.GetComponent<MudaacTileController>();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Tile Randomization: 0 = continuous spread, 1 = randomized whole-tile fills. "
            + "Stroke Propagation adds curving lanes with different speeds. Preset buttons keep your palette. "
            + "Spread Noise animates independently of pigment. Drag the origin handle in Scene view.", MessageType.Info);
        if (paint.paintMaterial == null || !paint.paintMaterial.HasProperty("_PaintSpread"))
            EditorGUILayout.HelpBox("Assign the MudaacPaint material to Paint Material.", MessageType.Warning);
        EditorGUILayout.LabelField("Selected surfaces", controller.Tiles.Count.ToString());
        EditorGUILayout.LabelField("Paint progress", paint.CurrentProgress.ToString("P0"));
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Restart Paint"))
                paint.RestartPaint();
            if (GUILayout.Button(paint.paused ? "Resume" : "Pause"))
            {
                Undo.RecordObject(paint, "Pause Mudaac Paint");
                paint.paused = !paint.paused;
                EditorUtility.SetDirty(paint);
            }
            if (GUILayout.Button("Refresh Spread"))
                paint.RefreshSpread();
        }
        if (GUILayout.Button(controller.previewInEditMode ? "Stop Preview And Restore" : "Preview Paint"))
        {
            Undo.RecordObject(controller, "Toggle Mudaac Paint Preview");
            controller.previewInEditMode = !controller.previewInEditMode;
            if (controller.previewInEditMode)
                paint.RestartPaint();
            else
                controller.RestoreTiles();
            EditorUtility.SetDirty(controller);
        }
        if (controller.previewInEditMode)
            Repaint();
    }

    private void OnSceneGUI()
    {
        var paint = (MudaacTilePaintModifier)target;
        var controller = paint.GetComponent<MudaacTileController>();
        if (controller.Tiles.Count == 0)
            return;
        Bounds bounds = controller.LocalBounds;
        Vector3 point = paint.origin != null ? paint.origin.position
            : controller.transform.TransformPoint(bounds.min + Vector3.Scale(bounds.size, paint.originInFacadeBounds));
        Handles.color = new Color(0.05f, 0.85f, 0.75f);
        Handles.DrawWireDisc(point, SceneView.currentDrawingSceneView.camera.transform.forward, HandleUtility.GetHandleSize(point) * 0.1f);
        Handles.Label(point, "Paint origin");
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.PositionHandle(point, Quaternion.identity);
        if (!EditorGUI.EndChangeCheck())
            return;
        if (paint.origin != null)
        {
            Undo.RecordObject(paint.origin, "Move Paint Origin");
            paint.origin.position = moved;
            EditorUtility.SetDirty(paint.origin);
            PrefabUtility.RecordPrefabInstancePropertyModifications(paint.origin);
        }
        else
        {
            Undo.RecordObject(paint, "Move Paint Origin");
            Vector3 local = controller.transform.InverseTransformPoint(moved) - bounds.min;
            paint.originInFacadeBounds = new Vector3(
                bounds.size.x > 0.001f ? Mathf.Clamp01(local.x / bounds.size.x) : 0.5f,
                bounds.size.y > 0.001f ? Mathf.Clamp01(local.y / bounds.size.y) : 0.5f,
                bounds.size.z > 0.001f ? Mathf.Clamp01(local.z / bounds.size.z) : 0.5f);
            EditorUtility.SetDirty(paint);
            PrefabUtility.RecordPrefabInstancePropertyModifications(paint);
        }
        paint.RefreshSpread();
    }
}
