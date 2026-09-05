using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
///     What one pair of eyes can see, as a fan of rays across the field of view, each stopping at
///     the first thing that blocks sight. Ported from the prototype's castRay and visionRays.
/// </summary>
/// <remarks>
///     The output is a polygon, not a set of cells. Marking cells is what makes fog look like a
///     staircase: rays enter some cells along a wall and graze past their neighbours, so the reveal
///     comes out blocky and speckled. Filling the shape the rays actually describe is both smoother
///     and closer to the truth — the prototype paints the same fan into its SEEN canvas.
///     <para>
///         One fan serves the fog and the drawn cone, as the prototype notes: "One ray set per
///         visible unit per frame, shared by the fog and the cone bands."
///     </para>
/// </remarks>
public static class VisionFan
{
    /// <summary>Where one ray ended, evaluable at whatever depth the caller wants.</summary>
    /// <remarks>
    ///     The wall art does not fill its cell — Wall_Straight is 39% of a cell wide, centred, so
    ///     its visible face is about 0.3 in from the cell boundary where the ray stops. Anything
    ///     drawn to the raw hit point therefore ends short of the wall and leaves a gap. Callers
    ///     pass how deep to reach in, measured straight through the face the ray crossed rather
    ///     than along the ray — a shallow ray grazing up into a wall covers almost no height per
    ///     unit travelled, and would still stop short. Depth under a cell keeps the reach inside
    ///     the blocker, so it can never spill through onto the floor behind.
    /// </remarks>
    public readonly struct Hit
    {
        /// <summary>Cap on how far a reach may travel, for rays running nearly along a face.</summary>
        private const float MaxAlong = 3f;

        public Hit(Vector3 origin, float dx, float dy, float distance, float facing)
        {
            this.origin = origin;
            this.dx = dx;
            this.dy = dy;
            this.facing = facing;
            Distance = distance;
        }

        private readonly Vector3 origin;
        private readonly float dx;
        private readonly float dy;

        /// <summary>How much of the ray's travel is perpendicular to the face it crossed.</summary>
        private readonly float facing;

        /// <summary>Where sight actually stops.</summary>
        public readonly float Distance;

        /// <param name="depth">How far into the blocker to reach, measured through its face.</param>
        public Vector3 At(float depth)
        {
            // A ray running almost parallel to the face it crossed needs an unbounded distance to
            // gain any depth at all, so the travel is capped rather than the depth.
            var along = Mathf.Min(depth / Mathf.Max(facing, 0.0001f), MaxAlong);
            var reach = Distance + along;
            return new Vector3(origin.x + dx * reach, origin.y + dy * reach, origin.z);
        }
    }

    /// <summary>
    ///     Casts the fan, filling both rims from the one set of rays. They differ only in how far
    ///     each reaches into whatever blocked it — the mask wants the whole wall lit, the drawn cone
    ///     only wants to meet its face.
    /// </summary>
    public static void Cast(
        Tilemap map, Vector3 origin, float facingDegrees, float fovDegrees, float range,
        float stepDegrees, float coneCover, float maskCover,
        List<Vector3> coneRim, List<Vector3> maskRim)
    {
        coneRim?.Clear();
        maskRim?.Clear();

        if (map == null)
        {
            return;
        }

        var step = Mathf.Max(stepDegrees, 0.25f);
        var half = fovDegrees * 0.5f;

        for (var offset = -half; offset <= half + 0.0001f; offset += step)
        {
            var hit = CastRay(map, origin, facingDegrees + offset, range);
            coneRim?.Add(hit.At(coneCover));
            maskRim?.Add(hit.At(maskCover));
        }
    }

    /// <summary>
    ///     A full circle of rays. Used for the short sweep that keeps an operator from standing in
    ///     his own fog; nothing draws it, so only one rim is produced.
    /// </summary>
    public static void CastCircle(
        Tilemap map, Vector3 origin, float radius, float stepDegrees, float cover,
        List<Vector3> rim)
    {
        rim?.Clear();

        if (map == null || radius <= 0f)
        {
            return;
        }

        var step = Mathf.Max(stepDegrees, 1f);

        for (var angle = 0f; angle <= 360f; angle += step)
        {
            rim?.Add(CastRay(map, origin, angle, radius).At(cover));
        }
    }

    /// <summary>
    ///     Marches one ray cell to cell until something blocks sight or it runs out of range. A
    ///     12-cell ray costs about a dozen lookups this way rather than sixty samples.
    /// </summary>
    public static Hit CastRay(Tilemap map, Vector3 origin, float angleDegrees, float range)
    {
        var radians = angleDegrees * Mathf.Deg2Rad;
        var dx = Mathf.Cos(radians);
        var dy = Mathf.Sin(radians);

        var cell = map.WorldToCell(origin);

        // Square cells, which a rectangular tilemap grid gives us. The march steps in world units,
        // so a non-uniform cell size would skew the angles.
        var size = map.cellSize.x;
        var corner = map.CellToWorld(cell);

        var stepX = dx > 0f ? 1 : -1;
        var stepY = dy > 0f ? 1 : -1;

        var deltaX = Mathf.Approximately(dx, 0f) ? float.MaxValue : Mathf.Abs(size / dx);
        var deltaY = Mathf.Approximately(dy, 0f) ? float.MaxValue : Mathf.Abs(size / dy);

        var nextX = dx > 0f ? corner.x + size : corner.x;
        var nextY = dy > 0f ? corner.y + size : corner.y;

        var maxX = Mathf.Approximately(dx, 0f) ? float.MaxValue : (nextX - origin.x) / dx;
        var maxY = Mathf.Approximately(dy, 0f) ? float.MaxValue : (nextY - origin.y) / dy;

        var reached = range;
        var facing = 1f;
        var blocked = false;

        for (var guard = 0; guard < 512; guard++)
        {
            float travelled;
            float crossed;

            if (maxX < maxY)
            {
                travelled = maxX;
                crossed = Mathf.Abs(dx);
                cell.x += stepX;
                maxX += deltaX;
            }
            else
            {
                travelled = maxY;
                crossed = Mathf.Abs(dy);
                cell.y += stepY;
                maxY += deltaY;
            }

            if (travelled >= range)
            {
                break;
            }

            if (!SightQuery.BlocksSight(map, cell))
            {
                continue;
            }

            reached = travelled;
            facing = crossed;
            blocked = true;
            break;
        }

        // Nothing blocked it: there is no face to reach into, so At() must not extend it.
        return new Hit(origin, dx, dy, reached, blocked ? facing : float.MaxValue);
    }
}
