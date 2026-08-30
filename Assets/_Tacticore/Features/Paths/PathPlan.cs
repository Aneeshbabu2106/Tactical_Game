using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     A drawn path plus the waypoints annotating it. The stroke stays the route — waypoints are
///     anchors on it, not control points it is generated from.
/// </summary>
/// <remarks>
///     The prototype re-paths between waypoints with A*, so there its waypoints define the route.
///     With no pathfinder here, regenerating a leg would mean a straight line that cuts wall
///     corners, so the drawn points remain authoritative and a waypoint drag deforms them instead.
/// </remarks>
public class PathPlan
{
    public List<Vector3> Points { get; } = new();

    /// <summary>Ordered by <see cref="Waypoint.PointIndex" />, ascending.</summary>
    public List<Waypoint> Waypoints { get; } = new();

    public bool IsEmpty => Points.Count == 0;

    public void Clear()
    {
        Points.Clear();
        Waypoints.Clear();
    }

    public void SetPoints(IReadOnlyList<Vector3> points)
    {
        Points.Clear();
        Points.AddRange(points);
        Waypoints.Clear();
    }

    /// <summary>
    ///     Drops waypoints where the stroke actually turns. Accumulated heading change rather than
    ///     the prototype's flat distance rule, which would scatter them along straights.
    /// </summary>
    /// <param name="turnThreshold">Degrees of accumulated turn that earns a waypoint.</param>
    /// <param name="minSpacing">Floor on spacing, so a shaky hand cannot produce a cluster.</param>
    /// <param name="maxSpacing">Ceiling on spacing, so a long straight still gets one.</param>
    public void AutoPlace(float turnThreshold, float minSpacing, float maxSpacing)
    {
        Waypoints.Clear();

        if (Points.Count < 2)
        {
            return;
        }

        var turn = 0f;
        var since = 0f;

        for (var i = 1; i < Points.Count - 1; i++)
        {
            since += Vector3.Distance(Points[i - 1], Points[i]);

            var before = Points[i] - Points[i - 1];
            var after = Points[i + 1] - Points[i];

            if (before.sqrMagnitude > 0f && after.sqrMagnitude > 0f)
            {
                turn += Mathf.Abs(Mathf.DeltaAngle(
                    Mathf.Atan2(before.y, before.x) * Mathf.Rad2Deg,
                    Mathf.Atan2(after.y, after.x) * Mathf.Rad2Deg));
            }

            var corner = turn >= turnThreshold && since >= minSpacing;
            var overdue = maxSpacing > 0f && since >= maxSpacing;

            if (!corner && !overdue)
            {
                continue;
            }

            Waypoints.Add(new Waypoint { PointIndex = i });
            turn = 0f;
            since = 0f;
        }

        // The destination is always a waypoint; it is what the operator is walking to.
        Add(Points.Count - 1);
    }

    /// <summary>
    ///     Attaches a waypoint to an existing point. Nothing is inserted into Points, so no other
    ///     waypoint's index can shift — the stroke is dense enough that snapping to the nearest
    ///     sample is imperceptible, and it removes a whole class of index bugs.
    /// </summary>
    public Waypoint Add(int pointIndex)
    {
        if (Points.Count == 0)
        {
            return null;
        }

        pointIndex = Mathf.Clamp(pointIndex, 0, Points.Count - 1);

        var at = Waypoints.FindIndex(w => w.PointIndex == pointIndex);

        if (at >= 0)
        {
            return Waypoints[at];
        }

        var waypoint = new Waypoint { PointIndex = pointIndex };
        var insert = Waypoints.FindIndex(w => w.PointIndex > pointIndex);

        if (insert < 0)
        {
            Waypoints.Add(waypoint);
        }
        else
        {
            Waypoints.Insert(insert, waypoint);
        }

        return waypoint;
    }

    public bool Remove(Waypoint waypoint)
    {
        return Waypoints.Remove(waypoint);
    }

    /// <summary>
    ///     The stretch of path a waypoint owns: from the previous waypoint to the next one, or to
    ///     the ends of the path where there is no neighbour. Nothing outside this span may move when
    ///     the waypoint does.
    /// </summary>
    public void GetSpan(Waypoint waypoint, out int from, out int to)
    {
        var index = Waypoints.IndexOf(waypoint);

        from = index > 0 ? Waypoints[index - 1].PointIndex : 0;
        to = index >= 0 && index < Waypoints.Count - 1 ? Waypoints[index + 1].PointIndex : Points.Count - 1;
    }

    /// <summary>
    ///     Moves a waypoint, bending only the path between its two neighbouring waypoints. Those
    ///     neighbours are pinned and everything beyond them is untouched, so editing one corner
    ///     cannot disturb the rest of the route.
    /// </summary>
    /// <remarks>
    ///     Always applied from <paramref name="basePoints" /> — a pristine copy taken when the drag
    ///     began — rather than from the current points, so a held drag cannot accumulate drift over
    ///     successive frames.
    /// </remarks>
    public void MoveWaypoint(Waypoint waypoint, IReadOnlyList<Vector3> basePoints, Vector3 target)
    {
        if (waypoint == null || basePoints == null || basePoints.Count != Points.Count)
        {
            return;
        }

        var anchor = waypoint.PointIndex;
        var offset = target - basePoints[anchor];

        for (var i = 0; i < Points.Count; i++)
        {
            Points[i] = basePoints[i];
        }

        Points[anchor] = basePoints[anchor] + offset;

        GetSpan(waypoint, out var from, out var to);

        Blend(basePoints, offset, from, anchor);
        Blend(basePoints, offset, to, anchor);
    }

    /// <summary>
    ///     Eases the offset from nothing at <paramref name="edge" /> to full at the anchor, measured
    ///     along the path so the weighting follows the route rather than straight-line distance.
    /// </summary>
    private void Blend(IReadOnlyList<Vector3> basePoints, Vector3 offset, int edge, int anchor)
    {
        if (edge == anchor)
        {
            return;
        }

        var step = anchor > edge ? 1 : -1;
        var total = PathGeometry.ArcLength(basePoints, edge, anchor);

        if (total <= 0f)
        {
            return;
        }

        var travelled = 0f;

        for (var i = edge + step; i != anchor; i += step)
        {
            travelled += Vector3.Distance(basePoints[i], basePoints[i - step]);

            var t = Mathf.Clamp01(travelled / total);
            Points[i] = basePoints[i] + offset * (t * t * (3f - 2f * t));
        }
    }

    /// <summary>The first waypoint at or after a point index, or null once past the last one.</summary>
    public Waypoint NextFrom(int pointIndex)
    {
        for (var i = 0; i < Waypoints.Count; i++)
        {
            if (Waypoints[i].PointIndex >= pointIndex)
            {
                return Waypoints[i];
            }
        }

        return null;
    }
}
