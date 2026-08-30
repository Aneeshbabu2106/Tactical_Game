using UnityEngine;

/// <summary>
///     Right-press an operator and drag out to aim it; the look target follows the cursor and the
///     operator turns to hold it, moving or not. A right-click without dragging clears the target
///     and hands the facing back to the direction of travel.
/// </summary>
public class OperatorLookInput : MonoBehaviour
{
    [SerializeField] private PointerInput pointer;
    [SerializeField] private PointerRouter router;

    [Tooltip("Drag shorter than this counts as a click, which clears the look target.")]
    [SerializeField] private float clickThreshold = 0.25f;

    private Operator aiming;
    private Vector3 pressedAt;
    private bool dragged;

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

    private void Update()
    {
        if (pointer == null || !pointer.IsAvailable)
        {
            return;
        }

        var cursor = pointer.WorldPosition;

        if (pointer.RightPressed && router != null && router.Kind == PointerTargetKind.Operator)
        {
            BeginAim(cursor);
        }
        else if (aiming != null && pointer.RightHeld)
        {
            ExtendAim(cursor);
        }
        else if (aiming != null && pointer.RightReleased)
        {
            CommitAim();
        }
    }

    private void BeginAim(Vector3 cursor)
    {
        aiming = OperatorPicker.At(cursor);

        if (aiming == null)
        {
            return;
        }

        pressedAt = cursor;
        dragged = false;
    }

    private void ExtendAim(Vector3 cursor)
    {
        if (!dragged && Vector3.Distance(cursor, pressedAt) < clickThreshold)
        {
            return;
        }

        // Past the threshold once, stay in drag mode for the rest of the gesture.
        dragged = true;
        aiming.SetLookTarget(cursor);
    }

    private void CommitAim()
    {
        if (!dragged)
        {
            aiming.SetLookTarget(null);
        }

        aiming = null;
    }
}
