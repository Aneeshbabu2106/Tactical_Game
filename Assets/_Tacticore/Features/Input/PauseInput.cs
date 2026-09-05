using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     Space toggles the simulation between paused and running, and says so on screen while it is
///     held.
/// </summary>
/// <remarks>
///     Play begins paused so the player can plan, which means every order given before the first
///     Space appears to do nothing at all — the path is drawn, the door is queued, and nobody moves.
///     The banner is there so that reads as "waiting" rather than "broken".
/// </remarks>
[DisallowMultipleComponent]
public class PauseInput : MonoBehaviour
{
    [SerializeField] private bool showBanner = true;

    [SerializeField] private Color bannerColor = new(1f, 0.85f, 0.35f, 0.95f);

    [Tooltip("Distance down from the top of the screen, in pixels.")]
    [SerializeField] private float bannerTop = 14f;

    private GUIStyle style;

    private void Update()
    {
        var keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            SimClock.TogglePause();
        }
    }

    private void OnGUI()
    {
        if (!showBanner || !SimClock.IsPaused)
        {
            return;
        }

        style ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 15
        };

        style.normal.textColor = bannerColor;

        GUI.Label(new Rect(0f, bannerTop, Screen.width, 24f), "PAUSED — SPACE TO GO", style);
    }
}
