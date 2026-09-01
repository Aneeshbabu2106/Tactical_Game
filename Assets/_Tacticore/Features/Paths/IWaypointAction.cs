/// <summary>
///     Something an operator stops and does on arriving at a waypoint — opening a door, breaking a
///     window. The motor runs the timer and calls <see cref="Perform" />; what actually happens is
///     none of its business.
/// </summary>
/// <remarks>
///     Deliberately an interface with no Unity types in it. The path model has no assembly
///     references at all, and the things these actions operate on live in the tile system — so the
///     waypoint holds this and the tools layer supplies the implementation.
/// </remarks>
public interface IWaypointAction
{
    /// <summary>Shown on the menu row that queued it, and on the progress bar while it runs.</summary>
    string Label { get; }

    /// <summary>Seconds of simulated time the operator spends on it.</summary>
    float Duration { get; }

    /// <summary>
    ///     False once the action has become pointless — a door someone else already opened. Checked
    ///     on arrival so the operator walks on instead of miming at an open doorway.
    /// </summary>
    bool IsStillValid { get; }

    void Perform();
}
