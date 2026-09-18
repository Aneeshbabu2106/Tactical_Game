using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     Draws the context menu next to whatever was clicked, and keeps it on screen.
/// </summary>
/// <remarks>
///     Presses are resolved by the panel's own event system, in the panel's own coordinates. The
///     IMGUI version this replaces went wrong twice over exactly that: a hand-rolled hit test in
///     converted coordinates that quietly disagreed with where the rows were drawn.
/// </remarks>
public sealed class ActionMenuView
{
    private readonly VisualElement panel;
    private readonly List<Label> rowLabels = new();

    /// <summary>Kept clear of the screen edge by this much, in panel pixels.</summary>
    private const float Margin = 8f;

    public ActionMenuView(VisualElement root)
    {
        panel = root.Q<VisualElement>("action-menu");
        Hide();
    }

    public void Hide()
    {
        panel.style.display = DisplayStyle.None;
    }

    /// <summary>
    ///     Shows the rows at a screen position, with the origin at the bottom left as the camera
    ///     reports it. <see cref="PanelSpace" /> puts it into the panel's own top-down coordinates.
    /// </summary>
    public void Show(IReadOnlyList<ActionRow> rows, Vector2 screenPosition)
    {
        if (rows.Count == 0)
        {
            Hide();
            return;
        }

        Fit(rows);

        panel.style.display = DisplayStyle.Flex;

        var at = PanelSpace.FromScreen(panel.panel, screenPosition);
        var size = panel.layout;

        // The layout is a frame behind on the first show, so fall back to a sensible guess rather
        // than clamping against a zero-sized rect and pinning the menu to the corner.
        var width = float.IsNaN(size.width) || size.width <= 0f ? 168f : size.width;
        var height = float.IsNaN(size.height) || size.height <= 0f ? rows.Count * 26f + 8f : size.height;

        var root = panel.parent ?? panel;
        var bounds = root.layout;

        var x = at.x - width * 0.5f;
        var y = at.y - height - 18f;

        // Below the thing clicked when there is no room above it.
        if (y < Margin)
        {
            y = at.y + 18f;
        }

        if (!float.IsNaN(bounds.width) && bounds.width > 0f)
        {
            x = Mathf.Clamp(x, Margin, bounds.width - width - Margin);
            y = Mathf.Clamp(y, Margin, Mathf.Max(Margin, bounds.height - height - Margin));
        }

        panel.style.left = x;
        panel.style.top = y;
    }

    /// <summary>
    ///     Rows are pooled and relabelled. Rebuilding them every frame would drop the hover and
    ///     cancel a click in progress, and the rows are rebuilt every frame so a label can track a
    ///     reload counting up.
    /// </summary>
    /// <remarks>
    ///     Plain elements rather than <see cref="Button" />s. A Button carries the default theme's
    ///     skin — including a background <em>image</em> — which outranks anything set here and
    ///     leaves the rows looking like stock Unity buttons in the middle of a dark panel. Nothing
    ///     here needs what Button adds, and the squad cards are built the same way.
    /// </remarks>
    private void Fit(IReadOnlyList<ActionRow> rows)
    {
        while (rowLabels.Count < rows.Count)
        {
            var label = new Label();
            label.AddToClassList("action-row");
            panel.Add(label);
            rowLabels.Add(label);
        }

        for (var i = 0; i < rowLabels.Count; i++)
        {
            var label = rowLabels[i];

            if (i >= rows.Count)
            {
                label.style.display = DisplayStyle.None;
                continue;
            }

            var row = rows[i];

            label.style.display = DisplayStyle.Flex;
            label.text = row.Label;
            label.SetEnabled(row.Enabled);

            // Rebuilt rather than added to: the row's closure captures the state of the thing the
            // menu is open on, and that is rebuilt every frame. A disabled row keeps no callback at
            // all, so a dimmed row cannot be pressed even if it is somehow reached.
            label.UnregisterCallback<ClickEvent>(OnRowClicked);

            if (row.Enabled)
            {
                label.userData = row.Action;
                label.RegisterCallback<ClickEvent>(OnRowClicked);
            }
            else
            {
                label.userData = null;
            }
        }
    }

    private static void OnRowClicked(ClickEvent evt)
    {
        if (evt.currentTarget is VisualElement { userData: System.Action action })
        {
            action();
        }
    }
}
