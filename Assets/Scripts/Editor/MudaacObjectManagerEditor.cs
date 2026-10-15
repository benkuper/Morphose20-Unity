using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MudaacObjectManager)), CanEditMultipleObjects]
public class MudaacObjectManagerEditor : Editor
{
    private readonly HashSet<string> expandedGroups = new HashSet<string>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("modelRoot"));
        if (serializedObject.ApplyModifiedProperties())
            foreach (UnityEngine.Object item in targets)
                ((MudaacObjectManager)item).RefreshGroups();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Show All")) Change("Show All Mudaac Objects", manager => manager.ShowAll());
            if (GUILayout.Button("Hide All")) Change("Hide All Mudaac Objects", manager => manager.HideAll());
        }
        bool canRestore = false;
        foreach (UnityEngine.Object item in targets)
            canRestore |= ((MudaacObjectManager)item).CanRestoreVisibility;
        using (new EditorGUI.DisabledScope(!canRestore))
            if (GUILayout.Button("Restore Previous Visibility"))
                Change("Restore Mudaac Visibility", manager => manager.RestoreVisibility());

        EditorGUILayout.HelpBox("Toggle groups to show or hide them. Isolate shows only one group; enable extra groups to combine them. "
            + "Restore Previous Visibility returns to the setup before isolation. Tile visibility is independent of modifier inclusion.", MessageType.Info);
        if (serializedObject.isEditingMultipleObjects)
            EditorGUILayout.HelpBox("Select one manager to edit individual visibility groups.", MessageType.Info);
        else
        {
            var manager = (MudaacObjectManager)target;
            EditorGUILayout.LabelField("Tile Groups", EditorStyles.boldLabel);
            DrawAllTiles(manager);
            foreach (MudaacObjectManager.ObjectGroup group in manager.Groups)
                if (group.isTileGroup)
                    DrawGroup(manager, group);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Other Objects", EditorStyles.boldLabel);
            foreach (MudaacObjectManager.ObjectGroup group in manager.Groups)
                if (!group.isTileGroup)
                    DrawGroup(manager, group);
        }
        EditorGUILayout.Space();
        if (GUILayout.Button("Refresh Object Groups"))
            Change("Refresh Mudaac Object Groups", manager => manager.RefreshGroups());
    }

    private void DrawAllTiles(MudaacObjectManager manager)
    {
        int total = 0, visible = 0;
        foreach (MudaacObjectManager.ObjectGroup group in manager.Groups)
            if (group.isTileGroup)
            {
                total += group.objects.Count;
                visible += manager.GetVisibleCount(group);
            }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.showMixedValue = visible > 0 && visible < total;
            EditorGUI.BeginChangeCheck();
            bool show = EditorGUILayout.ToggleLeft("All tiles (" + visible + "/" + total + ")", visible > 0);
            if (EditorGUI.EndChangeCheck())
                Change("Toggle Mudaac Tiles", item => item.SetTilesVisible(show));
            EditorGUI.showMixedValue = false;
            if (GUILayout.Button("Isolate", GUILayout.Width(52f)))
                Change("Isolate Mudaac Tiles", item => item.IsolateTiles());
        }
    }

    private void DrawGroup(MudaacObjectManager manager, MudaacObjectManager.ObjectGroup group)
    {
        int visible = manager.GetVisibleCount(group);
        bool expanded = expandedGroups.Contains(group.id);
        using (new EditorGUILayout.HorizontalScope())
        {
            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(14f));
            expanded = EditorGUI.Foldout(rect, expanded, GUIContent.none, true);
            if (expanded) expandedGroups.Add(group.id); else expandedGroups.Remove(group.id);
            EditorGUI.showMixedValue = visible > 0 && visible < group.objects.Count;
            EditorGUI.BeginChangeCheck();
            bool show = EditorGUILayout.ToggleLeft(group.name + " (" + visible + "/" + group.objects.Count + ")", visible > 0);
            if (EditorGUI.EndChangeCheck())
                Change("Toggle " + group.name, item => item.SetGroupVisible(group.id, show));
            EditorGUI.showMixedValue = false;
            if (GUILayout.Button("Isolate", GUILayout.Width(52f)))
                Change("Isolate " + group.name, item => item.IsolateGroup(group.id));
        }
        if (!expanded)
            return;
        EditorGUI.indentLevel++;
        foreach (GameObject item in group.objects)
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(item, typeof(GameObject), true);
        EditorGUI.indentLevel--;
    }

    private void Change(string label, Action<MudaacObjectManager> action)
    {
        foreach (UnityEngine.Object item in targets)
        {
            var manager = (MudaacObjectManager)item;
            List<UnityEngine.Object> affected = manager.GetVisibilityObjects();
            var activeStates = new Dictionary<GameObject, bool>();
            foreach (UnityEngine.Object candidate in affected)
                if (candidate is GameObject gameObject)
                    activeStates.Add(gameObject, gameObject.activeSelf);
            Undo.RecordObjects(affected.ToArray(), label);
            action(manager);
            foreach (UnityEngine.Object changed in affected)
            {
                if (changed is GameObject gameObject && gameObject.activeSelf == activeStates[gameObject])
                    continue;
                EditorUtility.SetDirty(changed);
                PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
            }
        }
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
