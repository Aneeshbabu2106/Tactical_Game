using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     A small action panel that opens above an operator on right-click and closes when you click
///     away from it.
/// </summary>
/// <remarks>
///     Drawn with IMGUI and hit-tested against <see cref="PointerInput" /> rather than built from
///     uGUI. The scene has no Canvas or EventSystem, and standing those up — plus an
///     InputSystemUIInputModule, since the old Input Manager is disabled — is a lot of apparatus for
///     a placeholder menu. Hit-testing through PointerInput also keeps every click in this project
///     on one code path.
/// </remarks>
public class OperatorActionMenu : MonoBehaviour
{
    [SerializeField] private OperatorPathInput pathInput;
    [SerializeField] private WaypointInput waypointInput;
    [SerializeField] private OpeningInput openingInput;
    [SerializeField] private Camera view;

    [Header("Layout, in pixels")]
    [SerializeField] private float panelWidth = 158f;

    [Tooltip("Height of one row. The panel is sized from this and the number of rows, so a door "
             + "with two verbs is not stretched to the height of the operator's four.")]
    [SerializeField] private float rowHeight = 26f;

    [SerializeField] private float gapAboveOperator = 22f;
    [SerializeField] private float padding = 6f;

    private readonly List<Row> rows = new();
    private readonly List<Rect> rowRects = new();

    private Operator target;
    private Waypoint waypoint;
    private Opening opening;
    private Rect panel;

    private readonly struct Row
    {
        public Row(string label, bool enabled, Action action)
        {
            Label = label;
            Enabled = enabled;
            Action = action;
        }

        public string Label { get; }
        public bool Enabled { get; }
        public Action Action { get; }
    }

    public bool IsOpen => target != null;

    private void Awake()
    {
        if (pathInput == null)
        {
            pathInput = FindFirstObjectByType<OperatorPathInput>();
        }

        if (waypointInput == null)
        {
            waypointInput = FindFirstObjectByType<WaypointInput>();
        }

        if (openingInput == null)
        {
            openingInput = FindFirstObjectByType<OpeningInput>();
        }


        if (view == null)
        {
            view = Camera.main;
        }
    }

    private void OnEnable()
    {
        if (pathInput != null)
        {
            pathInput.Clicked += OpenForOperator;
        }

        if (waypointInput != null)
        {
            waypointInput.WaypointClicked += OpenForWaypoint;
        }

        if (openingInput != null)
        {
            openingInput.OpeningClicked += OpenForOpening;
        }
    }

    private void OnDisable()
    {
        if (pathInput != null)
        {
            pathInput.Clicked -= OpenForOperator;
        }

        if (waypointInput != null)
        {
            waypointInput.WaypointClicked -= OpenForWaypoint;
        }

        if (openingInput != null)
        {
            openingInput.OpeningClicked -= OpenForOpening;
        }
    }

    private void Update()
    {
        // Tells the opening tool to sit out while this panel is up: it covers the very cell that
        // opened it, so a click on a row would otherwise be read as another click on the door.
        if (openingInput != null)
        {
            openingInput.Suppressed = IsOpen;
        }

        // The operator can be destroyed while its menu is up.
        if (target == null)
        {
            return;
        }

        // The door swung, or the glass went, while the panel was still showing.
        if (opening != null && !OpeningActions.HasAny(opening))
        {
            Close();
            return;
        }

        Layout();
    }

    private void OnGUI()
    {
        if (target == null)
        {
            return;
        }

        GUI.Box(panel, GUIContent.none);

        for (var i = 0; i < rows.Count && i < rowRects.Count; i++)
        {
            var row = rows[i];

            // A real IMGUI button rather than a box plus a hand-rolled hit test. It resolves the
            // press inside the GUI event loop, in GUI coordinates, so nothing depends on my
            // converting a pointer position into them correctly — which is what stopped these
            // responding at all.
            GUI.enabled = row.Enabled;
            var pressed = GUI.Button(rowRects[i], row.Label);
            GUI.enabled = true;

            if (!pressed)
            {
                continue;
            }

            // The action may close this menu, which empties the rows mid-draw.
            row.Action();
            return;
        }

        // Closing is handled here too, in the same coordinates the buttons use. Event.current
        // already carries a GUI-space mouse position, so there is no conversion to get wrong. A
        // click the buttons consumed arrives as EventType.Used and leaves this alone.
        if (Event.current.type == EventType.MouseDown && !panel.Contains(Event.current.mousePosition))
        {
            Close();
        }
    }

