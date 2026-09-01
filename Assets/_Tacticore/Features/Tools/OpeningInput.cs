using System;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
///     Left-click a door or window to open its action menu. Also outlines whichever opening is under
///     the cursor, so they read as things you can act on rather than as scenery. An opening with
///     nothing left to do stops registering with the router, so it neither highlights nor responds.
/// </summary>
public class OpeningInput : MonoBehaviour
{
    [SerializeField] private PointerInput pointer;
    [SerializeField] private PointerRouter router;
    [SerializeField] private Tilemap navigation;

    [SerializeField] private Color hoverColor = new(1f, 0.85f, 0.35f, 0.9f);
    [SerializeField] private float hoverWidth = 0.05f;

    [Tooltip("A press that moves more than this is a drag, not a click on the opening.")]
    [SerializeField] private float clickThreshold = 0.25f;

    /// <summary>Raised on a click of an opening, with the operator that would work it.</summary>
    public event Action<Operator, Opening> OpeningClicked;

    private LineRenderer outline;
    private Opening pressed;
    private Vector3 pressedAt;

    /// <summary>
    ///     Set by the menu while it is showing. Its panel sits directly over the opening it belongs
    ///     to, so without this a click on a menu row would also read as a fresh click on the door
    ///     underneath and reopen the menu on release.
    /// </summary>
    public bool Suppressed { get; set; }

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

        EnsureOutline();
    }

    private void Update()
    {
        if (pointer == null || !pointer.IsAvailable || router == null)
        {
            return;
        }

        var cursor = pointer.WorldPosition;
        var opening = router.Kind == PointerTargetKind.Opening ? router.Opening : null;

        DrawOutline(opening);

        if (pointer.Pressed)
        {
            pressed = Suppressed ? null : opening;
            pressedAt = cursor;
            return;
        }

        if (!pointer.Released)
        {
            return;
        }

        // Fired on release, like every other click in this project. Opening the menu on the press
        // would have the menu see that same press as a click outside itself and close immediately.
        var hit = pressed;
        pressed = null;

        if (hit == null || hit != opening || Vector3.Distance(cursor, pressedAt) >= clickThreshold)
        {
            return;
        }

        // Whoever is nearest works the opening. With one operator this is trivially him; the
        // prototype picks the nearest man who can actually do something, which needs a squad first.
        var op = OperatorPicker.Nearest(cursor);

        if (op != null)
        {
            OpeningClicked?.Invoke(op, hit);
        }
    }

    /// <summary>Queues a verb, using this component's navigation map. Returns false if refused.</summary>
    public bool Queue(Operator op, Opening opening, OpeningVerb verb)
    {
        return OpeningActions.Queue(op, opening, verb, navigation);
    }

    private void DrawOutline(Opening opening)
    {
        if (outline == null)
        {
            return;
        }

        if (opening == null || navigation == null)
        {
            outline.enabled = false;
            return;
        }

        outline.enabled = true;

        var centre = navigation.GetCellCenterWorld(opening.Cell);
        var half = (Vector3)navigation.cellSize * 0.5f;

        // Closed loop: the fifth point repeats the first so the last corner is mitred like the rest.
        outline.SetPosition(0, centre + new Vector3(-half.x, -half.y, 0f));
        outline.SetPosition(1, centre + new Vector3(half.x, -half.y, 0f));
        outline.SetPosition(2, centre + new Vector3(half.x, half.y, 0f));
        outline.SetPosition(3, centre + new Vector3(-half.x, half.y, 0f));
        outline.SetPosition(4, centre + new Vector3(-half.x, -half.y, 0f));
    }

    private void EnsureOutline()
    {
        if (outline != null)
        {
            return;
        }

        var host = new GameObject("OpeningHighlight");
        host.transform.SetParent(transform, false);

        outline = host.AddComponent<LineRenderer>();
        outline.useWorldSpace = true;
        outline.widthMultiplier = hoverWidth;
        outline.positionCount = 5;
        outline.numCornerVertices = 2;
        outline.material = new Material(Shader.Find("Sprites/Default"));
        outline.startColor = hoverColor;
        outline.endColor = hoverColor;
        outline.sortingOrder = 103;
        outline.enabled = false;
    }
}
