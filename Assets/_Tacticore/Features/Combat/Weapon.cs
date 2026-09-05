using UnityEngine;

/// <summary>
///     One firearm's state: what is in the magazine, when it may fire next, and whether it is
///     mid-reload. A plain class, driven entirely by <see cref="Tick" />, so the timing can be
///     exercised without a scene the way <see cref="OperatorMotor" /> already is.
/// </summary>
/// <remarks>
///     Follows the prototype's fireShot: the interval between rounds is 60/rpm, and a magazine that
///     runs dry reloads itself. A reload can also be ordered early, which is the whole point of
///     having the action on the menu — you top up before the door, not in the doorway.
/// </remarks>
public class Weapon
{
    private float cooldown;
    private float reloading;

    public Weapon(int magazineSize, float roundsPerMinute, float reloadSeconds)
    {
        MagazineSize = Mathf.Max(1, magazineSize);
        RoundsPerMinute = Mathf.Max(1f, roundsPerMinute);
        ReloadSeconds = Mathf.Max(0.01f, reloadSeconds);
        Ammo = MagazineSize;
    }

    public int MagazineSize { get; }

    public float RoundsPerMinute { get; }

    public float ReloadSeconds { get; }

    public int Ammo { get; private set; }

    public bool IsReloading => reloading > 0f;

    /// <summary>How far through a reload, 0 to 1. Zero when not reloading.</summary>
    public float ReloadProgress => IsReloading ? 1f - reloading / ReloadSeconds : 0f;

    public bool IsFull => Ammo >= MagazineSize;

    public bool CanFire => !IsReloading && cooldown <= 0f && Ammo > 0;

    /// <summary>Seconds until the next round may leave. Negative means overdue.</summary>
    public float Cooldown => cooldown;

    /// <summary>Seconds left of the current reload.</summary>
    public float Reloading => reloading;

    /// <summary>Seconds between rounds.</summary>
    private float Interval => 60f / RoundsPerMinute;

    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        if (IsReloading)
        {
            reloading -= deltaTime;

            if (reloading <= 0f)
            {
                reloading = 0f;
                Ammo = MagazineSize;
                cooldown = 0f;
            }

            return;
        }

        // Allowed to go negative. The overshoot is carried into the next shot, so the rate cannot
        // quantise to whole frames and drift below the stated rpm.
        cooldown -= deltaTime;
    }

    /// <summary>
    ///     Fires if it can. A dry magazine starts reloading instead of firing, which is what makes
    ///     sustained fire look after itself.
    /// </summary>
    public bool TryFire()
    {
        if (IsReloading || cooldown > 0f)
        {
            return false;
        }

        if (Ammo <= 0)
        {
            BeginReload();
            return false;
        }

        Ammo--;
        cooldown += Interval;

        if (Ammo == 0)
        {
            BeginReload();
        }

        return true;
    }

    /// <summary>Orders a reload early. Refuses on a full magazine or one already in progress.</summary>
    public bool BeginReload()
    {
        if (IsReloading || IsFull)
        {
            return false;
        }

        reloading = ReloadSeconds;
        return true;
    }
}
