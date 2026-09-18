using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     Converts a screen position into panel coordinates.
/// </summary>
/// <remarks>
///     There are two screen conventions in play and they disagree about which way is up. The input
///     system and <see cref="Camera.WorldToScreenPoint" /> both put the origin at the bottom left;
///     the panel's own coordinates run top-down. RuntimePanelUtils handles the panel's scaling but
///     not that flip, so it has to be done here — and it has to be done in one place, because the
///     two callers fail in opposite and equally confusing ways when it is missed: the menu opens at
///     the wrong end of the screen, and the cursor is judged to be over the panel it is furthest
///     from.
/// </remarks>
public static class PanelSpace
{
    public static Vector2 FromScreen(IPanel panel, Vector2 screenPosition)
    {
        var flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        return RuntimePanelUtils.ScreenToPanel(panel, flipped);
    }
}
