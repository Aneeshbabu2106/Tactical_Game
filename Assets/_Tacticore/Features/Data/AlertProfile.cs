using UnityEngine;

/// <summary>
///     The timings every enemy shares, indexed by <see cref="AlertLevel" />. Global tables in the
///     prototype rather than per-type numbers, and kept that way here: an alerted thug and an
///     alerted heavy react at the same speed, they just differ in what they are holding.
/// </summary>
[CreateAssetMenu(fileName = "AlertProfile", menuName = "Tacticore/Alert Profile")]
public class AlertProfile : ScriptableObject
{
    [Header("Per alert level: unaware, suspicious, searching, alerted")]
    [Tooltip("Seconds spent deciding before he starts to turn onto a target. The prototype's "
             + "T_DECIDE — the difference between surprising a man and not.")]
    public float[] decideSeconds = { 1.30f, 0.70f, 0.50f, 0.25f };

    [Tooltip("Degrees per second he can swing. The prototype's ENEMY_TURN.")]
    public float[] turnRate = { 180f, 300f, 340f, 400f };

    [Header("Alerted sight")]
    [Tooltip("Once he has seen someone his eyes widen. Overrides the vision spec while alerted.")]
    public float alertedRange = 12f;

    public float alertedFov = 120f;

    [Header("Searching")]
    [Tooltip("Seconds he will spend walking to a noise before giving it up.")]
    public float searchSeconds = 12f;

    [Tooltip("How close to the noise counts as having looked, in cells.")]
    public float searchArriveRadius = 1f;

    [Header("Decay")]
    [Tooltip("Seconds of quiet before suspicious falls back to unaware.")]
    public float suspiciousSeconds = 8f;

    [Tooltip("Seconds of no contact before alerted falls back to suspicious. The prototype never "
             + "comes down from alerted at all; here a mistake is recoverable.")]
    public float alertedSeconds = 10f;

    [Header("Firing pipeline")]
    [Tooltip("Seconds a lost target is held before the pipeline resets, so stepping behind a door "
             + "frame for an instant does not buy a fresh reaction time.")]
    public float reacquireSeconds = 2f;

    [Tooltip("Degrees of residual error at which he stops turning and starts aiming.")]
    public float onTargetDegrees = 8f;

    [Tooltip("Seconds spent settling and aiming once he is on target, at close range.")]
    public float aimSeconds = 0.24f;

    [Tooltip("Extra aim time per cell of range past the first five.")]
    public float aimSecondsPerCell = 0.012f;

    public float DecideSeconds(AlertLevel level)
    {
        return Pick(decideSeconds, level, 0.5f);
    }

    public float TurnRate(AlertLevel level)
    {
        return Pick(turnRate, level, 300f);
    }

    /// <summary>Tolerates a shortened array rather than throwing, since these are hand-edited.</summary>
    private static float Pick(float[] table, AlertLevel level, float fallback)
    {
        if (table == null || table.Length == 0)
        {
            return fallback;
        }

        return table[Mathf.Clamp((int)level, 0, table.Length - 1)];
    }
}
