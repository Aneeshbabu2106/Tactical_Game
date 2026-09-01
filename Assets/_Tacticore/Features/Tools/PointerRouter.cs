using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>What the pointer is currently over. Resolved in one place, in priority order.</summary>
public enum PointerTargetKind
{
    None,
    Waypoint,
    Operator,
    Opening,
    Path
}

/// <summary>
///     The single authority on what a press is aimed at. Tools read <see cref="Kind" /> and act only
///     on their own, so exactly one of them can claim any given press.
/// </summary>
/// <remarks>
///     Previously each tool hit-tested the pointer itself, which meant several components racing for
///     the same press at undefined execution order. That produced two real bugs already — an input
///     component added to two objects at once, and a right-click landing on a menu row meant for the
///     left button. Runs at -50: after PointerInput publishes the cursor, before anything reads this.
/// </remarks>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class PointerRouter : MonoBehaviour
{
    [SerializeField] private PointerInput pointer;
    [SerializeField] private Tilemap navigation;

    [Tooltip("Seconds between rescans for operators, rather than searching the scene every frame.")]
    [SerializeField] private float rescanInterval = 0.5f;

    private Operator[] operators = System.Array.Empty<Operator>();
    private float nextScan;

    public PointerTargetKind Kind { get; private set; }

    /// <summary>The operator involved — the one hit, or the owner of the waypoint or path.</summary>
    public Operator Operator { get; private set; }

    public Waypoint Waypoint { get; private set; }

    /// <summary>The door or window under the cursor, when the pointer is over one.</summary>
    public Opening Opening { get; private set; }

    /// <summary>Closest position on the hovered path, and the point index nearest to it.</summary>
    public Vector3 PathPoint { get; private set; }

    public int PathIndex { get; private set; }

    public Vector3 Cursor => pointer != null ? pointer.WorldPosition : Vector3.zero;

    private void Awake()
    {
        if (pointer == null)
        {
            pointer = FindFirstObjectByType<PointerInput>();
        }
    }

    private void Update()
    {
        Kind = PointerTargetKind.None;
        Operator = null;
        Waypoint = null;
        Opening = null;

        if (pointer == null || !pointer.IsAvailable)
        {
            return;
        }

        RefreshOperators();
        Resolve(pointer.WorldPosition);
    }

    private void RefreshOperators()
    {
        if (Time.unscaledTime < nextScan)
        {
            return;
        }

        nextScan = Time.unscaledTime + rescanInterval;
        operators = FindObjectsByType<Operator>(FindObjectsSortMode.None);
    }

    /// <summary>
    ///     Waypoint, then operator, then opening, then path. A waypoint sits on the path and often
    ///     near the operator, so the smallest target has to win or it becomes unclickable.
    /// </summary>
    private void Resolve(Vector3 cursor)
    {
        var best = float.MaxValue;

        foreach (var op in operators)
        {
            if (op == null || op.Plan == null)
            {
                continue;
            }

            foreach (var waypoint in op.Plan.Waypoints)
            {
                // Only what is actually drawn can be clicked. A waypoint already walked past is
                // hidden by Operator.RedrawWaypoints, and an invisible target that swallows clicks
                // is worse than no target at all.
                if (waypoint.PointIndex >= op.Plan.Points.Count || waypoint.PointIndex < op.NextIndex)
                {
                    continue;
                }

                var at = op.Plan.Points[waypoint.PointIndex];

                // A waypoint underfoot loses to the man standing on it. Queued door actions put one
                // exactly where the operator stops, and while paused it sits there indefinitely —
                // which made him impossible to grab and drag a new path from.
                if (Vector2.Distance(at, op.transform.position) <= op.PickRadius)
                {
                    continue;
                }

                var d = Vector2.Distance(at, cursor);

                if (d <= op.WaypointPickRadius && d < best)
                {
                    best = d;
                    Kind = PointerTargetKind.Waypoint;
                    Operator = op;
                    Waypoint = waypoint;
                }
            }
        }

        if (Kind == PointerTargetKind.Waypoint)
        {
            return;
        }

        var hit = OperatorPicker.At(cursor);

        if (hit != null)
        {
            Kind = PointerTargetKind.Operator;
            Operator = hit;
            return;
        }

        // A door is a whole cell and a dictionary lookup away, so it costs nothing to test. It
        // outranks the path: a route crossing a doorway must not make the door unclickable. The
        // cost is that the add-waypoint hint does not appear over one, which suits a doorway.
        // A spent opening — door already swung, glass already gone — has no verbs left, so it drops
        // out of the reckoning entirely and the path underneath becomes clickable again.
        if (navigation != null
            && OpeningRegistry.TryGet(navigation.WorldToCell(cursor), out var opening)
            && OpeningActions.HasAny(opening))
        {
            Kind = PointerTargetKind.Opening;
            Opening = opening;
            return;
        }

        best = float.MaxValue;

        foreach (var op in operators)
        {
            if (op == null || op.Plan == null || op.Plan.Points.Count < 2)
            {
                continue;
            }

            if (!PathGeometry.ClosestPoint(op.Plan.Points, cursor, out var index, out var position, out var d))
            {
                continue;
            }

            if (d <= op.PathHoverThreshold && d < best)
            {
                best = d;
                Kind = PointerTargetKind.Path;
                Operator = op;
                PathPoint = position;
                PathIndex = index;
            }
        }
    }
}
