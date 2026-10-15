using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MudaacTileLiquidSeamsModifier))]
public class MudaacTileLiquidSeamsModifierEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var seams = (MudaacTileLiquidSeamsModifier)target;
        var controller = seams.GetComponent<MudaacTileController>();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Liquid follows shared tile seams in the controller's facade plane. "
            + "Inside Tile Lines adds moving cells within each tile. Zero Seam Width removes thin edges, leaving the thicker pools. "
            + "Set Interior Blend to zero to keep the building's colour.", MessageType.Info);
        if (seams.seamMaterial == null || !seams.seamMaterial.HasProperty("_SeamShape"))
            EditorGUILayout.HelpBox("Assign Assets/Materials/MudaacLiquidSeams.mat to Seam Material.", MessageType.Warning);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Restart Flow"))
                seams.RestartFlow();
            if (GUILayout.Button(seams.paused ? "Resume" : "Pause"))
            {
                Undo.RecordObject(seams, "Pause Liquid Seams");
                seams.paused = !seams.paused;
                EditorUtility.SetDirty(seams);
                PrefabUtility.RecordPrefabInstancePropertyModifications(seams);
            }
        }
        if (GUILayout.Button(controller.previewInEditMode ? "Stop Preview And Restore" : "Preview Liquid Seams"))
        {
            Undo.RecordObject(controller, "Preview Liquid Seams");
            controller.previewInEditMode = !controller.previewInEditMode;
            if (!controller.previewInEditMode)
                controller.RestoreTiles();
            EditorUtility.SetDirty(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        }
        if (controller.previewInEditMode)
            Repaint();
    }
}
