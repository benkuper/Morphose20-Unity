using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MudaacTileWireVolumeModifier))]
public class MudaacTileWireVolumeModifierEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var wire = (MudaacTileWireVolumeModifier)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Fine box edges with translucent faces and interior mist. Edge Width and Glow Width use metres. "
            + "Bottom Density settles the mist toward world down. Influencers and Modifier Weight blend each tile into the effect. "
            + "Use a dark background for the reference look.", MessageType.Info);
        if (wire.wireMaterial == null || !wire.wireMaterial.HasProperty("_WireVolume"))
            EditorGUILayout.HelpBox("Assign Assets/Materials/MudaacWireVolume.mat to Wire Material.", MessageType.Warning);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reference Mist"))
                ApplyPreset(wire, true);
            if (GUILayout.Button("Clean Wire"))
                ApplyPreset(wire, false);
        }
        var controller = wire.GetComponent<MudaacTileController>();
        if (controller != null && GUILayout.Button(controller.previewInEditMode
            ? "Stop Preview And Restore" : "Preview Wire Volume"))
        {
            Undo.RecordObject(controller, "Preview Wire Volume");
            controller.previewInEditMode = !controller.previewInEditMode;
            if (!controller.previewInEditMode)
                controller.RestoreTiles();
            EditorUtility.SetDirty(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        }
    }

    private static void ApplyPreset(MudaacTileWireVolumeModifier wire, bool mist)
    {
        Undo.RecordObject(wire, "Wire Volume Preset");
        wire.opacity = 1f;
        wire.edgeColor = new Color(0.75f, 0.75f, 0.75f);
        wire.edgeWidth = 0.006f;
        wire.edgeOpacity = 0.75f;
        wire.edgeBrightness = 1f;
        wire.glowWidth = mist ? 0.02f : 0f;
        wire.glowStrength = mist ? 0.12f : 0f;
        wire.rearEdgeOpacity = 0.35f;
        wire.volumeColor = new Color(0.35f, 0.35f, 0.35f);
        wire.faceOpacity = mist ? 0.025f : 0f;
        wire.volumeDensity = mist ? 0.15f : 0f;
        wire.bottomDensity = 3f;
        wire.heightFalloff = 4f;
        wire.volumeSamples = 6;
        wire.noiseScale = 2.5f;
        wire.noiseStrength = mist ? 0.65f : 0f;
        wire.grainScale = 140f;
        wire.grainStrength = mist ? 0.65f : 0f;
        wire.drift = new Vector3(0.04f, 0.08f, 0.02f);
        EditorUtility.SetDirty(wire);
        PrefabUtility.RecordPrefabInstancePropertyModifications(wire);
    }
}
