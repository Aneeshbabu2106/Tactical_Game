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

    public bool Pressed { get; private set; }

    public bool Held { get; private set; }

    public bool Released { get; private set; }

    public bool RightPressed { get; private set; }

    public bool RightHeld { get; private set; }

    public bool RightReleased { get; private set; }

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
            return;
        }

        var world = view.ScreenToWorldPoint(mouse.position.ReadValue());
        world.z = 0f;
        WorldPosition = world;

        Pressed = mouse.leftButton.wasPressedThisFrame;
        Held = mouse.leftButton.isPressed;
        Released = mouse.leftButton.wasReleasedThisFrame;

        RightPressed = mouse.rightButton.wasPressedThisFrame;
        RightHeld = mouse.rightButton.isPressed;
        RightReleased = mouse.rightButton.wasReleasedThisFrame;
    }
}