    public void OpenForOperator(Operator op)
    {
        target = op;
        waypoint = null;
        opening = null;
    }

    public void OpenForWaypoint(Operator op, Waypoint wp)
    {
        target = op;
        waypoint = wp;
        opening = null;
    }

    public void OpenForOpening(Operator op, Opening hit)
    {
        target = op;
        waypoint = null;
        opening = hit;
    }

    public void Close()
    {
        target = null;
        waypoint = null;
        opening = null;
    }

    /// <summary>
    ///     Rebuilt every frame so labels and enabled state track the thing the menu is open on,
    ///     rather than freezing at the moment it opened.
    /// </summary>
    private void BuildRows()
    {
        rows.Clear();

        if (opening != null)
        {
            foreach (var verb in OpeningActions.For(opening))
            {
                // Captured by value: the struct is a data row, and the loop variable moves on.
                var queued = verb;

                rows.Add(new Row(queued.Label, openingInput != null, () =>
                {
                    // Refused when no route reaches the opening. Say so and stay open, rather than
                    // closing on a click that did nothing.
                    if (openingInput.Queue(target, opening, queued))
                    {
                        Close();
                    }
                    else
                    {
                        openingInput.ShowRefused(opening);
                    }
                }));
            }

            return;
        }

        if (waypoint != null)
        {
            rows.Add(new Row(waypoint.Run ? "RUN" : "WALK", true, () => waypoint.Run = !waypoint.Run));
            rows.Add(new Row("CANCEL WAYPOINT", true, () =>
            {
                target.Plan.Remove(waypoint);
                target.PathChanged();
                Close();
            }));

            return;
        }

        rows.Add(new Row(target.IsRunning ? "RUN" : "WALK", true, target.ToggleRunning));
        rows.Add(new Row("CLEAR PATH", target.IsMoving, target.ClearPath));

        rows.Add(new Row(Label(target.Engagement), true, target.CycleEngagement));

        // Topping up before a door is the point of ordering it by hand; a dry magazine reloads
        // itself anyway.
        if (target.TryGetComponent(out OperatorCombat combat))
        {
            var weapon = combat.Weapon;

            // A reload runs on simulated time, so one ordered while planning shows 0% until the
            // game is running. The percentage is what makes that legible rather than looking stuck.
            var label = weapon.IsReloading
                ? $"RELOADING {Mathf.RoundToInt(weapon.ReloadProgress * 100f)}%"
                : weapon.IsFull
                    ? $"LOADED {weapon.Ammo}/{weapon.MagazineSize}"
                    : $"RELOAD {weapon.Ammo}/{weapon.MagazineSize}";

            rows.Add(new Row(label, !weapon.IsReloading && !weapon.IsFull, () => weapon.BeginReload()));
        }
    }

    private static string Label(EngagementMode mode)
    {
        return mode switch
        {
            EngagementMode.WaitToClear => "WAIT TO CLEAR",
            EngagementMode.HoldFire => "HOLD FIRE",
            _ => "KEEP MOVING"
        };
    }

    private void Layout()
    {
        BuildRows();

        var anchor = opening != null ? opening.transform.position
            : waypoint != null && waypoint.PointIndex < target.Plan.Points.Count
                ? target.Plan.Points[waypoint.PointIndex]
                : target.transform.position;

        var screen = view.WorldToScreenPoint(anchor);

        // The panel is sized to what is in it. Fixing its height and dividing that between the rows
        // made a one-verb window menu into a single slab of text with nothing that read as a button.
        var count = Mathf.Max(1, rows.Count);
        var height = padding + count * (rowHeight + padding);

        // IMGUI measures y downward from the top; screen space measures it upward from the bottom.
        var top = Screen.height - screen.y - gapAboveOperator - height;

        panel = new Rect(screen.x - panelWidth * 0.5f, top, panelWidth, height);

        var width = panelWidth - padding * 2f;

        rowRects.Clear();

        for (var i = 0; i < count; i++)
        {
            rowRects.Add(new Rect(
                panel.x + padding,
                panel.y + padding + i * (rowHeight + padding),
                width,
                rowHeight));
        }
    }


}

