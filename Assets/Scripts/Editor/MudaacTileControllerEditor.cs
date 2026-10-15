using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MudaacTileController)), CanEditMultipleObjects]
public class MudaacTileControllerEditor : Editor
{
    private readonly HashSet<int> expandedGroups = new HashSet<int>();
    private bool showDiscovery;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawGroups();
        showDiscovery = EditorGUILayout.Foldout(showDiscovery, "Discovery Settings", true);
        if (showDiscovery)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tilesRoot"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tileNamePrefixes"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("explicitTiles"), true);
        }
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("globalModifiers"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("previewInEditMode"));
        bool changed = serializedObject.ApplyModifiedProperties();
        if (changed)
            foreach (Object item in targets)
                ((MudaacTileController)item).RefreshTiles();
        foreach (Object item in targets)
        {
            var controller = (MudaacTileController)item;
            EditorGUILayout.LabelField(controller.name + " tiles", controller.Tiles.Count.ToString());
        }
        EditorGUILayout.HelpBox("Add Mudaac modifiers to this GameObject. Assign influencer components to each modifier. "
            + "Modifiers run in component order, starting from the authored layout each frame.", MessageType.Info);

        if (GUILayout.Button("Refresh Tile Groups"))
            foreach (Object item in targets)
            {
                var controller = (MudaacTileController)item;
                Undo.RecordObject(controller, "Refresh Mudaac Tile Groups");
                controller.RefreshTiles();
                EditorUtility.SetDirty(controller);
                PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            }
        if (GUILayout.Button("Stop Preview and Restore Tiles"))
            foreach (Object item in targets)
            {
                var controller = (MudaacTileController)item;
                Undo.RecordObject(controller, "Stop Mudaac Tile Preview");
                controller.previewInEditMode = false;
                controller.RestoreTiles();
                EditorUtility.SetDirty(controller);
                PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            }
        if (showDiscovery && GUILayout.Button("Capture Current Tile Layout"))
            foreach (Object item in targets)
            {
                var controller = (MudaacTileController)item;
                Undo.RecordObject(controller, "Capture Mudaac Tile Layout");
                controller.CaptureCurrentTileLayout();
                EditorUtility.SetDirty(controller);
                PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            }
    }

    private void DrawGroups()
    {
        EditorGUILayout.LabelField("Tile Groups", EditorStyles.boldLabel);
        if (serializedObject.isEditingMultipleObjects)
        {
            EditorGUILayout.HelpBox("Select one controller to edit its tile groups.", MessageType.Info);
            return;
        }
        SerializedProperty groups = serializedObject.FindProperty("tileGroups");
        EditorGUILayout.HelpBox("Check groups to include them in tile effects. Expand a group to include or exclude individual panels. "
            + "Undercroft panels are excluded by default.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Include All Groups"))
                SetGroupsIncluded(groups, true);
            if (GUILayout.Button("Exclude All Groups"))
                SetGroupsIncluded(groups, false);
        }
        var controller = (MudaacTileController)target;
        for (int i = 0; i < groups.arraySize; i++)
        {
            SerializedProperty group = groups.GetArrayElementAtIndex(i);
            SerializedProperty included = group.FindPropertyRelative("included");
            SerializedProperty tiles = group.FindPropertyRelative("tiles");
            int kind = group.FindPropertyRelative("kind").enumValueIndex;
            string name = i < controller.TileGroups.Count ? controller.TileGroups[i].Name : "Tiles";
            int selectedCount = 0;
            for (int tileIndex = 0; tileIndex < tiles.arraySize; tileIndex++)
                if (tiles.GetArrayElementAtIndex(tileIndex).FindPropertyRelative("included").boolValue)
                    selectedCount++;
            bool expanded = expandedGroups.Contains(kind);
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect foldoutRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(14f));
                bool nextExpanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, true);
                if (nextExpanded) expandedGroups.Add(kind); else expandedGroups.Remove(kind);
                expanded = nextExpanded;
                EditorGUILayout.PropertyField(included, GUIContent.none, GUILayout.Width(18f));
                EditorGUILayout.LabelField(name + " (" + (included.boolValue ? selectedCount : 0) + "/" + tiles.arraySize + ")");
                if (GUILayout.Button("Select", GUILayout.Width(48f)))
                    SelectGroup(tiles);
            }
            if (!expanded)
                continue;
            EditorGUI.indentLevel++;
            for (int tileIndex = 0; tileIndex < tiles.arraySize; tileIndex++)
            {
                SerializedProperty selection = tiles.GetArrayElementAtIndex(tileIndex);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(selection.FindPropertyRelative("included"), GUIContent.none,
                        GUILayout.Width(32f));
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(selection.FindPropertyRelative("renderer"), GUIContent.none);
                }
            }
            EditorGUI.indentLevel--;
        }
    }

    private static void SetGroupsIncluded(SerializedProperty groups, bool included)
    {
        for (int i = 0; i < groups.arraySize; i++)
            groups.GetArrayElementAtIndex(i).FindPropertyRelative("included").boolValue = included;
    }

    private static void SelectGroup(SerializedProperty tiles)
    {
        var objects = new List<Object>();
        for (int i = 0; i < tiles.arraySize; i++)
        {
            var renderer = tiles.GetArrayElementAtIndex(i).FindPropertyRelative("renderer").objectReferenceValue as Renderer;
            if (renderer != null)
                objects.Add(renderer.gameObject);
        }
        Selection.objects = objects.ToArray();
    }
}
