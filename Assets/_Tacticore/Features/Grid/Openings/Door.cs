using UnityEngine;

/// <summary>
///     A door leaf. Shut it blocks its cell; open it is walkable and the leaf is out of the way.
///     The verbs that operate it live in the tools layer — this holds only the state.
/// </summary>
/// <remarks>
///     Opening is one-way. There is no close verb, so a door that has been worked is done with.
/// </remarks>
[DisallowMultipleComponent]
public class Door : Opening
{
    [SerializeField] private bool startOpen;

    public bool IsOpen { get; private set; }

    public override bool IsPassable => IsOpen;

    protected override void Initialise()
    {
        IsOpen = startOpen;
    }

    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        Changed();
    }
}
