using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways, DisallowMultipleComponent]
public class MudaacObjectManager : MonoBehaviour
{
    [Serializable]
    public sealed class ObjectGroup
    {
        public string id;
        public string name;
        public bool isTileGroup;
        public int order;
        public List<GameObject> objects = new List<GameObject>();
    }

    [Serializable]
    private sealed class VisibilityState
    {
        public GameObject target;
        public bool active;
    }

    [Tooltip("Model to organize. Defaults to this GameObject. Cameras and lights outside it are unaffected.")]
    [SerializeField] private Transform modelRoot;
    [SerializeField, HideInInspector] private List<ObjectGroup> groups = new List<ObjectGroup>();
    [SerializeField, HideInInspector] private List<VisibilityState> previousVisibility = new List<VisibilityState>();
    private bool refreshRequested;

    public IReadOnlyList<ObjectGroup> Groups => groups;
    public Transform ModelRoot => modelRoot != null ? modelRoot : transform;
    public bool CanRestoreVisibility => previousVisibility.Count > 0;

    private void OnEnable() => RefreshGroups();
    private void OnValidate() => refreshRequested = true;

    private void Update()
    {
        if (refreshRequested)
            RefreshGroups();
    }

    public void RefreshGroups()
    {
        // Read-only discovery: keep the current active states, including hidden garden objects.
        var byId = new Dictionary<string, ObjectGroup>();
        var seen = new HashSet<GameObject>();
        groups.Clear();
        foreach (Renderer renderer in ModelRoot.GetComponentsInChildren<Renderer>(true))
        {
            GameObject target;
            ObjectGroup description = Classify(renderer.transform, out target);
            if (!CanControl(target) || !seen.Add(target))
                continue;
            ObjectGroup group;
            if (!byId.TryGetValue(description.id, out group))
            {
                group = description;
                byId.Add(group.id, group);
                groups.Add(group);
            }
            group.objects.Add(target);
        }
        groups.Sort((left, right) => left.order.CompareTo(right.order));
        refreshRequested = false;
    }

