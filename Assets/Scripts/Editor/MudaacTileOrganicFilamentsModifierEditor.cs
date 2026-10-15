using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MudaacTileOrganicFilamentsModifier))]
public class MudaacTileOrganicFilamentsModifierEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var filaments = (MudaacTileOrganicFilamentsModifier)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Moving membranes, fine strands and glowing junctions cover the tile interiors. "
            + "Tile Influence bends the field within each tile; zero makes it continuous across the facade. "
            + "Influencers blend the effect per tile. Cell Size, Membrane Width and Glow Width use metres.", MessageType.Info);
        if (filaments.filamentMaterial == null || !filaments.filamentMaterial.HasProperty("_OrganicPattern"))
            EditorGUILayout.HelpBox("Assign Assets/Materials/MudaacOrganicFilaments.mat to Filament Material.", MessageType.Warning);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(filaments.paused ? "Resume" : "Pause"))
            {
                Undo.RecordObject(filaments, "Pause Organic Filaments");
                filaments.paused = !filaments.paused;
                EditorUtility.SetDirty(filaments);
                PrefabUtility.RecordPrefabInstancePropertyModifications(filaments);
            }
            if (GUILayout.Button("Restart Animation"))
                filaments.RestartAnimation();
        }
        var controller = filaments.GetComponent<MudaacTileController>();
        if (controller != null && GUILayout.Button(controller.previewInEditMode
            ? "Stop Preview And Restore" : "Preview Organic Filaments"))
        {
            Undo.RecordObject(controller, "Preview Organic Filaments");
            controller.previewInEditMode = !controller.previewInEditMode;
            if (!controller.previewInEditMode)
                controller.RestoreTiles();
            EditorUtility.SetDirty(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        }
    }
}
