using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
///     One thing an operator can do to a door or window, as a data row rather than a method — the
///     shape the prototype's ABILITIES table uses, so adding a verb stays an entry in a list.
/// </summary>
public readonly struct OpeningVerb
{
    public OpeningVerb(
        string label, float duration, float loudness, Predicate<Opening> available,
        Action<Opening> apply)
    {
        Label = label;
        Duration = duration;
        Loudness = loudness;
        this.available = available;
        this.apply = apply;
    }

    private readonly Predicate<Opening> available;
    private readonly Action<Opening> apply;

    public string Label { get; }

    /// <summary>Seconds of simulated time the operator spends on it.</summary>
    public float Duration { get; }

    /// <summary>How far the doing of it carries. The reason KICK is not simply a faster OPEN.</summary>
    public float Loudness { get; }

    public bool IsValid => apply != null;

    /// <summary>Whether the verb makes sense against the opening's state right now.</summary>
    public bool IsAvailable(Opening opening)
    {
        return opening != null && available(opening);
    }

    public void Apply(Opening opening)
    {
        apply(opening);
    }
}

/// <summary>
///     The verbs available on doors and windows, and the queuing that sends an operator to perform
///     one. Mirrors the prototype's abilitiesForEdge and queueEdgeAction.
/// </summary>
public static class OpeningActions
{
    /// <summary>Spacing of the points laid down on an appended approach run.</summary>
    private const float PointSpacing = 0.2f;

    /// <summary>
    ///     Availability is driven entirely by state, and every verb here is one-way: once a door is
    ///     open or a pane is broken the opening has nothing left to offer and stops responding to
    ///     clicks altogether. The prototype's fuller set — try handle, pick lock, wedge, the
    ///     breaching charges — waits on the lock and kit systems that do not exist yet.
    /// </summary>
    private static readonly OpeningVerb[] Verbs =
    {
        new("OPEN", 1.4f, Noise.DoorOpen,
            o => o is Door { IsOpen: false }, o => ((Door)o).Open()),

        // The same outcome as OPEN in half the time, paid for in noise: this is heard across most
        // of the map and sends anyone who hears it to the door.
        new("KICK", 0.7f, Noise.DoorKick,
            o => o is Door { IsOpen: false }, o => ((Door)o).Open()),

        new("BREAK GLASS", 0.9f, Noise.GlassBreak,
            o => o is Window { IsBroken: false }, o => ((Window)o).Break())
    };

    /// <summary>
    ///     Whether the opening is worth clicking at all. Used to keep a spent door out of the
    ///     pointer's reckoning entirely, rather than offering an empty menu.
    /// </summary>
    public static bool HasAny(Opening opening)
    {
        foreach (var verb in Verbs)
        {
            if (verb.IsAvailable(opening))
            {
                return true;
            }
        }

        return false;
    }

    public static List<OpeningVerb> For(Opening opening)
    {
        var found = new List<OpeningVerb>();

        foreach (var verb in Verbs)
        {
            if (verb.IsAvailable(opening))
            {
                found.Add(verb);
            }
        }

        return found;
    }

    /// <summary>
    ///     Sends an operator to work on an opening: extends its route until it is within reach of
    ///     the opening and puts a waypoint there carrying the verb.
    /// </summary>
    /// <remarks>
    ///     Reach, not the cell centre. Standing exactly on the doorstep looks wrong and would put
    ///     the operator inside the arc the leaf swings through, so he stops as soon as he is close
    ///     enough to work it. An operator already within reach does not move at all.
    ///     <para>
    ///         The approach is routed with <see cref="CellPathfinder" /> from wherever the route
    ///         currently ends, so it walks around walls rather than through them. Only orders the
    ///         player did not draw are routed this way; a hand-drawn stroke is still the route.
    ///     </para>
    /// </remarks>
    public static bool Queue(Operator op, Opening opening, OpeningVerb verb, Tilemap navigation)
    {
        if (op == null || opening == null || navigation == null || !verb.IsValid)
        {
            Debug.LogWarning(
                $"{verb.Label}: refused, something is not wired — operator={op != null} "
                + $"opening={opening != null} tilemap={navigation != null} verb={verb.IsValid}");
            return false;
        }

        var plan = op.Plan;

        if (plan == null)
        {
            Debug.LogWarning($"{verb.Label}: refused, {op.name} has no path plan.", op);
            return false;
        }

        // Continue from the end of the existing route, or from the operator when there is none.
        var anchor = plan.Points.Count > 0 ? plan.Points[plan.Points.Count - 1] : op.transform.position;

        var centre = navigation.GetCellCenterWorld(opening.Cell);
        centre.z = anchor.z;

        var reach = Mathf.Max(op.OpeningReach, 0.01f);
        int index;

        if (Vector2.Distance(anchor, centre) <= reach)
        {
            // Close enough already: work from here rather than shuffling onto a mark. Still a new
            // point rather than the existing last one — a route already walked has its index behind
            // the motor, and a waypoint there would never be reached.
            index = plan.Append(new[] { anchor });
        }
        else
        {
            if (!TryWalkTo(opening, navigation, anchor, reach, plan, out index))
            {
                Debug.LogWarning(
                    $"{verb.Label}: refused, no route from {anchor} to the {opening.name} at "
                    + $"{opening.Cell}. Draw a path closer and order it again.", opening);
                return false;
            }
        }

        if (index < 0)
        {
            return false;
        }

        var waypoint = plan.Add(index);

        if (waypoint == null)
        {
            return false;
        }

        // Deliberately no facing: working a door does not tell the operator where to look, and
        // overriding his aim to stare at a doorframe is the last thing wanted on a stack.
        waypoint.Action = new OpeningAction(opening, verb);
        waypoint.ActionDone = false;

        op.PathChanged();

        return true;
    }

