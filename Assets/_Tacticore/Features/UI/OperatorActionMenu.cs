using UnityEngine;

/// <summary>
///     A small action panel that opens above an operator on right-click and closes when you click
///     away from it.
/// </summary>
/// <remarks>
///     Drawn with IMGUI and hit-tested against <see cref="PointerInput" /> rather than built from
///     uGUI. The scene has no Canvas or EventSystem, and standing those up — plus an
///     InputSystemUIInputModule, since the old Input Manager is disabled — is a lot of apparatus for
///     a placeholder menu. Hit-testing through PointerInput also keeps every click in this project
///     on one code path.
/// </remarks>
public class OperatorActionMenu : MonoBehaviour
{
    [SerializeField] private PointerInput pointer;
    [SerializeField] private OperatorPathInput pathInput;
    [SerializeField] private Camera view;

    [Header("Layout, in pixels")]
    [SerializeField] private Vector2 panelSize = new(148f, 66f);
    [SerializeField] private float gapAboveOperator = 22f;
    [SerializeField] private float padding = 6f;

    private Operator target;
    private Rect panel;
    private Rect speedButton;
    private Rect clearButton;

    public bool IsOpen => target != null;

    private void Awake()
    {
        if (pointer == null)
        {
            pointer = FindFirstObjectByType<PointerInput>();
        }

        if (pathInput == null)
        {
            pathInput = FindFirstObjectByType<OperatorPathInput>();
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
            pathInput.Clicked += Open;
        }
    }

    private void OnDisable()
    {
        if (pathInput != null)
        {
            pathInput.Clicked -= Open;
        }
    }

    private void Update()
    {
        // The operator can be destroyed while its menu is up.
        if (target == null)
        {
            return;
        }

        Layout();

        if (pointer == null || !pointer.IsAvailable)
        {
            return;
        }

        // Opening happens on release, so the press that opened this cannot also close it.
        if (!pointer.Pressed && !pointer.RightPressed)
        {
            return;
        }

        var click = GuiPoint(pointer.ScreenPosition);
        var insidePanel = panel.Contains(click);

        // Left only activates a row. The panel sits just above the operator, so a right-click
        // aimed at the operator can land on it, and that gesture belongs to the operator.
        if (pointer.Pressed && insidePanel)
        {
            if (speedButton.Contains(click))
            {
                target.ToggleRunning();
            }
            else if (clearButton.Contains(click) && target.IsMoving)
            {
                target.ClearPath();
            }

            return;
        }

        if (!insidePanel)
        {
            Close();
        }
    }

    private void OnGUI()
    {
        if (target == null)
        {
            return;
        }

        GUI.Box(panel, GUIContent.none);

        DrawButton(speedButton, target.IsRunning ? "RUN" : "WALK", true);
        DrawButton(clearButton, "CLEAR PATH", target.IsMoving);
    }

    public void Open(Operator op)
    {
        target = op;
    }

    public void Close()
    {
        target = null;
    }

    private void Layout()
    {
        var screen = view.WorldToScreenPoint(target.transform.position);

        // IMGUI measures y downward from the top; screen space measures it upward from the bottom.
        var top = Screen.height - screen.y - gapAboveOperator - panelSize.y;

        panel = new Rect(screen.x - panelSize.x * 0.5f, top, panelSize.x, panelSize.y);

        var rowHeight = (panelSize.y - padding * 3f) * 0.5f;
        var width = panelSize.x - padding * 2f;

        speedButton = new Rect(panel.x + padding, panel.y + padding, width, rowHeight);
        clearButton = new Rect(panel.x + padding, speedButton.yMax + padding, width, rowHeight);
    }

    private void DrawButton(Rect rect, string label, bool enabled)
    {
        var previous = GUI.color;

        // Greyed rather than hidden, so a disabled action still shows it exists.
        GUI.color = enabled ? previous : new Color(previous.r, previous.g, previous.b, 0.4f);
        GUI.Box(rect, label);
        GUI.color = previous;
    }

    private static Vector2 GuiPoint(Vector2 screenPosition)
    {
        return new Vector2(screenPosition.x, Screen.height - screenPosition.y);
    }
}
