/// <summary>
///     An annotated anchor on a drawn path. Mirrors the prototype's Waypoint, minus the parts that
///     need systems this project does not have yet (abilities, hold-for-go-code).
/// </summary>
/// <remarks>
///     A reference type on purpose: input holds one of these across a drag, and the menu acts on the
///     same instance the path owns.
/// </remarks>
public class Waypoint
{
    /// <summary>Index into the owning plan's Points. Waypoints attach to points, never between them.</summary>
    public int PointIndex;

    /// <summary>Pace for the leg ending at this waypoint.</summary>
    public bool Run;

    /// <summary>Look direction to hold on this leg, or null to leave the facing to the walk.</summary>
    public float? FacingDegrees;

    /// <summary>Work to do on arrival, or null to walk straight through.</summary>
    public IWaypointAction Action;

    /// <summary>
    ///     Set once the action has run. Kept on the waypoint rather than cleared with the action
    ///     itself, so the marker stays on the path showing where the work was done.
    /// </summary>
    public bool ActionDone;

    public bool HasPendingAction => Action != null && !ActionDone;
}
