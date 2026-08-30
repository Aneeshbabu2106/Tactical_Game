using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Polyline helpers shared by the path model and everything that draws or hit-tests a path.
/// </summary>
public static class PathGeometry
{
    /// <summary>
    ///     Nearest position on the polyline to <paramref name="world" />, projected onto segments
    ///     rather than snapped to vertices, so hovering a long straight gives a sensible point.
    ///     <paramref name="index" /> is the nearer end of the segment it landed on.
    /// </summary>
    public static bool ClosestPoint(
        IReadOnlyList<Vector3> points, Vector3 world,
        out int index, out Vector3 position, out float distance)
    {
        index = -1;
        position = world;
        distance = float.MaxValue;

        if (points == null || points.Count == 0)
        {
            return false;
        }

        if (points.Count == 1)
        {
            index = 0;
            position = points[0];
            distance = Vector2.Distance(points[0], world);
            return true;
        }

        for (var i = 1; i < points.Count; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            var ab = b - a;
            var lengthSquared = ab.sqrMagnitude;

            var t = lengthSquared > 0f
                ? Mathf.Clamp01(Vector3.Dot(world - a, ab) / lengthSquared)
                : 0f;

            var onSegment = a + ab * t;
            var d = Vector2.Distance(onSegment, world);

            if (d >= distance)
            {
                continue;
            }

            distance = d;
            position = onSegment;
            index = t < 0.5f ? i - 1 : i;
        }

        return index >= 0;
    }

    /// <summary>Arc length between two point indices, in either order.</summary>
    public static float ArcLength(IReadOnlyList<Vector3> points, int from, int to)
    {
        if (from > to)
        {
            (from, to) = (to, from);
        }

        var total = 0f;

        for (var i = from + 1; i <= to && i < points.Count; i++)
        {
            total += Vector3.Distance(points[i - 1], points[i]);
        }

        return total;
    }
}
