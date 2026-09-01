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
    [SerializeField] private PointerInput pointer;
    [SerializeField] private OperatorPathInput pathInput;
    [SerializeField] private WaypointInput waypointInput;
    [SerializeField] private OpeningInput openingInput;
    [SerializeField] private Camera view;

    [Header("Layout, in pixels")]
    [SerializeField] private Vector2 panelSize = new(148f, 66f);
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
        if (pointer == null)
        {
            pointer = FindFirstObjectByType<PointerInput>();
        }

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

        if (pointer == null || !pointer.IsAvailable)
        {
            return;
        }

        // Opening happens on release, so the press that opened this cannot also close it.
        if (!pointer.Pressed && !pointer.RightPressed)
        {
            return;
        }

        var click = GuiPoint(pointer.ScreenPosition);
        var insidePanel = panel.Contains(click);

        // Left only activates a row. The panel sits just above its anchor, so a right-click aimed
        // at the operator or waypoint can land on it, and that gesture belongs to the target.
        if (pointer.Pressed && insidePanel)
        {
            for (var i = 0; i < rows.Count && i < rowRects.Count; i++)
            {
                if (rows[i].Enabled && rowRects[i].Contains(click))
                {
                    rows[i].Action();
                    break;
                }
            }

            return;
        }

        if (!insidePanel)
        {
            Close();
        }
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
            DrawButton(rowRects[i], rows[i].Label, rows[i].Enabled);
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
                    openingInput.Queue(target, opening, queued);
                    Close();
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
    }

    private void Layout()
    {
        BuildRows();

        var anchor = opening != null ? opening.transform.position
            : waypoint != null && waypoint.PointIndex < target.Plan.Points.Count
                ? target.Plan.Points[waypoint.PointIndex]
                : target.transform.position;

        var screen = view.WorldToScreenPoint(anchor);

        // IMGUI measures y downward from the top; screen space measures it upward from the bottom.
        var top = Screen.height - screen.y - gapAboveOperator - panelSize.y;

        panel = new Rect(screen.x - panelSize.x * 0.5f, top, panelSize.x, panelSize.y);

        var count = Mathf.Max(1, rows.Count);
        var rowHeight = (panelSize.y - padding * (count + 1)) / count;
        var width = panelSize.x - padding * 2f;

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

    private void DrawButton(Rect rect, string label, bool enabled)
    {
        var previous = GUI.color;

        // Greyed rather than hidden, so a disabled action still shows it exists.
        GUI.color = enabled ? previous : new Color(previous.r, previous.g, previous.b, 0.4f);
        GUI.Box(rect, label);
        GUI.color = previous;
    }

    private static Vector2 GuiPoint(Vector2 screenPosition)
    {
        return new Vector2(screenPosition.x, Screen.height - screenPosition.y);
    }
}

