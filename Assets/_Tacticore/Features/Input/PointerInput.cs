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

        var world = view.ScreenToWorldPoint(ScreenPosition);
        world.z = 0f;
        WorldPosition = world;

        // Windows reports 120 per detent; dividing keeps a notch worth 1 for consumers,
        // while trackpads still come through proportionally as fractions.
        Scroll = mouse.scroll.ReadValue().y / 120f;

        Pressed = mouse.leftButton.wasPressedThisFrame;
        Held = mouse.leftButton.isPressed;
        Released = mouse.leftButton.wasReleasedThisFrame;

        RightPressed = mouse.rightButton.wasPressedThisFrame;
        RightHeld = mouse.rightButton.isPressed;
        RightReleased = mouse.rightButton.wasReleasedThisFrame;

        MiddlePressed = mouse.middleButton.wasPressedThisFrame;
        MiddleHeld = mouse.middleButton.isPressed;
        MiddleReleased = mouse.middleButton.wasReleasedThisFrame;
    }
}
