using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Something that made a sound, at a place, with a loudness. Loudness is in the prototype's
///     arbitrary units, not decibels — see <see cref="Noise" />.
/// </summary>
public readonly struct NoiseEvent
{
    public readonly Vector3 At;
    public readonly float Loudness;

    /// <summary>Who made it, so a unit never reacts to its own footsteps.</summary>
    public readonly Object Source;

    public NoiseEvent(Vector3 at, float loudness, Object source)
    {
        At = at;
        Loudness = loudness;
        Source = source;
    }
}

/// <summary>
///     The second information channel. Sight tells an enemy where you are; noise tells him that you
///     exist and roughly where to look, through walls that sight cannot cross.
/// </summary>
/// <remarks>
///     Ported from the prototype's audioprop.js. Loudness is a bare number that falls off with
///     distance and is eaten by whatever the sound passes through — <see cref="SoundQuery" /> does
///     that part, because it needs the tilemap and this does not.
///     <para>
///         Listeners pull rather than subscribe, each holding a cursor into the log, so an enemy
///         that ticks late in the frame sees exactly the same events as one that ticked early and
///         nothing has to care about execution order. The log is a small ring: a listener that falls
///         further behind than that has bigger problems than a missed footstep.
///     </para>
/// </remarks>
public static class Noise
{
    /// <summary>
    ///     Loudness by source, from the prototype's NOISE table. A walk step is deliberately at the
    ///     notice threshold and so is never actually heard — walking is the quiet option, and the
    ///     number is kept only so a future surface multiplier has something to scale.
    /// </summary>
    public const float WalkStep = 1.0f;

    /// <summary>Above the locate threshold, so a running man can be pinned down at close range.</summary>
    public const float RunStep = 4.5f;

    public const float DoorOpen = 1.0f;
    public const float GlassBreak = 7.0f;
    public const float DoorKick = 8.0f;
    public const float Gunshot = 9.0f;

    private const int Capacity = 64;

    private static readonly NoiseEvent[] Log = new NoiseEvent[Capacity];

    /// <summary>How many events have ever been emitted. Listeners keep a cursor into this.</summary>
    public static int Sequence { get; private set; }

    public static void Emit(Vector3 at, float loudness, Object source = null)
    {
        if (loudness <= 0f)
        {
            return;
        }

        Log[Sequence % Capacity] = new NoiseEvent(at, loudness, source);
        Sequence++;
    }

    /// <summary>
    ///     Appends everything emitted since <paramref name="cursor" /> and moves it to the present.
    ///     Pass a cursor of 0 on the first call to hear everything so far.
    /// </summary>
    public static void Collect(ref int cursor, List<NoiseEvent> into)
    {
        var from = Mathf.Max(cursor, Sequence - Capacity);

        for (var i = from; i < Sequence; i++)
        {
            into.Add(Log[i % Capacity]);
        }

        cursor = Sequence;
    }

    /// <summary>
    ///     Play mode does not reset statics, so a second run would start with the first run's
    ///     gunshots still in the log and every enemy already suspicious.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Sequence = 0;
    }
}
