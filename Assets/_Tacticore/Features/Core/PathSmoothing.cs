using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Turns a polyline into a smooth curve through its points, for drawing paths as a stroke rather
///     than a chain of segments.
/// </summary>
/// <remarks>
///     Centripetal Catmull-Rom (alpha 0.5), not uniform. Uniform Catmull-Rom overshoots and can loop
///     on sharp corners, which on a path hugging a wall would visibly bulge through it. Centripetal
///     is provably cusp and self-intersection free, so the curve stays inside the drawn corner.
/// </remarks>
public static class PathSmoothing
{
    private const float Alpha = 0.5f;
    private const float MinSpan = 1e-4f;

    /// <summary>
    ///     Writes the smoothed curve into <paramref name="into" />, which is cleared first. Pass a
    ///     reusable list; this runs every frame while a path is drawn.
    /// </summary>
    public static void Smooth(IReadOnlyList<Vector3> points, List<Vector3> into, int samplesPerSegment)
    {
        into.Clear();

        if (points == null || points.Count == 0)
        {
            return;
        }

        // Fewer than three points cannot curve, and one sample per segment is just the polyline.
        if (points.Count < 3 || samplesPerSegment < 2)
        {
            for (var i = 0; i < points.Count; i++)
            {
                into.Add(points[i]);
            }

            return;
        }

        for (var i = 0; i < points.Count - 1; i++)
        {
            // Clamp the ends so the curve starts and finishes exactly on the first and last point.
            var p0 = points[Mathf.Max(i - 1, 0)];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = points[Mathf.Min(i + 2, points.Count - 1)];

            var t0 = 0f;
            var t1 = t0 + Span(p0, p1);
            var t2 = t1 + Span(p1, p2);
            var t3 = t2 + Span(p2, p3);

            for (var s = 0; s < samplesPerSegment; s++)
            {
                var t = Mathf.Lerp(t1, t2, s / (float)samplesPerSegment);
                into.Add(Evaluate(p0, p1, p2, p3, t0, t1, t2, t3, t));
            }
        }

        into.Add(points[points.Count - 1]);
    }

    /// <summary>
    ///     Knot spacing by distance raised to alpha. Floored so coincident points cannot produce a
    ///     zero span, which would divide by zero in the interpolation below.
    /// </summary>
    private static float Span(Vector3 a, Vector3 b)
    {
        return Mathf.Pow(Mathf.Max(Vector3.Distance(a, b), MinSpan), Alpha);
    }

    /// <summary>Barry-Goldman pyramidal form, which handles the non-uniform knots directly.</summary>
    private static Vector3 Evaluate(
        Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
        float t0, float t1, float t2, float t3, float t)
    {
        var a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
        var a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
        var a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;

        var b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
        var b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;

        return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
    }
}