    private ObjectGroup Classify(Transform item, out GameObject target)
    {
        target = item.gameObject;
        // Reuse existing semantic containers where possible, without reparenting the FBX.
        for (Transform ancestor = item; ancestor != null && ancestor != ModelRoot; ancestor = ancestor.parent)
        {
            string ancestorName = ancestor.name;
            if (StartsWith(ancestorName, "SCULPTURE"))
            {
                target = ancestor.gameObject;
                return Describe("sculpture", "Sculpture and pedestal", 20);
            }
            if (ancestorName.IndexOf("observed garden plant", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                target = ancestor.gameObject;
                return Describe("vegetation", "Trees and plants", 30);
            }
            if (ancestorName.IndexOf("timber support", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                target = ancestor.gameObject;
                return Describe("garden-supports", "Garden supports", 31);
            }
            if (ancestorName.IndexOf("fitted metal mast", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                target = ancestor.gameObject;
                return Describe("flagpoles", "Flagpoles", 33);
            }
        }

        string name = item.name;
        if (MudaacTileGroup.IsTileName(name))
        {
            MudaacTileGroup.Kind kind = MudaacTileGroup.Classify(name);
            return new ObjectGroup
            {
                id = "tiles:" + kind, name = new MudaacTileGroup(kind).Name,
                isTileGroup = true, order = (int)kind
            };
        }
        if (StartsWith(name, "Historic"))
            return Describe("historic-building", "Historic building - left of entrance", 22);
        if (StartsWith(name, "Glass front") || StartsWith(name, "Glass side")
            || StartsWith(name, "Glass vault") || StartsWith(name, "Vault ")
            || StartsWith(name, "Entrance door"))
            return Describe("glass-entrance", "Glass entrance and frame", 21);
        if (StartsWith(name, "Left glazing") || StartsWith(name, "Glass panel"))
            return Describe("other-glazing", "Other facade glazing", 23);
        if (StartsWith(name, "Roof guard rail") || StartsWith(name, "Roof rail upright"))
            return Describe("railings", "Roof railings", 25);
        if (StartsWith(name, "MUDAAC logo") || StartsWith(name, "Museum ") || StartsWith(name, "Plaque"))
            return Describe("signage", "Museum signs and plaques", 24);
        if (StartsWith(name, "Garden ground") || StartsWith(name, "Garden path"))
            return Describe("ground", "Garden ground and paths", 32);
        if (StartsWith(name, "Recess terrace"))
            return Describe("terrace", "Sculpture terrace", 34);
        return Describe("other", "Other objects", 99);
    }

    private static bool StartsWith(string name, string prefix) => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    private static ObjectGroup Describe(string id, string name, int order) => new ObjectGroup { id = id, name = name, order = order };

    private bool CanControl(GameObject target) => target != null && target.transform != ModelRoot
        && target.transform != transform && !transform.IsChildOf(target.transform);

    public int GetVisibleCount(ObjectGroup group)
    {
        int count = 0;
        foreach (GameObject target in group.objects)
            if (target != null && target.activeInHierarchy)
                count++;
        return count;
    }

    public void SetGroupVisible(string id, bool visible)
    {
        foreach (ObjectGroup group in groups)
            if (group.id == id)
            {
                SetVisible(group, visible);
                return;
            }
    }

    public void SetTilesVisible(bool visible)
    {
        foreach (ObjectGroup group in groups)
            if (group.isTileGroup)
                SetVisible(group, visible);
    }

    public void IsolateGroup(string id)
    {
        // Ignore stale IDs instead of unexpectedly hiding the entire model.
        if (!groups.Exists(group => group.id == id))
            return;
        CapturePreviousVisibility();
        foreach (ObjectGroup group in groups)
            SetVisible(group, group.id == id);
    }

    public void IsolateTiles()
    {
        CapturePreviousVisibility();
        foreach (ObjectGroup group in groups)
            SetVisible(group, group.isTileGroup);
    }

    public void ShowAll() => SetAllVisible(true);
    public void HideAll() => SetAllVisible(false);

    private void SetAllVisible(bool visible)
    {
        CapturePreviousVisibility();
        foreach (ObjectGroup group in groups)
            SetVisible(group, visible);
    }

    private void SetVisible(ObjectGroup group, bool visible)
    {
        foreach (GameObject target in group.objects)
        {
            if (!CanControl(target))
                continue;
            if (visible)
            {
                // A manually hidden intermediate container must not block a selected group.
                for (Transform ancestor = target.transform.parent; ancestor != null && ancestor != ModelRoot;
                    ancestor = ancestor.parent)
                    if (!ancestor.gameObject.activeSelf)
                        ancestor.gameObject.SetActive(true);
            }
            if (target.activeSelf != visible)
                target.SetActive(visible);
        }
    }

    private void CapturePreviousVisibility()
    {
        // Keep the state before the first isolate/bulk action across subsequent isolates and reloads.
        if (CanRestoreVisibility)
            return;
        foreach (Transform item in ModelRoot.GetComponentsInChildren<Transform>(true))
            if (CanControl(item.gameObject))
                previousVisibility.Add(new VisibilityState { target = item.gameObject, active = item.gameObject.activeSelf });
    }

    public void RestoreVisibility()
    {
        foreach (VisibilityState state in previousVisibility)
            if (CanControl(state.target))
                state.target.SetActive(state.active);
        previousVisibility.Clear();
    }

    // The Inspector uses these objects to record GameObject activation and manager state together.
    public List<UnityEngine.Object> GetVisibilityObjects()
    {
        var objects = new List<UnityEngine.Object> { this };
        var seen = new HashSet<GameObject>();
        foreach (Transform item in ModelRoot.GetComponentsInChildren<Transform>(true))
            if (CanControl(item.gameObject) && seen.Add(item.gameObject))
                objects.Add(item.gameObject);
        foreach (VisibilityState state in previousVisibility)
            if (CanControl(state.target) && seen.Add(state.target))
                objects.Add(state.target);
        return objects;
    }
}
