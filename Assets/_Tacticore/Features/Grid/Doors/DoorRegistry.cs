using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Cell -> door lookup, populated when <see cref="DoorRuleTile" /> spawns a door prefab.
///     Static so gameplay can query a cell without holding a scene reference.
/// </summary>
public static class DoorRegistry
{
    private static readonly Dictionary<Vector3Int, Door> Doors = new();

    public static IReadOnlyDictionary<Vector3Int, Door> All => Doors;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Clear()
    {
        // Survives domain reload being disabled, which would carry stale doors into play mode.
        Doors.Clear();
    }

    public static void Register(Vector3Int cell, Door door)
    {
        Doors[cell] = door;
    }

    public static void Unregister(Vector3Int cell, Door door)
    {
        if (Doors.TryGetValue(cell, out var existing) && existing == door)
        {
            Doors.Remove(cell);
        }
    }

    public static bool TryGet(Vector3Int cell, out Door door)
    {
        return Doors.TryGetValue(cell, out door);
    }
}
