using System;
using UnityEngine;

/// <summary>
///     A cell in the building shell that can change whether it may be crossed — a door that opens, a
///     window that breaks. Lives on the prefab its rule tile spawns, and is handed its cell by
///     <see cref="NavigationRuleTile.StartUp" />.
/// </summary>
/// <remarks>
///     The prototype keeps these on tile edges, with a direction alongside every reference. Ours are
///     cells, so an opening is one object at one position and the two sides fall out of its
///     orientation. Doors and windows differ only in what makes them passable and in the verbs they
///     offer; everything else — placement, registration, the blocker, the presentation swap — is
///     here.
/// </remarks>
public abstract class Opening : MonoBehaviour
{
    [SerializeField] protected GameObject panel;
    [SerializeField] protected Collider2D blocker;

    private bool registered;

    public Vector3Int Cell { get; private set; }

    /// <summary>
    ///     True when the opening sits in a wall running north to south, which means it is crossed
    ///     east to west. Decides which two cells an operator can work from.
    /// </summary>
    public bool IsVertical { get; private set; }

    /// <summary>Whether a unit can cross this cell right now. The one thing NavigationQuery asks.</summary>
    public abstract bool IsPassable { get; }

    /// <summary>The two cells an operator can stand in to reach this opening, either side of it.</summary>
    public Vector3Int NearSide => Cell + (IsVertical ? Vector3Int.left : Vector3Int.down);

    public Vector3Int FarSide => Cell + (IsVertical ? Vector3Int.right : Vector3Int.up);

    public event Action<Opening> StateChanged;

    private void Awake()
    {
        EnsureBlocker();
        Initialise();
        ApplyState();
    }

    private void OnDestroy()
    {
        Unregister();
    }

    private void Reset()
    {
        if (panel == null && transform.childCount > 0)
        {
            panel = transform.GetChild(0).gameObject;
        }

        blocker = GetComponent<Collider2D>();
    }

    /// <summary>Read the authored starting state. Runs before the first <see cref="ApplyState" />.</summary>
    protected virtual void Initialise()
    {
    }

    /// <summary>
    ///     Called by <see cref="NavigationRuleTile.StartUp" />. Registration is play-mode only so the
    ///     editor's preview instances never enter the registry.
    /// </summary>
    public void Place(Vector3Int cell, bool isVertical)
    {
        Unregister();

        Cell = cell;
        IsVertical = isVertical;

        if (Application.isPlaying)
        {
            OpeningRegistry.Register(cell, this);
            registered = true;
        }

        ApplyState();
    }

    /// <summary>
    ///     Hides the panel and unblocks the cell once the opening is passable.
    /// </summary>
    /// <remarks>
    ///     The panel is the only thing that draws the leaf. The rule tile deliberately renders no
    ///     sprite of its own for a door or a window cell — it used to draw the door on top of the
    ///     prefab's copy, which meant hiding the panel left the tile's duplicate standing in the
    ///     doorway.
    /// </summary>
    protected void ApplyState()
    {
        if (panel != null)
        {
            panel.SetActive(!IsPassable);
        }

        if (blocker != null)
        {
            blocker.enabled = !IsPassable;
        }
    }

    /// <summary>Applies the new state and tells anyone watching. Subclasses call this on every change.</summary>
    protected void Changed()
    {
        ApplyState();
        StateChanged?.Invoke(this);
    }

    /// <summary>
    ///     The art prefabs ship without a collider, so give the opening one that fills its cell.
    ///     Runtime only — edit-mode preview instances stay untouched.
    /// </summary>
    private void EnsureBlocker()
    {
        if (blocker != null)
        {
            return;
        }

        blocker = GetComponent<Collider2D>();

        if (blocker != null)
        {
            return;
        }

        var box = gameObject.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;
        blocker = box;
    }

    private void Unregister()
    {
        if (!registered)
        {
            return;
        }

        OpeningRegistry.Unregister(Cell, this);
        registered = false;
    }
}
