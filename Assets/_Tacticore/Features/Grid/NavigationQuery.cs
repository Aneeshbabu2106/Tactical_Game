using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
///     Single entry point for "can a unit stand on this cell". Movement, pathfinding and line of
///     sight should all go through here rather than reading tiles directly.
/// </summary>
public static class NavigationQuery
{
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

            case NavigationType.Door:
                return DoorRegistry.TryGet(cell, out var door) && door.IsOpen;

            case NavigationType.Wall:
            case NavigationType.Window:
            default:
                return false;
        }
    }

    public static bool Blocks(Tilemap tilemap, Vector3Int cell)
    {
        return !IsWalkable(tilemap, cell);
    }
}
