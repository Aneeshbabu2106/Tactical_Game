using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class TilemapTools
{
    /// <summary>
    ///     Erasing a tile does not refresh the neighbours it used to have — RuleTile only walks
    ///     neighbours from a tile that still exists — so gaps leave stale sprites behind.
    /// </summary>
    [MenuItem("Tacticore/Refresh Tilemaps")]
    private static void RefreshTilemaps()
    {
        var refreshed = 0;

        foreach (var tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            tilemap.RefreshAllTiles();
            refreshed++;
        }

        Debug.Log($"Refreshed {refreshed} tilemap(s).");
    }
}
