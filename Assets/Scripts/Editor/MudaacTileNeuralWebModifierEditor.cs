using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MudaacTileNeuralWebModifier))]
public class MudaacTileNeuralWebModifierEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var web = (MudaacTileNeuralWebModifier)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Animated cellular filaments with luminous junctions and nested fine webs. "
            + "Cell Size, Strand Width and Glow Width use metres. The pattern spans the authored facade; "
            + "Influencers and Modifier Weight blend the entire effect per tile.", MessageType.Info);
        if (web.webMaterial == null || !web.webMaterial.HasProperty("_WebShape"))
            EditorGUILayout.HelpBox("Assign Assets/Materials/MudaacNeuralWeb.mat to Web Material.", MessageType.Warning);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Restart Animation"))
                web.RestartAnimation();
            if (GUILayout.Button(web.paused ? "Resume" : "Pause"))
            {
                Undo.RecordObject(web, "Pause Neural Web");
                web.paused = !web.paused;
                EditorUtility.SetDirty(web);
                PrefabUtility.RecordPrefabInstancePropertyModifications(web);
            }
        }
        var controller = web.GetComponent<MudaacTileController>();
        if (controller != null && GUILayout.Button(controller.previewInEditMode ? "Stop Preview And Restore" : "Preview Neural Web"))
        {
            Undo.RecordObject(controller, "Preview Neural Web");
            controller.previewInEditMode = !controller.previewInEditMode;
            if (!controller.previewInEditMode)
                controller.RestoreTiles();
            EditorUtility.SetDirty(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        }
    }
}
