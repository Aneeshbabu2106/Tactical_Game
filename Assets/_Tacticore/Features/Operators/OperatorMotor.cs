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
    private readonly List<Vector3> path = new();
    private int next;

    public OperatorMotor(Vector3 position, float facingDegrees = 0f)
    {
        Position = position;
        FacingDegrees = facingDegrees;
    }

    public float MoveSpeed { get; set; } = 2.6f;

    public float TurnRate { get; set; } = 360f;

    public Vector3 Position { get; private set; }

    /// <summary>Degrees counter-clockwise from +X.</summary>
    public float FacingDegrees { get; private set; }

    public Vector2 Forward =>
        new(Mathf.Cos(FacingDegrees * Mathf.Deg2Rad), Mathf.Sin(FacingDegrees * Mathf.Deg2Rad));

    public bool IsMoving => next < path.Count;

    /// <summary>
    ///     A point to keep looking at. While set it wins over the direction of travel, matching the
    ///     prototype's rule that an explicit look beats "the walk owns the look". Null hands the
    ///     facing back to movement.
    /// </summary>
    public Vector3? LookTarget { get; set; }

    /// <summary>The part of the path still to be walked, nearest point first.</summary>
    public IEnumerable<Vector3> Remaining
    {
        get
        {
            for (var i = next; i < path.Count; i++)
            {
                yield return path[i];
            }
        }
    }

    public int RemainingCount => Mathf.Max(0, path.Count - next);

    /// <summary>
    ///     Teleports the motor, for example to sync with a transform moved from outside.
    /// </summary>
    public void MoveTo(Vector3 position)
    {
        Position = position;
    }

    public void SetPath(IReadOnlyList<Vector3> points)
    {
        path.Clear();

        // Walk in the operator's own plane, so following a path never changes sprite sorting.
        var z = Position.z;

        foreach (var point in points)
        {
            path.Add(new Vector3(point.x, point.y, z));
        }

        next = 0;
    }

    public void ClearPath()
    {
        path.Clear();
        next = 0;
    }

    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        var before = Position;

        if (IsMoving)
        {
            Position = Vector3.MoveTowards(Position, path[next], MoveSpeed * deltaTime);

            // Consume every waypoint reached this tick, so a high speed cannot overshoot a corner.
            while (next < path.Count && Position == path[next])
            {
                next++;
            }
        }

        // Runs even when stopped, so a stationary operator can still be turned by a look target.
        Turn(Position - before, deltaTime);
    }

    /// <summary>
    ///     Eases the facing towards the look target if one is set, otherwise towards the direction
    ///     actually travelled. Travel is used rather than the target waypoint so the operator leans
    ///     into a curve instead of snapping to each of the densely sampled points.
    /// </summary>
    private void Turn(Vector3 travelled, float deltaTime)
    {
        Vector3 towards;

        if (LookTarget.HasValue)
        {
            towards = LookTarget.Value - Position;
        }
        else
        {
            towards = travelled;
        }

        // Ignore sub-pixel drift; a stopped operator holds its last facing rather than resetting.
        if (towards.sqrMagnitude < 1e-8f)
        {
            return;
        }

        var desired = Mathf.Atan2(towards.y, towards.x) * Mathf.Rad2Deg;
        FacingDegrees = Mathf.MoveTowardsAngle(FacingDegrees, desired, TurnRate * deltaTime);
    }
}
