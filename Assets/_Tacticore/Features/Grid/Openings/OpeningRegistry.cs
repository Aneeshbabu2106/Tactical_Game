using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Cell -> opening lookup, populated when a rule tile spawns a door or window prefab. Static so
///     gameplay can ask about a cell without holding a scene reference.
/// </summary>
public static class OpeningRegistry
{
    private static readonly Dictionary<Vector3Int, Opening> Openings = new();

    public static IReadOnlyDictionary<Vector3Int, Opening> All => Openings;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Clear()
    {
        // Survives domain reload being disabled, which would carry stale openings into play mode.
        Openings.Clear();
    }

    public static void Register(Vector3Int cell, Opening opening)
    {
        Openings[cell] = opening;
    }

    public static void Unregister(Vector3Int cell, Opening opening)
    {
        if (Openings.TryGetValue(cell, out var existing) && existing == opening)
        {
            Openings.Remove(cell);
        }
    }

    public static bool TryGet(Vector3Int cell, out Opening opening)
    {
        return Openings.TryGetValue(cell, out opening);
    }
}
