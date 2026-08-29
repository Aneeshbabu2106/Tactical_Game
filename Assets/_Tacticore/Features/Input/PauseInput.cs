using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     Space toggles the simulation between paused and running.
/// </summary>
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
