using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
///     Single entry point for "can a unit stand on this cell". Movement, pathfinding and line of
///     sight should all go through here rather than reading tiles directly.
/// </summary>
public static class NavigationQuery
{
    /// <summary>Default spacing of the samples taken along a segment, in cells.</summary>
    public const float DefaultStep = 0.2f;

    /// <summary>
    ///     An empty cell is outside the building shell and counts as not walkable.
    /// </summary>
    public static bool IsWalkable(Tilemap tilemap, Vector3Int cell)
    {
        if (tilemap == null)
        {
            return false;
        }

        return IsWalkable(tilemap.GetTile(cell), cell);
    }

    /// <summary>
    ///     Overload for callers that already hold the tile — a bulk pass over a region should fetch
    ///     tiles once with <see cref="Tilemap.GetTilesBlock" /> rather than paying a GetTile per query.
    /// </summary>
    public static bool IsWalkable(TileBase tile, Vector3Int cell)
    {
        if (tile is not NavigationRuleTile navigation)
        {
            return false;
        }

        switch (navigation.Type)
        {
            case NavigationType.Walkable:
                return true;

            // Both ask the same question — an open door and a broken window are equally a gap.
            case NavigationType.Door:
            case NavigationType.Window:
                return OpeningRegistry.TryGet(cell, out var opening) && opening.IsPassable;

            case NavigationType.Wall:
            default:
                return false;
        }
    }

    public static bool Blocks(Tilemap tilemap, Vector3Int cell)
    {
        return !IsWalkable(tilemap, cell);
    }

    /// <summary>
    ///     Whether a straight run stays on walkable ground the whole way. Sampled rather than
    ///     rasterised: at a fifth of a cell no wall is thin enough to be stepped over, and the
    ///     callers are all per-frame input paths.
    /// </summary>
    public static bool SegmentIsWalkable(Tilemap tilemap, Vector3 from, Vector3 to, float step = DefaultStep)
    {
        if (tilemap == null)
        {
            return false;
        }

        var length = Vector3.Distance(from, to);
        var steps = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(step, 0.01f)));

        for (var i = 0; i <= steps; i++)
        {
            var point = Vector3.Lerp(from, to, i / (float)steps);

            if (!IsWalkable(tilemap, tilemap.WorldToCell(point)))
            {
                return false;
            }
        }

        return true;
    }
}
