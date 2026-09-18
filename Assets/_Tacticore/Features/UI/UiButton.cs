using System;
using UnityEngine.UIElements;

/// <summary>
///     Makes a plain element behave as a button.
/// </summary>
/// <remarks>
///     Deliberately not <see cref="Button" />. The default runtime theme skins buttons with its own
///     background, borders and text colour, which outranks the project's stylesheet and leaves stock
///     Unity buttons sitting in the middle of a dark panel. Everything clickable in this interface
///     is a styled element with a click handler instead, which is also how the squad cards and the
///     action menu rows are built.
/// </remarks>
public static class UiButton
{
    public static void Bind(VisualElement element, Action action)
    {
        if (element == null || action == null)
        {
            return;
        }

        element.RegisterCallback<ClickEvent>(_ => action());
    }
}
