using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
///     Single entry point for "can a unit see past this cell". The sight counterpart to
///     <see cref="NavigationQuery" />, and deliberately a different question.
/// </summary>
/// <remarks>
///     The prototype keeps blocksLOS as its own table beside blocksMove for the same reason: the
///     two disagree in both directions. Glass can be seen through but not walked through; an open
///     doorway is both. Answering sight with the walkability check would wall off every window.
/// </remarks>
public static class SightQuery
{
    /// <summary>An empty cell is outside the building shell, and nothing is seen past it.</summary>
    public static bool BlocksSight(Tilemap tilemap, Vector3Int cell)
    {
        if (tilemap == null)
        {
            return true;
        }

        return BlocksSight(tilemap.GetTile(cell), cell);
    }

    /// <summary>
    ///     Overload for callers holding the tile already, matching
    ///     <see cref="NavigationQuery.IsWalkable(TileBase, Vector3Int)" />.
    /// </summary>
    public static bool BlocksSight(TileBase tile, Vector3Int cell)
    {
        if (tile is not NavigationRuleTile navigation)
        {
            return true;
        }

        switch (navigation.Type)
        {
            case NavigationType.Walkable:
                return false;

            // Glass is transparent whether or not it is broken. Breaking a window changes what can
            // be walked through, never what can be seen through.
            case NavigationType.Window:
                return false;

            case NavigationType.Door:
                return !(OpeningRegistry.TryGet(cell, out var opening) && opening.IsPassable);

            case NavigationType.Wall:
            default:
                return true;
        }
    }
}