    /// <summary>
    ///     Routes the operator to a spot <paramref name="reach" /> out from the opening and lays the
    ///     points down. A doorway has two sides; the nearer is tried first, and the other is a real
    ///     fallback rather than a formality — the near side is often the one behind the wall.
    /// </summary>
    private static bool TryWalkTo(
        Opening opening, Tilemap navigation, Vector3 anchor, float reach,
        PathPlan plan, out int index)
    {
        index = -1;

        var centre = navigation.GetCellCenterWorld(opening.Cell);
        centre.z = anchor.z;

        foreach (var side in Sides(opening, navigation, anchor))
        {
            var offset = (Vector2)(navigation.GetCellCenterWorld(side) - navigation.GetCellCenterWorld(opening.Cell));
            var target = centre + (Vector3)(offset.normalized * reach);
            target.z = anchor.z;

            // Reach can fall short of the neighbouring cell, which would leave him standing in the
            // doorway itself — a cell that is not walkable while the door is shut, so the order
            // would be refused outright. The cell beside it is what was meant either way, and
            // snapping to it means a badly authored reach costs a step of accuracy, not the order.
            if (!NavigationQuery.IsWalkable(navigation, navigation.WorldToCell(target)))
            {
                target = navigation.GetCellCenterWorld(side);
                target.z = anchor.z;
            }

            var route = CellPathfinder.Find(navigation, anchor, target);

            if (route == null)
            {
                continue;
            }

            var points = new List<Vector3>();
            var leg = new List<Vector3>();

            // Route corners are only the turns; fill them in at the cadence a drawn stroke uses so
            // the smoothing and the direction arrows treat this no differently.
            for (var i = 1; i < route.Count; i++)
            {
                Sample(route[i - 1], route[i], leg);
                points.AddRange(leg);
            }

            index = plan.Append(points);
            return index >= 0;
        }

        return false;
    }

    /// <summary>The walkable cells either side of the opening, nearest to the route end first.</summary>
    private static IEnumerable<Vector3Int> Sides(Opening opening, Tilemap navigation, Vector3 from)
    {
        var near = opening.NearSide;
        var far = opening.FarSide;

        if (Vector2.Distance(navigation.GetCellCenterWorld(far), from)
            < Vector2.Distance(navigation.GetCellCenterWorld(near), from))
        {
            (near, far) = (far, near);
        }

        if (NavigationQuery.IsWalkable(navigation, near))
        {
            yield return near;
        }

        if (NavigationQuery.IsWalkable(navigation, far))
        {
            yield return far;
        }
    }

    /// <summary>
    ///     Points along the approach at the same cadence a drawn stroke uses, so the smoothing and
    ///     the direction arrows treat it no differently. The anchor itself is skipped: it is already
    ///     the last point of the route.
    /// </summary>
    private static void Sample(Vector3 from, Vector3 to, List<Vector3> into)
    {
        into.Clear();

        var length = Vector3.Distance(from, to);
        var steps = Mathf.Max(1, Mathf.CeilToInt(length / PointSpacing));

        for (var i = 1; i <= steps; i++)
        {
            into.Add(Vector3.Lerp(from, to, i / (float)steps));
        }
    }
}

/// <summary>
///     A verb bound to the opening it was issued against, in the form the motor can run. This is the
///     Grid-aware half that <see cref="IWaypointAction" /> deliberately keeps out of the path model.
/// </summary>
public class OpeningAction : IWaypointAction
{
    private readonly Opening opening;
    private readonly OpeningVerb verb;

    public OpeningAction(Opening opening, OpeningVerb verb)
    {
        this.opening = opening;
        this.verb = verb;
    }

    public string Label => verb.Label;

    public float Duration => verb.Duration;

    /// <summary>
    ///     Re-asked on arrival. A door another operator already opened fails here and the walk
    ///     carries on rather than stalling for the duration.
    /// </summary>
    public bool IsStillValid => opening != null && verb.IsAvailable(opening);

    public void Perform()
    {
        if (opening == null)
        {
            return;
        }

        verb.Apply(opening);

        // From the opening, not the operator: what carries is the door coming in, and that is
        // where anyone who hears it will come looking.
        Noise.Emit(opening.transform.position, verb.Loudness, opening);
    }
}
