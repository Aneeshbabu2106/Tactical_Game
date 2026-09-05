using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
///     How loud a noise is by the time it reaches a listener. The hearing counterpart to
///     <see cref="SightQuery" />, and a third answer again: glass muffles sound while being
///     perfectly transparent, and an opened door leaks sound it used to hold back.
/// </summary>
/// <remarks>
///     Ported from the prototype's heardIntensity/soundPathAtten. Two things eat loudness: distance,
///     linearly, and whatever the sound passes through, subtractively. There is no radius — every
///     listener is evaluated on its own line, which is what lets sound round a corner through a door
///     you kicked and not through the wall beside it.
///     <para>
///         The prototype attenuates per wall <em>edge</em>; our world is tiles, so a wall is a cell
///         and the same numbers apply per cell crossed. The march is the one
///         <see cref="VisionFan.CastRay" /> uses, so sight and sound cross the map the same way.
///     </para>
/// </remarks>
public static class SoundQuery
{
    /// <summary>Loudness lost per cell travelled.</summary>
    public const float DistanceAttenuation = 0.6f;

    /// <summary>Above this the listener knows where it came from and can go and look.</summary>
    public const float HearLocate = 4.0f;

    /// <summary>Above this he only knows roughly which way to turn.</summary>
    public const float HearNotice = 1.0f;

    private const float Wall = 6.0f;
    private const float ClosedDoor = 2.0f;
    private const float IntactWindow = 1.5f;

    /// <summary>What a cell takes out of a sound passing through it.</summary>
    public static float Attenuation(Tilemap tilemap, Vector3Int cell)
    {
        if (tilemap == null)
        {
            return Wall;
        }

        // An empty cell is outside the building shell; treat it as solid, as sight does.
        if (tilemap.GetTile(cell) is not NavigationRuleTile navigation)
        {
            return Wall;
        }

        switch (navigation.Type)
        {
            case NavigationType.Walkable:
                return 0f;

            // Both open once they are worked: a broken window and an opened door stop muffling.
            case NavigationType.Window:
                return Passable(cell) ? 0f : IntactWindow;

            case NavigationType.Door:
                return Passable(cell) ? 0f : ClosedDoor;

            case NavigationType.Wall:
            default:
                return Wall;
        }
    }

    /// <summary>
    ///     What is left of <paramref name="loudness" /> at <paramref name="to" />. Zero means the
    ///     listener heard nothing at all.
    /// </summary>
    public static float HeardIntensity(Tilemap tilemap, Vector3 from, Vector3 to, float loudness)
    {
        var dx = to.x - from.x;
        var dy = to.y - from.y;
        var distance = Mathf.Sqrt(dx * dx + dy * dy);

        var carried = loudness - distance * DistanceAttenuation;

        // Cheap out before walking any cells: distance alone already killed it.
        if (carried <= HearNotice)
        {
            return 0f;
        }

        return Mathf.Max(0f, carried - PathAttenuation(tilemap, from, to, distance, dx, dy));
    }

    /// <summary>Whether a listener at that intensity can place the sound rather than just notice it.</summary>
    public static bool CanLocate(float intensity)
    {
        return intensity > HearLocate;
    }

    public static bool CanNotice(float intensity)
    {
        return intensity > HearNotice;
    }

    /// <summary>
    ///     Sums every cell strictly between the two points. Both end cells are skipped: a man
    ///     standing in a doorway is not muffled by his own doorway, and neither is the sound by the
    ///     cell it was made in.
    /// </summary>
    private static float PathAttenuation(
        Tilemap tilemap, Vector3 from, Vector3 to, float distance, float dx, float dy)
    {
        if (tilemap == null || distance < 0.0001f)
        {
            return 0f;
        }

        dx /= distance;
        dy /= distance;

        var cell = tilemap.WorldToCell(from);
        var goal = tilemap.WorldToCell(to);

        var size = tilemap.cellSize.x;
        var corner = tilemap.CellToWorld(cell);

        var stepX = dx > 0f ? 1 : -1;
        var stepY = dy > 0f ? 1 : -1;

        var deltaX = Mathf.Approximately(dx, 0f) ? float.MaxValue : Mathf.Abs(size / dx);
        var deltaY = Mathf.Approximately(dy, 0f) ? float.MaxValue : Mathf.Abs(size / dy);

        var nextX = dx > 0f ? corner.x + size : corner.x;
        var nextY = dy > 0f ? corner.y + size : corner.y;

        var maxX = Mathf.Approximately(dx, 0f) ? float.MaxValue : (nextX - from.x) / dx;
        var maxY = Mathf.Approximately(dy, 0f) ? float.MaxValue : (nextY - from.y) / dy;

        var total = 0f;

        for (var guard = 0; guard < 512; guard++)
        {
            float travelled;

            if (maxX < maxY)
            {
                travelled = maxX;
                cell.x += stepX;
                maxX += deltaX;
            }
            else
            {
                travelled = maxY;
                cell.y += stepY;
                maxY += deltaY;
            }

            if (travelled >= distance || cell == goal)
            {
                break;
            }

            total += Attenuation(tilemap, cell);
        }

        return total;
    }

    private static bool Passable(Vector3Int cell)
    {
        return OpeningRegistry.TryGet(cell, out var opening) && opening.IsPassable;
    }
}
