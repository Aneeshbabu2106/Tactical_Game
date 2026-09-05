/// <summary>
///     How an operator handles contact. The order you give before he goes through the door.
/// </summary>
public enum EngagementMode
{
    /// <summary>Shoots what he sees without breaking stride. Speed over certainty.</summary>
    KeepMoving,

    /// <summary>Stops to shoot, and only moves on once nothing is in view. Certainty over speed.</summary>
    WaitToClear,

    /// <summary>Will not fire at all, however plain the shot. For getting somewhere quietly.</summary>
    HoldFire
}
