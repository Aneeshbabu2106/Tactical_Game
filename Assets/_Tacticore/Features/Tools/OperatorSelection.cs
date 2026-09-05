using UnityEngine;

/// <summary>
///     Tracks which operator the player is working with. Pressing one selects him; pressing empty
///     ground clears the selection. The prototype's UI.selected, which it sets on the same mousedown
///     that begins a path drag.
/// </summary>
/// <remarks>
///     This shares the press with <see cref="OperatorPathInput" /> rather than competing for it.
///     The router's one-tool-per-press rule is about gestures that would contradict each other —
///     selecting an operator and starting to draw his path are the same intent.
/// </remarks>
[DisallowMultipleComponent]
public class OperatorSelection : MonoBehaviour
{
    [SerializeField] private PointerInput pointer;
    [SerializeField] private PointerRouter router;

    [Tooltip("Start with an operator already selected, so his cone is up before the first click.")]
    [SerializeField] private bool selectFirstAtStart = true;

    public Operator Selected { get; private set; }

    private void Awake()
    {
        if (pointer == null)
        {
            pointer = FindFirstObjectByType<PointerInput>();
        }

        if (router == null)
        {
            router = FindFirstObjectByType<PointerRouter>();
        }
    }

    private void Start()
    {
        // A squad game opens with someone selected; nothing here should have to be discovered by
        // clicking around to find out it exists.
        if (selectFirstAtStart && Selected == null)
        {
            Select(FindFirstObjectByType<Operator>());
        }
    }

    private void Update()
    {
        if (pointer == null || router == null || !pointer.IsAvailable || !pointer.Pressed)
        {
            return;
        }

        switch (router.Kind)
        {
            case PointerTargetKind.Operator:
                Select(router.Operator);
                break;

            // Only bare ground deselects. A press on a waypoint, a path or a door belongs to the
            // operator who owns it, and dropping the selection there would be a surprise.
            case PointerTargetKind.None:
                Select(null);
                break;
        }
    }

    public void Select(Operator op)
    {
        if (Selected == op)
        {
            return;
        }

        if (Selected != null)
        {
            Selected.SetSelected(false);
        }

        Selected = op;

        if (Selected != null)
        {
            Selected.SetSelected(true);
        }
    }
}
