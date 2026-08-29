using System;
using UnityEngine;

/// <summary>
///     The simulation's own clock. Everything that advances gameplay reads
///     <see cref="DeltaTime" /> instead of <see cref="Time.deltaTime" />, so pausing is a value of
///     zero rather than a global freeze.
/// </summary>
/// <remarks>
///     Deliberately not Time.timeScale: that would also stop input polling rate, animation and
///     particles, and the whole point of the paused state is that the player keeps planning in it.
/// </remarks>
public static class SimClock
{
    /// <summary>Play begins paused — the player plans first, then commits.</summary>
    public static bool IsPaused { get; private set; } = true;

    /// <summary>Seconds since the last frame, or zero while paused.</summary>
    public static float DeltaTime => IsPaused ? 0f : Time.deltaTime;

    public static event Action<bool> PausedChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        // Survives domain reload being disabled, which would otherwise carry the last
        // session's pause state and subscribers into the next play.
        IsPaused = true;
        PausedChanged = null;
    }

    public static void SetPaused(bool paused)
    {
        if (IsPaused == paused)
        {
            return;
        }

        IsPaused = paused;
        PausedChanged?.Invoke(paused);
    }

    public static void TogglePause()
    {
        SetPaused(!IsPaused);
    }
}
