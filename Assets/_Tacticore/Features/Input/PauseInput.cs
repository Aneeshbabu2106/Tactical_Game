using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     Space toggles the simulation between paused and running.
/// </summary>
/// <remarks>
///     Play begins paused so the player can plan, which means every order given before the first
///     Space appears to do nothing at all — the path is drawn, the door is queued, and nobody moves.
///     Saying so is the mission bar's job now; this is only the key.
/// </remarks>
[DisallowMultipleComponent]
public class PauseInput : MonoBehaviour
{
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
}
