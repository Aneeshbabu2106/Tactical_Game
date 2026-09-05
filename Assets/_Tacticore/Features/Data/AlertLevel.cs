/// <summary>
///     How switched-on an enemy is. Drives his reaction speed, how fast he turns and how far he can
///     see, so the same man is a very different opponent before and after you make a noise.
/// </summary>
/// <remarks>
///     Note this is not "what he is doing" — engaging a target he can see is checked before any of
///     these and cuts across all of them, exactly as the prototype's updateAI checks engageTarget
///     first. The prototype has a fifth level, ALARM, raised for the whole map at once; there is no
///     general alarm here yet, so there is no level nothing can reach.
/// </remarks>
public enum AlertLevel
{
    /// <summary>Nothing is wrong. Walks his patrol or holds his post facing.</summary>
    Unaware = 0,

    /// <summary>Heard something faint. Stands and turns to face it.</summary>
    Suspicious = 1,

    /// <summary>Heard something he can place. Walks to it and looks.</summary>
    Searching = 2,

    /// <summary>Has seen a hostile. Fast, wide-eyed, and holds the angle he last saw one on.</summary>
    Alerted = 3
}
