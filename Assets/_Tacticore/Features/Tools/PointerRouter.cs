using UnityEngine;

/// <summary>What the pointer is currently over. Resolved in one place, in priority order.</summary>
public enum PointerTargetKind
{
    None,
    Waypoint,
    Operator,
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

    [Tooltip("Seconds between rescans for operators, rather than searching the scene every frame.")]
    [SerializeField] private float rescanInterval = 0.5f;

    private Operator[] operators = System.Array.Empty<Operator>();
    private float nextScan;

    public PointerTargetKind Kind { get; private set; }

    /// <summary>The operator involved — the one hit, or the owner of the waypoint or path.</summary>
    public Operator Operator { get; private set; }

    public Waypoint Waypoint { get; private set; }

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
    ///     Waypoint, then operator, then path. A waypoint sits on the path and often near the
    ///     operator, so the smallest target has to win or it becomes unclickable.
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
                if (waypoint.PointIndex >= op.Plan.Points.Count)
                {
                    continue;
                }

                var d = Vector2.Distance(op.Plan.Points[waypoint.PointIndex], cursor);

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
