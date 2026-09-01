using System;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum NavigationType
{
    None,
    Wall,
    Door,
    Window,
    Walkable
}

/// <summary>
///     Base for tiles that make up the building shell. Adds the navigation neighbor vocabulary on
///     top of <see cref="ExtendedRuleTile" />.
/// </summary>
public abstract class NavigationRuleTile : ExtendedRuleTile
{
    /// <summary>
    ///     Ids start at <see cref="ExtendedRuleTile.FirstDerivedNeighbor" /> and must stay unique
    ///     against everything inherited. <see cref="ExtendedRuleTile.FindDuplicateNeighborIds" />
    ///     verifies that on editor load rather than leaving it to discipline.
    /// </summary>
    public class NavigationNeighbor : Neighbor
    {
        public const int Walkable = FirstDerivedNeighbor;      // 5
        public const int Wall = FirstDerivedNeighbor + 1;      // 6
        public const int Door = FirstDerivedNeighbor + 2;      // 7
        public const int Window = FirstDerivedNeighbor + 3;    // 8

        // Anything that continues a wall run: Wall, Door or Window.
        public const int WallLike = FirstDerivedNeighbor + 4;    // 9
        public const int NotWallLike = FirstDerivedNeighbor + 5; // 10
    }

    public abstract NavigationType Type { get; }

    /// <summary>
    ///     Hands a spawned <see cref="Opening" /> its cell. Doors and windows both arrive this way,
    ///     so neither tile type needs an override of its own.
    /// </summary>
    public override bool StartUp(Vector3Int position, ITilemap tilemap, GameObject instantiatedGameObject)
    {
        // Base does the transform placement for the spawned prefab, so let it run first.
        var result = base.StartUp(position, tilemap, instantiatedGameObject);

        if (instantiatedGameObject != null && instantiatedGameObject.TryGetComponent(out Opening opening))
        {
            opening.Place(position, IsVerticalAt(position, tilemap));
        }

        return result;
    }

    /// <summary>
    ///     A cell with wall-like neighbours above and below sits in a vertical wall run, and is
    ///     therefore crossed east to west. Derived from the tilemap rather than from which rule
    ///     matched, which RuleTile does not expose.
    /// </summary>
    protected static bool IsVerticalAt(Vector3Int position, ITilemap tilemap)
    {
        return IsWallLike(tilemap.GetTile(position + Vector3Int.up))
               && IsWallLike(tilemap.GetTile(position + Vector3Int.down));
    }

    public override Type NeighborVocabulary => typeof(NavigationNeighbor);

    public override bool RuleMatch(int neighbor, TileBase tile)
    {
        switch (neighbor)
        {
            case NavigationNeighbor.Walkable:
                return IsType(tile, NavigationType.Walkable);

            case NavigationNeighbor.Wall:
                return IsType(tile, NavigationType.Wall);

            case NavigationNeighbor.Door:
                return IsType(tile, NavigationType.Door);

            case NavigationNeighbor.Window:
                return IsType(tile, NavigationType.Window);

            case NavigationNeighbor.WallLike:
                return IsWallLike(tile);

            case NavigationNeighbor.NotWallLike:
                return !IsWallLike(tile);
        }

        return base.RuleMatch(neighbor, tile);
    }

    /// <summary>
    ///     Matches on the tile's declared <see cref="NavigationType" /> rather than its C# type,
    ///     so variant assets of the same kind keep matching.
    /// </summary>
    public static bool IsType(TileBase tile, NavigationType type)
    {
        return tile is NavigationRuleTile navigation && navigation.Type == type;
    }

    public static bool IsWallLike(TileBase tile)
    {
        if (tile is not NavigationRuleTile navigation)
        {
            return false;
        }

        return navigation.Type == NavigationType.Wall
               || navigation.Type == NavigationType.Door
               || navigation.Type == NavigationType.Window;
    }
}
