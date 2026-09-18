using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     The only place in the project that talks to an input device. Publishes the pointer as plain
///     world-space state so gameplay never references the Input System, and swapping to touch,
///     gamepad or an InputActions asset stays a change inside this assembly.
/// </summary>
/// <remarks>
///     Runs early so consumers reading this state in their own Update always see the current frame.
/// </remarks>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class PointerInput : MonoBehaviour
{
    [SerializeField] private Camera view;

    /// <summary>Pointer position on the z = 0 plane, where the 2D grid lives.</summary>
    public Vector3 WorldPosition { get; private set; }

    /// <summary>Raw pointer position in pixels, for callers that must re-project it themselves.</summary>
    public Vector2 ScreenPosition { get; private set; }

    /// <summary>Wheel movement this frame in notches; positive is scroll up.</summary>
    public float Scroll { get; private set; }

    public bool Pressed { get; private set; }

    public bool Held { get; private set; }

    public bool Released { get; private set; }

    public bool RightPressed { get; private set; }

    public bool RightHeld { get; private set; }

    public bool RightReleased { get; private set; }

    public bool MiddlePressed { get; private set; }

    public bool MiddleHeld { get; private set; }

    public bool MiddleReleased { get; private set; }

    /// <summary>False when there is no mouse or no camera, so consumers can bail cleanly.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    ///     Asked whether a screen position is over the interface. Set by the HUD, which is the only
    ///     thing that knows where its panels are.
    /// </summary>
    /// <remarks>
    ///     A delegate rather than a reference to the HUD, so Input stays the bottom of the stack and
    ///     goes on knowing nothing about what is drawn on top of it. Only the press edges are
    ///     swallowed: a drag already under way when the cursor crosses a panel still finishes, which
    ///     is what you want when a path is drawn near the edge of the screen.
    /// </remarks>
    public System.Func<Vector2, bool> UiBlocksPointer { get; set; }

    /// <summary>True while the cursor is over the interface. For anything that wants to know.</summary>
    public bool OverUi { get; private set; }

    private void Awake()
    {
        if (view == null)
        {
            view = Camera.main;
        }
    }

    private void Update()
    {
        var mouse = Mouse.current;

        IsAvailable = mouse != null && view != null;

        if (!IsAvailable)
        {
            Pressed = false;
            Held = false;
            Released = false;
            RightPressed = false;
            RightHeld = false;
            RightReleased = false;
            MiddlePressed = false;
            MiddleHeld = false;
            MiddleReleased = false;
            Scroll = 0f;
            return;
        }

        ScreenPosition = mouse.position.ReadValue();

        OverUi = UiBlocksPointer != null && UiBlocksPointer(ScreenPosition);

        var world = view.ScreenToWorldPoint(ScreenPosition);
        world.z = 0f;
        WorldPosition = world;

        // Windows reports 120 per detent; dividing keeps a notch worth 1 for consumers,
        // while trackpads still come through proportionally as fractions.
        Scroll = mouse.scroll.ReadValue().y / 120f;

        // Presses are swallowed over the interface so a click on a panel does not also start a
        // gesture in the world underneath it. Held and Released are left alone deliberately.
        Pressed = !OverUi && mouse.leftButton.wasPressedThisFrame;
        Held = mouse.leftButton.isPressed;
        Released = mouse.leftButton.wasReleasedThisFrame;

        RightPressed = !OverUi && mouse.rightButton.wasPressedThisFrame;
        RightHeld = mouse.rightButton.isPressed;
        RightReleased = mouse.rightButton.wasReleasedThisFrame;

        MiddlePressed = !OverUi && mouse.middleButton.wasPressedThisFrame;
        MiddleHeld = mouse.middleButton.isPressed;
        MiddleReleased = mouse.middleButton.wasReleasedThisFrame;
    }
}
