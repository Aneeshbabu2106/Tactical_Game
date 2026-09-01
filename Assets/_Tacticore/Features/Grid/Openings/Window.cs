using UnityEngine;

/// <summary>
///     A window pane. Intact it blocks the cell; broken it is a hole in the wall like any other gap,
///     which is what makes breaking one worth doing.
/// </summary>
/// <remarks>
///     The prototype keeps a broken window blocking and offers a separate Climb Through verb to get
///     across it. Here a broken window is simply walkable, so a drawn path crosses it with no extra
///     verb — climbing would have nothing left to do.
/// </remarks>
[DisallowMultipleComponent]
public class Window : Opening
{
    [SerializeField] private bool startBroken;

    public bool IsBroken { get; private set; }

    public override bool IsPassable => IsBroken;

    protected override void Initialise()
    {
        IsBroken = startBroken;
    }

    /// <summary>Glass does not un-break, so there is no matching repair.</summary>
    public void Break()
    {
        if (IsBroken)
        {
            return;
        }

        IsBroken = true;
        Changed();
    }
}
