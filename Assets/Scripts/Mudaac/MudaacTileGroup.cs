using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MudaacTileGroup
{
    public enum Kind
    {
        LeftFacade, LeftLowWall, LeftRoundedWall, LeftCantilever,
        EntranceCurve, EntranceFront, EntranceRecess, EntranceCorners,
        RightFacade, Undercroft, Other
    }

    [Serializable]
    public sealed class TileSelection
    {
        public Renderer renderer;
        public bool included = true;

        // Keep authored poses and visibility with the selection, including excluded
        // tiles. Preview output must never become the baseline after Undo or a reload.
        [SerializeField, HideInInspector] private Renderer restRenderer;
        [SerializeField, HideInInspector] private Vector3 restLocalPosition;
        [SerializeField, HideInInspector] private Quaternion restLocalRotation;
        [SerializeField, HideInInspector] private Vector3 restLocalScale;
        [SerializeField, HideInInspector] private bool restVisibilityCaptured;
        [SerializeField, HideInInspector] private bool restVisible;

        public TileSelection(Renderer renderer) => this.renderer = renderer;

        internal void EnsureRestPose()
        {
            if (renderer != null && restRenderer != renderer)
                CaptureRestPose();
            else if (renderer != null && !restVisibilityCaptured)
            {
                // Existing scene selections predate visibility snapshots.
                restVisible = renderer.enabled;
                restVisibilityCaptured = true;
            }
        }

        internal void CaptureRestPose()
        {
            restRenderer = renderer;
            restVisibilityCaptured = renderer != null;
            if (renderer == null)
                return;
            restVisible = renderer.enabled;
            Transform target = renderer.transform;
            restLocalPosition = target.localPosition;
            restLocalRotation = target.localRotation;
            restLocalScale = target.localScale;
        }

        internal void RestoreRestPose()
        {
            if (renderer == null || restRenderer != renderer)
                return;
            Transform target = renderer.transform;
            target.SetLocalPositionAndRotation(restLocalPosition, restLocalRotation);
            target.localScale = restLocalScale;
            if (restVisibilityCaptured && renderer.enabled != restVisible)
                renderer.enabled = restVisible;
        }

        internal MudaacTile CreateBinding()
        {
            EnsureRestPose();
            return new MudaacTile(renderer, restLocalPosition, restLocalRotation, restLocalScale, restVisible);
        }
    }

    public Kind kind;
    public bool included = true;
    public List<TileSelection> tiles = new List<TileSelection>();

    public string Name
    {
        get
        {
            switch (kind)
            {
                case Kind.LeftFacade: return "Left wing - main facade";
                case Kind.LeftLowWall: return "Left low wall";
                case Kind.LeftRoundedWall: return "Left lower rounded wall";
                case Kind.LeftCantilever: return "Left upper cantilever";
                case Kind.EntranceCurve: return "Entrance - curved facade";
                case Kind.EntranceFront: return "Entrance - front panels";
                case Kind.EntranceRecess: return "Entrance - recessed wall";
                case Kind.EntranceCorners: return "Entrance - curved recess sides";
                case Kind.RightFacade: return "Right wing - main facade";
                case Kind.Undercroft: return "Undercroft panels";
                default: return "Other tiles";
            }
        }
    }

    public MudaacTileGroup(Kind kind)
    {
        this.kind = kind;
        included = kind != Kind.Undercroft;
    }

    public static bool IsTileName(string name) => name.StartsWith("Concrete panel", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Undercroft panel", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Left low wall", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Left lower rounded wall", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Left upper cantilever", StringComparison.OrdinalIgnoreCase);

    public static Kind Classify(string name)
    {
        if (name.StartsWith("Undercroft panel", StringComparison.OrdinalIgnoreCase))
            return Kind.Undercroft;
        if (name.StartsWith("Left lower rounded wall", StringComparison.OrdinalIgnoreCase))
            return Kind.LeftRoundedWall;
        if (name.StartsWith("Left upper cantilever", StringComparison.OrdinalIgnoreCase))
            return Kind.LeftCantilever;
        if (name.StartsWith("Left low wall", StringComparison.OrdinalIgnoreCase))
            return Kind.LeftLowWall;

        const string concretePrefix = "Concrete panel";
        int panel;
        if (!name.StartsWith(concretePrefix, StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(name.Substring(concretePrefix.Length).Trim(), out panel))
            return Kind.Other;

        // Stable panel IDs in the Mudaac FBX identify architectural sections.
        // Membership is independent of the model's rotation, scale and tile effects.
        if (panel >= 0 && panel <= 23) return Kind.EntranceCurve;
        if (panel >= 51 && panel <= 62) return Kind.EntranceFront;
        if (panel >= 63 && panel <= 74) return Kind.EntranceRecess;
        if (panel >= 75 && panel <= 86) return Kind.EntranceCorners;
        if (panel >= 97 && panel <= 158) return Kind.RightFacade;
        if (panel >= 173 && panel <= 232) return Kind.LeftFacade;
        return Kind.Other;
    }
}
