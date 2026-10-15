using System.Collections.Generic;
using UnityEngine;

// Neighboring tiles evaluate the same nearby segments, including T junctions.
public sealed class MudaacLiquidSeamNetwork
{
    public const int MaximumEdges = 128;
    public const int MaximumCellTiles = 32;

    public sealed class Neighborhood
    {
        public readonly Vector4[] Edges = new Vector4[MaximumEdges];
        public int Count;
        public readonly Vector4[] CellRects = new Vector4[MaximumCellTiles];
        public readonly Vector4[] CellSeeds = new Vector4[MaximumCellTiles];
        public int CellCount;
    }

    private readonly Dictionary<MudaacTile, Neighborhood> neighborhoods = new Dictionary<MudaacTile, Neighborhood>();
    private readonly List<Vector4> edges = new List<Vector4>();
    private readonly List<Vector4> candidates = new List<Vector4>();
    private readonly List<KeyValuePair<MudaacTile, Vector4>> cellCandidates = new List<KeyValuePair<MudaacTile, Vector4>>();

    public Neighborhood Find(MudaacTile tile)
    {
        Neighborhood value;
        return neighborhoods.TryGetValue(tile, out value) ? value : null;
    }

    public void Rebuild(Dictionary<MudaacTile, Vector4> footprints, float reach)
    {
        neighborhoods.Clear();
        edges.Clear();
        foreach (Vector4 rect in footprints.Values)
        {
            float x0 = rect.x - rect.z, x1 = rect.x + rect.z;
            float y0 = rect.y - rect.w, y1 = rect.y + rect.w;
            AddEdge(new Vector4(x0, y0, x1, y0));
            AddEdge(new Vector4(x0, y1, x1, y1));
            AddEdge(new Vector4(x0, y0, x0, y1));
            AddEdge(new Vector4(x1, y0, x1, y1));
        }
        foreach (var pair in footprints)
        {
            var neighborhood = new Neighborhood();
            Vector4 rect = pair.Value;
            candidates.Clear();
            foreach (Vector4 edge in edges)
                if (DistanceSquared(rect, edge) <= reach * reach)
                    candidates.Add(edge);
            if (candidates.Count > MaximumEdges)
                candidates.Sort((a, b) => DistanceSquared(rect, a).CompareTo(DistanceSquared(rect, b)));
            neighborhood.Count = Mathf.Min(MaximumEdges, candidates.Count);
            for (int i = 0; i < neighborhood.Count; i++)
                neighborhood.Edges[i] = candidates[i];
            cellCandidates.Clear();
            foreach (var cell in footprints)
            {
                Vector4 other = cell.Value;
                // Rectangle-to-rectangle distance, using the same segment bounds calculation.
                var diagonal = new Vector4(other.x - other.z, other.y - other.w, other.x + other.z, other.y + other.w);
                if (DistanceSquared(rect, diagonal) <= reach * reach)
                    cellCandidates.Add(cell);
            }
            if (cellCandidates.Count > MaximumCellTiles)
                cellCandidates.Sort((a, b) => CenterDistanceSquared(rect, a.Value).CompareTo(CenterDistanceSquared(rect, b.Value)));
            neighborhood.CellCount = Mathf.Min(MaximumCellTiles, cellCandidates.Count);
            for (int i = 0; i < neighborhood.CellCount; i++)
            {
                var cell = cellCandidates[i];
                neighborhood.CellRects[i] = cell.Value;
                neighborhood.CellSeeds[i] = new Vector4(TileSeed(cell.Key.Renderer.name), 0f, 0f, 0f);
            }
            neighborhoods[pair.Key] = neighborhood;
        }
    }

    // Stable across refreshes and player launches; string.GetHashCode is not guaranteed to be.
    public static int TileSeed(string name)
    {
        uint hash = 2166136261;
        for (int i = 0; i < name.Length; i++)
            hash = unchecked((hash ^ name[i]) * 16777619);
        return (int)(hash % 10000);
    }

    private static float CenterDistanceSquared(Vector4 a, Vector4 b)
    {
        float x = a.x - b.x, y = a.y - b.y;
        return x * x + y * y;
    }

    private void AddEdge(Vector4 edge)
    {
        // Shared edges of touching panels need only one entry. Parallel edges around a physical gap remain distinct.
        for (int i = 0; i < edges.Count; i++)
            if ((edges[i] - edge).sqrMagnitude < 0.00000001f)
                return;
        edges.Add(edge);
    }

    private static float DistanceSquared(Vector4 rect, Vector4 edge)
    {
        float dx = Mathf.Max(0f, Mathf.Max(rect.x - rect.z - Mathf.Max(edge.x, edge.z),
            Mathf.Min(edge.x, edge.z) - rect.x - rect.z));
        float dy = Mathf.Max(0f, Mathf.Max(rect.y - rect.w - Mathf.Max(edge.y, edge.w),
            Mathf.Min(edge.y, edge.w) - rect.y - rect.w));
        return dx * dx + dy * dy;
    }
}
