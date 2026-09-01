using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
///     Waypoint gestures: left-drag to move one, left-click to open its menu, right-drag to set the
///     direction it should be facing, and left-click on the path to add one.
/// </summary>
public class WaypointInput : MonoBehaviour
{
    [SerializeField] private PointerInput pointer;
    [SerializeField] private PointerRouter router;
    [SerializeField] private Tilemap navigation;

    [Tooltip("A press that moves less than this is a click, not a drag.")]
    [SerializeField] private float clickThreshold = 0.2f;

    [Tooltip("Spacing of the walkability samples taken along a deformed span.")]
    [SerializeField] private float clearanceStep = 0.2f;

    /// <summary>Raised on a left-click of a waypoint that was not a drag.</summary>
    public event Action<Operator, Waypoint> WaypointClicked;

    // Pristine copy of the route taken when a drag begins, so a held drag re-applies from the
    // original each frame instead of compounding its own offset.
    private readonly List<Vector3> basePoints = new();

    private SpriteRenderer hoverMarker;
    private Operator owner;
    private Waypoint dragging;
    private Waypoint aiming;
    private Vector3 pressedAt;
    private bool dragged;

    private void Awake()
    {
        if (pointer == null)
        {
            pointer = FindFirstObjectByType<PointerInput>();
        }

        if (router == null)
        {
            router = FindFirstObjectByType<PointerRouter>();
        }

        EnsureHoverMarker();
    }

    /// <summary>The plus glyph shown where a click would add a waypoint.</summary>
    private void EnsureHoverMarker()
    {
        var host = new GameObject("AddWaypointHint");
        host.transform.SetParent(transform, false);

        hoverMarker = host.AddComponent<SpriteRenderer>();
        hoverMarker.sprite = LineArt.Plus();
        hoverMarker.sortingOrder = 106;
        hoverMarker.enabled = false;
    }

    private void UpdateHoverMarker()
    {
        // Hidden mid-gesture: the cursor is committed to a drag, not shopping for a place to click.
        var show = router.Kind == PointerTargetKind.Path && dragging == null && aiming == null;

        hoverMarker.enabled = show;

        if (!show)
        {
            return;
        }

        hoverMarker.color = router.Operator.PathColor;
        hoverMarker.transform.position = router.PathPoint;
        hoverMarker.transform.localScale = Vector3.one * router.Operator.WaypointMarkerSize;
    }

    private void Update()
    {
        if (pointer == null || !pointer.IsAvailable || router == null)
        {
            return;
        }

        var cursor = pointer.WorldPosition;

        HandleMove(cursor);
        HandleAim(cursor);
        HandleAdd();
        UpdateHoverMarker();
    }

    private void HandleMove(Vector3 cursor)
    {
        if (pointer.Pressed && router.Kind == PointerTargetKind.Waypoint)
        {
            owner = router.Operator;
            dragging = router.Waypoint;
            pressedAt = cursor;
            dragged = false;

            basePoints.Clear();
            basePoints.AddRange(owner.Plan.Points);
            return;
        }

        if (dragging == null)
        {
            return;
        }

        if (pointer.Held)
        {
            if (!dragged && Vector3.Distance(cursor, pressedAt) < clickThreshold)
            {
                return;
            }

            dragged = true;
            TryMove(cursor);
            return;
        }

        if (pointer.Released)
        {
            if (!dragged)
            {
                WaypointClicked?.Invoke(owner, dragging);
            }

            dragging = null;
            basePoints.Clear();
        }
    }

    /// <summary>
    ///     Applies the drag, then reverts it whole if the deformed span leaves walkable ground.
    ///     Rejecting outright rather than clamping keeps the path honest: a partially applied bend
    ///     could still clip a corner.
    /// </summary>
    private void TryMove(Vector3 cursor)
    {
        var plan = owner.Plan;

        plan.MoveWaypoint(dragging, basePoints, cursor);

        if (SpanIsWalkable(plan, dragging))
        {
            owner.PathChanged();
            return;
        }

        for (var i = 0; i < plan.Points.Count; i++)
        {
            plan.Points[i] = basePoints[i];
        }
    }

    /// <summary>
    ///     Only the span between the neighbouring waypoints can have moved, so only it is rechecked.
    /// </summary>
    private bool SpanIsWalkable(PathPlan plan, Waypoint waypoint)
    {
        if (navigation == null)
        {
            return true;
        }

        plan.GetSpan(waypoint, out var from, out var to);

        // A one-point span has no segment to sample; check the point itself.
        if (from >= to)
        {
            return NavigationQuery.IsWalkable(navigation, navigation.WorldToCell(plan.Points[from]));
        }

        for (var i = from; i < to && i + 1 < plan.Points.Count; i++)
        {
            if (!NavigationQuery.SegmentIsWalkable(navigation, plan.Points[i], plan.Points[i + 1], clearanceStep))
            {
                return false;
            }
        }

        return true;
    }

    private void HandleAim(Vector3 cursor)
    {
        if (pointer.RightPressed && router.Kind == PointerTargetKind.Waypoint)
        {
            owner = router.Operator;
            aiming = router.Waypoint;
            pressedAt = cursor;
            return;
        }

        if (aiming == null)
        {
            return;
        }

        if (pointer.RightHeld)
        {
            var anchor = owner.Plan.Points[aiming.PointIndex];
            var towards = cursor - anchor;

            if (towards.sqrMagnitude > 1e-6f)
            {
                aiming.FacingDegrees = Mathf.Atan2(towards.y, towards.x) * Mathf.Rad2Deg;
                owner.PathChanged();
            }

            return;
        }

        if (pointer.RightReleased)
        {
            // A right-click without a drag clears the waypoint's facing.
            if (Vector3.Distance(cursor, pressedAt) < clickThreshold)
            {
                aiming.FacingDegrees = null;
                owner.PathChanged();
            }

            aiming = null;
        }
    }

    private void HandleAdd()
    {
        if (!pointer.Pressed || router.Kind != PointerTargetKind.Path)
        {
            return;
        }

        router.Operator.Plan.Add(router.PathIndex);
        router.Operator.PathChanged();
    }
}
