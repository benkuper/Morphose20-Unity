using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MudaacTilePaintRevealModifier))]
public class MudaacTilePaintRevealModifierEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var reveal = (MudaacTilePaintRevealModifier)target;
        var controller = reveal.GetComponent<MudaacTileController>();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Broad brush swipes alternate across the facade. Brush Angle rotates the entire pattern. "
            + "Disable Automatic Reveal to scrub Progress. Paint And Erase wipes back to the original surface in the same stroke order.", MessageType.Info);
        if (reveal.revealMaterial == null || !reveal.revealMaterial.HasProperty("_RevealProgress"))
            EditorGUILayout.HelpBox("Assign Assets/Materials/MudaacPaintReveal.mat to Reveal Material.", MessageType.Warning);
        EditorGUILayout.LabelField("Painted / erased", reveal.RevealProgress.ToString("P0") + " / " + reveal.EraseProgress.ToString("P0"));
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Restart Reveal"))
                reveal.RestartReveal();
            if (GUILayout.Button(reveal.paused ? "Resume" : "Pause"))
            {
                Undo.RecordObject(reveal, "Pause Paint Reveal");
                reveal.paused = !reveal.paused;
                EditorUtility.SetDirty(reveal);
                PrefabUtility.RecordPrefabInstancePropertyModifications(reveal);
            }
        }
        if (GUILayout.Button(controller.previewInEditMode ? "Stop Preview And Restore" : "Preview Reveal"))
        {
            Undo.RecordObject(controller, "Preview Paint Reveal");
            controller.previewInEditMode = !controller.previewInEditMode;
            if (controller.previewInEditMode)
                reveal.RestartReveal();
            else
                controller.RestoreTiles();
            EditorUtility.SetDirty(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        }
        if (controller.previewInEditMode)
            Repaint();
    }
}
