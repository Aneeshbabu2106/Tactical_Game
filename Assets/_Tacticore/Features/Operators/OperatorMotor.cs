using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Path following and facing for one operator, as a plain class. Holds no scene references and
///     is driven entirely by <see cref="Tick" />, so the movement rules can be exercised without a
///     GameObject, a camera or a running editor.
/// </summary>
/// <remarks>
///     It still uses <see cref="Vector3" /> and <see cref="Mathf" />, which are plain structs and
///     static maths — this is free of MonoBehaviour and the scene, not of UnityEngine as a whole.
/// </remarks>
public class OperatorMotor
{
    private int next;
    private Waypoint running;
    private float elapsed;

    public OperatorMotor(Vector3 position, float facingDegrees = 0f)
    {
        Position = position;
        FacingDegrees = facingDegrees;
    }

    /// <summary>The route and its waypoints. Input edits this in place; the motor only reads it.</summary>
    public PathPlan Plan { get; } = new();

    public float WalkSpeed { get; set; } = 2.6f;

    public float RunSpeed { get; set; } = 4.4f;

    /// <summary>Operator-wide pace, used on any leg whose waypoint does not ask to run.</summary>
    public bool Running { get; set; }

    /// <summary>
    ///     Stops him advancing without cancelling the route. Combat sets this while he is holding to
    ///     shoot; clearing it puts him straight back on the path where he left it.
    /// </summary>
    public bool Holding { get; set; }

    public float TurnRate { get; set; } = 360f;

    public Vector3 Position { get; private set; }

    /// <summary>Degrees counter-clockwise from +X.</summary>
    public float FacingDegrees { get; private set; }

    public Vector2 Forward =>
        new(Mathf.Cos(FacingDegrees * Mathf.Deg2Rad), Mathf.Sin(FacingDegrees * Mathf.Deg2Rad));

    public bool IsMoving => next < Plan.Points.Count;

    /// <summary>True while stopped at a waypoint working on its action.</summary>
    public bool IsBusy => running != null;

    /// <summary>The action under way, for the progress readout. Null when simply walking.</summary>
    public IWaypointAction ActiveAction => running?.Action;

    /// <summary>How far through that action, 0 to 1.</summary>
    public float ActionProgress =>
        running != null && running.Action.Duration > 0f
            ? Mathf.Clamp01(elapsed / running.Action.Duration)
            : 0f;

    /// <summary>
    ///     A point to keep looking at. Beaten by a waypoint's own facing, matching the prototype's
    ///     wpLook order. Null hands the facing back to movement.
    /// </summary>
    public Vector3? LookTarget { get; set; }

    /// <summary>
    ///     The waypoint being worked on, else the one being walked towards, else null. The action
    ///     waypoint wins so its facing holds while the operator stands there working.
    /// </summary>
    public Waypoint CurrentWaypoint => running ?? (IsMoving ? Plan.NextFrom(next) : null);

    /// <summary>The part of the path still to be walked, nearest point first.</summary>
    public IEnumerable<Vector3> Remaining
    {
        get
        {
            for (var i = next; i < Plan.Points.Count; i++)
            {
                yield return Plan.Points[i];
            }
        }
    }

    public int RemainingCount => Mathf.Max(0, Plan.Points.Count - next);

    /// <summary>Index of the point being walked towards, for callers editing the plan mid-walk.</summary>
    public int NextIndex => next;

    /// <summary>
    ///     Teleports the motor, for example to sync with a transform moved from outside.
    /// </summary>
    public void MoveTo(Vector3 position)
    {
        Position = position;
    }

    /// <summary>Replaces the route and re-derives its waypoints.</summary>
    public void SetPath(IReadOnlyList<Vector3> points, float turnThreshold, float minSpacing, float maxSpacing)
    {
        Plan.Clear();

        // Walk in the operator's own plane, so following a path never changes sprite sorting.
        var z = Position.z;

        foreach (var point in points)
        {
            Plan.Points.Add(new Vector3(point.x, point.y, z));
        }

        Plan.AutoPlace(turnThreshold, minSpacing, maxSpacing);
        Idle();
    }

    public void ClearPath()
    {
        Plan.Clear();
        Idle();
    }

    /// <summary>Drops any work in progress along with the route it belonged to.</summary>
    private void Idle()
    {
        next = 0;
        running = null;
        elapsed = 0f;
    }

    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        var before = Position;

        // Work first: an operator opening a door is not also walking through it.
        if (running != null)
        {
            elapsed += deltaTime;

            if (elapsed >= running.Action.Duration)
            {
                running.Action.Perform();
                running.ActionDone = true;
                running = null;
                elapsed = 0f;
            }
        }
        else if (IsMoving && !Holding)
        {
            Position = Vector3.MoveTowards(Position, Plan.Points[next], SpeedNow() * deltaTime);

            // Consume every waypoint reached this tick, so a high speed cannot overshoot a corner.
            while (next < Plan.Points.Count && Position == Plan.Points[next])
            {
                var reached = Plan.At(next);
                next++;

                if (Begin(reached))
                {
                    break;
                }
            }
        }

        // Runs even when stopped, so a stationary operator can still be turned by a look target.
        Turn(Position - before, deltaTime);
    }

    /// <summary>
    ///     Starts a waypoint's action, if it has one worth doing. An action that has gone stale —
    ///     a door another operator already opened — is marked done and stepped over rather than
    ///     mimed at.
    /// </summary>
    private bool Begin(Waypoint waypoint)
    {
        if (waypoint == null || !waypoint.HasPendingAction)
        {
            return false;
        }

        if (!waypoint.Action.IsStillValid)
        {
            waypoint.ActionDone = true;
            return false;
        }

        running = waypoint;
        elapsed = 0f;
        return true;
    }

    /// <summary>
    ///     A waypoint's pace applies to the leg ending at it, so the change lands as the operator
    ///     walks that stretch rather than when it was set — the prototype's repathToCurrentWaypoint
    ///     rule, without needing to re-path.
    /// </summary>
    private float SpeedNow()
    {
        var waypoint = CurrentWaypoint;

        // A waypoint's Run escalates the pace, it does not override it. Reading the waypoint alone
        // would make the operator-wide toggle dead the moment a path had any waypoints on it, which
        // is always — AutoPlace gives every drawn path several.
        var runningNow = Running || (waypoint?.Run ?? false);
        return runningNow ? RunSpeed : WalkSpeed;
    }

    /// <summary>
    ///     Facing precedence, following wpLook: the current waypoint's own facing, then the
    ///     operator's look target, then the direction actually travelled. Travel is used rather than
    ///     the target waypoint so the operator leans into a curve instead of snapping to each of the
    ///     densely sampled points.
    /// </summary>
    private void Turn(Vector3 travelled, float deltaTime)
    {
        float desired;

        var waypoint = CurrentWaypoint;

        if (waypoint?.FacingDegrees != null)
        {
            desired = waypoint.FacingDegrees.Value;
        }
        else
        {
            var towards = LookTarget.HasValue ? LookTarget.Value - Position : travelled;

            // Ignore sub-pixel drift; a stopped operator holds its last facing rather than resetting.
            if (towards.sqrMagnitude < 1e-8f)
            {
                return;
            }

            desired = Mathf.Atan2(towards.y, towards.x) * Mathf.Rad2Deg;
        }

        FacingDegrees = Mathf.MoveTowardsAngle(FacingDegrees, desired, TurnRate * deltaTime);
    }
}
