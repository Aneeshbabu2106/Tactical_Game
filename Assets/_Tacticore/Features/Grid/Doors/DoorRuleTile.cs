using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(
    fileName = "DoorRuleTile",
    menuName = "Tacticore/Rule Tiles/Door"
)]
public class DoorRuleTile : NavigationRuleTile
{
    public override NavigationType Type => NavigationType.Door;

    public override bool StartUp(Vector3Int position, ITilemap tilemap, GameObject instantiatedGameObject)
    {
        // Base does the transform placement for the spawned prefab, so let it run first.
        var result = base.StartUp(position, tilemap, instantiatedGameObject);

        if (instantiatedGameObject != null && instantiatedGameObject.TryGetComponent(out Door door))
        {
            door.Place(position, IsVerticalAt(position, tilemap));
        }

        return result;
    }

    /// <summary>
    ///     A door with wall-like neighbors above and below sits in a vertical wall run. Derived from
    ///     the tilemap rather than from which rule matched, which RuleTile does not expose.
    /// </summary>
    private static bool IsVerticalAt(Vector3Int position, ITilemap tilemap)
    {
        return IsWallLike(tilemap.GetTile(position + Vector3Int.up))
               && IsWallLike(tilemap.GetTile(position + Vector3Int.down));
    }
}
