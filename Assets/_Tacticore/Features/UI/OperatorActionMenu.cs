using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Decides what the context menu offers and what it is anchored to. The drawing is
///     <see cref="ActionMenuView" />'s.
/// </summary>
/// <remarks>
///     Split that way because the two halves fail differently. What a door offers depends on its
///     state and changes every frame; where the panel goes and how a press resolves is a UI Toolkit
///     concern and belongs with the panel. Keeping them in one IMGUI file is what let a hand-rolled
///     hit test drift out of step with the rows it was testing.
/// </remarks>
public class OperatorActionMenu : MonoBehaviour
{
    [SerializeField] private GameplayHud hud;
    [SerializeField] private OperatorPathInput pathInput;
    [SerializeField] private WaypointInput waypointInput;
    [SerializeField] private OpeningInput openingInput;
    [SerializeField] private PointerInput pointer;
    [SerializeField] private Camera view;

    private readonly List<ActionRow> rows = new();

    private Operator target;
    private Waypoint waypoint;
    private Opening opening;
    private int openedOnFrame = -1;

    public bool IsOpen => target != null;

    private void Awake()
    {
        if (hud == null)
        {
            hud = FindFirstObjectByType<GameplayHud>();
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

        if (pointer == null)
        {
            pointer = FindFirstObjectByType<PointerInput>();
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

        hud?.Menu?.Hide();
    }

    private void LateUpdate()
    {
        // Tells the opening tool to sit out while this panel is up: it covers the very cell that
        // opened it, so a click on a row would otherwise be read as another click on the door.
        if (openingInput != null)
        {
            openingInput.Suppressed = IsOpen;
        }

        if (hud == null || hud.Menu == null)
        {
            return;
        }

        // The operator can be destroyed, or shot, while his menu is up.
        if (target == null || !target.IsAlive)
        {
            Close();
            hud.Menu.Hide();
            return;
        }

        // The door swung, or the glass went, while the panel was still showing.
        if (opening != null && !OpeningActions.HasAny(opening))
        {
            Close();
            hud.Menu.Hide();
            return;
        }

        // A press anywhere in the world closes it. Presses that land on a panel never reach here at
        // all — PointerInput swallows those — so this needs no hit test of its own. The frame guard
        // is for the press that opened the menu in the first place.
        if (pointer != null && pointer.Pressed && Time.frameCount != openedOnFrame)
        {
            Close();
            hud.Menu.Hide();
            return;
        }

        BuildRows();
        hud.Menu.Show(rows, view.WorldToScreenPoint(Anchor()));
    }

    public void OpenForOperator(Operator op)
    {
        target = op;
        waypoint = null;
        opening = null;
        openedOnFrame = Time.frameCount;
    }

    public void OpenForWaypoint(Operator op, Waypoint wp)
    {
        target = op;
        waypoint = wp;
        opening = null;
        openedOnFrame = Time.frameCount;
    }

    public void OpenForOpening(Operator op, Opening hit)
    {
        target = op;
        waypoint = null;
        opening = hit;
        openedOnFrame = Time.frameCount;
    }

    public void Close()
    {
        target = null;
        waypoint = null;
        opening = null;
    }

    /// <summary>What the panel points at: the door, the waypoint, or the man.</summary>
    private Vector3 Anchor()
    {
        if (opening != null)
        {
            return opening.transform.position;
        }

        if (waypoint != null && target.Plan != null && waypoint.PointIndex < target.Plan.Points.Count)
        {
            return target.Plan.Points[waypoint.PointIndex];
        }

        return target.transform.position;
    }

    /// <summary>
    ///     Rebuilt every frame so labels and enabled state track the thing the menu is open on,
    ///     rather than freezing at the moment it opened. A reload counting up is the visible case.
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

                rows.Add(new ActionRow(queued.Label, openingInput != null, () =>
                {
                    // Refused when no route reaches the opening. Say so and stay open, rather than
                    // closing on a click that did nothing.
                    if (openingInput.Queue(target, opening, queued))
                    {
                        Close();
                        hud.Menu.Hide();
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
            rows.Add(new ActionRow(waypoint.Run ? "RUN" : "WALK", true, () => waypoint.Run = !waypoint.Run));
            rows.Add(new ActionRow("CANCEL WAYPOINT", true, () =>
            {
                target.Plan.Remove(waypoint);
                target.PathChanged();
                Close();
                hud.Menu.Hide();
            }));

            return;
        }

        rows.Add(new ActionRow(target.IsRunning ? "RUN" : "WALK", true, target.ToggleRunning));
        rows.Add(new ActionRow("CLEAR PATH", target.IsMoving, target.ClearPath));
        rows.Add(new ActionRow(Label(target.Engagement), true, target.CycleEngagement));

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

            rows.Add(new ActionRow(label, !weapon.IsReloading && !weapon.IsFull, () => weapon.BeginReload()));
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
}
