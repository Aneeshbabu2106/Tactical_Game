using System;
using UnityEngine;

/// <summary>
///     Runtime state and presentation for a single door. Lives on the prefab that
///     <see cref="DoorRuleTile" /> spawns, and is handed its cell by that tile's StartUp.
/// </summary>
[DisallowMultipleComponent]
public class Door : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Collider2D blocker;
    [SerializeField] private bool startOpen;

    private bool registered;

    public Vector3Int Cell { get; private set; }
    public bool IsVertical { get; private set; }
    public bool IsOpen { get; private set; }

    public event Action<Door> StateChanged;

    private void Awake()
    {
        EnsureBlocker();
        IsOpen = startOpen;
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

    /// <summary>
    ///     Called by <see cref="DoorRuleTile.StartUp" />. Registration is play-mode only so the
    ///     editor's preview instances never enter the registry.
    /// </summary>
    public void Place(Vector3Int cell, bool isVertical)
    {
        Unregister();

        Cell = cell;
        IsVertical = isVertical;

        if (Application.isPlaying)
        {
            DoorRegistry.Register(cell, this);
            registered = true;
        }

        ApplyState();
    }

    public void Open()
    {
        SetOpen(true);
    }

    public void Close()
    {
        SetOpen(false);
    }

    public void Toggle()
    {
        SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        if (IsOpen == open)
        {
            return;
        }

        IsOpen = open;
        ApplyState();
        StateChanged?.Invoke(this);
    }

    private void ApplyState()
    {
        if (panel != null)
        {
            panel.SetActive(!IsOpen);
        }

        if (blocker != null)
        {
            blocker.enabled = !IsOpen;
        }
    }

    /// <summary>
    ///     The art prefabs ship without a collider, so give the door one that fills its cell.
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

        DoorRegistry.Unregister(Cell, this);
        registered = false;
    }
}
