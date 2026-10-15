using System.Collections.Generic;
using UnityEngine;

/// <summary>Cached authored surface geometry and deterministic tile arrival values.</summary>
public sealed class MudaacPaintPropagation
{
    public sealed class Surface
    {
        public MudaacTile Tile;
        public Matrix4x4 RestMatrix;
        public Bounds Bounds;
        public Vector3 Center;
        public float Distance;
        public float RandomDistance;
        public float ArrivalTime = float.NaN;
    }

    private readonly Dictionary<MudaacTile, Surface> byTile = new Dictionary<MudaacTile, Surface>();
    private Surface[] surfaces = new Surface[0];
    public IReadOnlyList<Surface> Surfaces => surfaces;
    public float MaximumDistance { get; private set; }
    public Bounds FacadeBounds { get; private set; }

    public Surface Find(MudaacTile tile)
    {
        Surface surface;
        return byTile.TryGetValue(tile, out surface) ? surface : null;
    }

    public bool Matches(IReadOnlyList<MudaacTile> tiles)
    {
        if (tiles.Count != surfaces.Length)
            return false;
        for (int i = 0; i < tiles.Count; i++)
            if (surfaces[i].Tile != tiles[i] || !tiles[i].IsValid || surfaces[i].RestMatrix != GetRestMatrix(tiles[i]))
                return false;
        return true;
    }

    public static Matrix4x4 GetRestMatrix(MudaacTile tile)
    {
        Matrix4x4 local = Matrix4x4.TRS(tile.RestLocalPosition, tile.RestLocalRotation, tile.RestLocalScale);
        return tile.Transform.parent != null ? tile.Transform.parent.localToWorldMatrix * local : local;
    }

    public void Rebuild(IReadOnlyList<MudaacTile> tiles, IReadOnlyList<Vector3> origins, MudaacNoiseSettings noise)
    {
        byTile.Clear();
        MaximumDistance = 0f;
        surfaces = new Surface[tiles.Count];
        Bounds facadeBounds = new Bounds();
        bool hasBounds = false;
        for (int i = 0; i < tiles.Count; i++)
        {
            MudaacTile tile = tiles[i];
            var surface = new Surface { Tile = tile };
            surfaces[i] = surface;
            if (!tile.IsValid)
                continue;
            surface.RestMatrix = GetRestMatrix(tile);
            surface.Center = tile.RestWorldCenter;
            surface.Distance = DistanceToOrigins(surface.Center, origins);
            Bounds local = tile.Renderer.localBounds;
            surface.Bounds = new Bounds(surface.Center, Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = Vector3.Scale(local.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f));
                Vector3 point = surface.RestMatrix.MultiplyPoint3x4(local.center + offset);
                surface.Bounds.Encapsulate(point);
            }
            if (!hasBounds)
            {
                facadeBounds = surface.Bounds;
                hasBounds = true;
            }
            else
                facadeBounds.Encapsulate(surface.Bounds);
            byTile[tile] = surface;
        }
        // A facade-wide extent keeps normalized progress independent of mesh partitioning.
        if (hasBounds)
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = Vector3.Scale(facadeBounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                MaximumDistance = Mathf.Max(MaximumDistance, DistanceToOrigins(facadeBounds.center + offset, origins));
            }
        if (hasBounds && origins.Count > 1)
            MaximumDistance = Mathf.Max(MaximumDistance,
                DistanceToOrigins(facadeBounds.center, origins) + facadeBounds.extents.magnitude);
        FacadeBounds = facadeBounds;
        MaximumDistance = Mathf.Max(0.001f, MaximumDistance);
        foreach (Surface surface in surfaces)
            if (surface.Tile.IsValid)
                surface.RandomDistance = noise.Sample01(surface.Center, 13.173f, 0f, 0f, 101) * MaximumDistance;
        // Every origin still starts on its nearest tile at the randomized end of the slider.
        for (int source = 0; source < origins.Count; source++)
        {
            Surface nearest = null;
            float closest = float.PositiveInfinity;
            foreach (Surface surface in surfaces)
            {
                if (!surface.Tile.IsValid)
                    continue;
                float distance = (surface.Bounds.ClosestPoint(origins[source]) - origins[source]).sqrMagnitude;
                if (distance < closest)
                {
                    closest = distance;
                    nearest = surface;
                }
            }
            if (nearest != null)
                nearest.RandomDistance = 0f;
        }
    }

    private static float DistanceToOrigins(Vector3 point, IReadOnlyList<Vector3> origins)
    {
        float distance = float.PositiveInfinity;
        for (int i = 0; i < origins.Count; i++)
            distance = Mathf.Min(distance, Vector3.Distance(point, origins[i]));
        return float.IsInfinity(distance) ? 0f : distance;
    }
}
