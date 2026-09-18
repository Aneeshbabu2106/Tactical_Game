using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     Owns the whole interface: the main menu, deployment, the in-mission panels, and the results.
/// </summary>
/// <remarks>
///     Built on UI Toolkit. The frame is UXML and the styling is USS, so the layout can be moved
///     around without touching code; the lists that vary in length — squad cards, menu rows, entry
///     points, stat lines — are built here against the same USS classes.
///     <para>
///         Every screen lives in one document rather than one document per screen. They are never
///         visible at the same time, they share a palette and a stylesheet, and one panel means one
///         place that answers whether the cursor is over the interface.
///     </para>
///     <para>
///         Which screen is up is <see cref="MissionState" />'s to say, not this one's. The interface
///         asks and draws; it does not decide when a mission has been won.
///     </para>
/// </remarks>
[RequireComponent(typeof(UIDocument))]
[DisallowMultipleComponent]
public class GameplayHud : MonoBehaviour
{
    [SerializeField] private MissionState mission;
    [SerializeField] private OperatorSelection selection;
    [SerializeField] private PointerInput pointer;
    [SerializeField] private MapCamera mapCamera;
    [SerializeField] private DeployMarkers deployMarkers;

    [Tooltip("Seconds between sweeps for operators and enemies, rather than every frame.")]
    [SerializeField] private float rescanInterval = 0.5f;

    private readonly List<Operator> operators = new();
    private readonly List<Enemy> enemies = new();

    private VisualElement root;
    private VisualElement gameplay;
    private VisualElement overlay;
    private VisualElement mainMenu;
    private VisualElement deployment;
    private VisualElement results;

    private MissionBar missionBar;
    private SquadBar squadBar;
    private OperatorPanel operatorPanel;
    private DeploymentScreen deployScreen;
    private ResultsScreen resultsScreen;

    private GameScreen shown = (GameScreen)(-1);
    private float elapsed;
    private float nextScan;

    /// <summary>The context menu, for <see cref="OperatorActionMenu" /> to drive.</summary>
    public ActionMenuView Menu { get; private set; }

    private void Awake()
    {
        if (mission == null)
        {
            mission = FindFirstObjectByType<MissionState>();
        }

        if (selection == null)
        {
            selection = FindFirstObjectByType<OperatorSelection>();
        }

        if (pointer == null)
        {
            pointer = FindFirstObjectByType<PointerInput>();
        }

        if (mapCamera == null)
        {
            mapCamera = FindFirstObjectByType<MapCamera>();
        }

        if (deployMarkers == null)
        {
            deployMarkers = FindFirstObjectByType<DeployMarkers>();
        }
    }

    private void OnEnable()
    {
        root = GetComponent<UIDocument>().rootVisualElement;

        if (root == null || mission == null)
        {
            Debug.LogError($"{name}: needs a visual tree and a MissionState. Disabling.", this);
            enabled = false;
            return;
        }

        gameplay = root.Q<VisualElement>("gameplay");
        overlay = root.Q<VisualElement>("overlay");
        mainMenu = root.Q<VisualElement>("main-menu");
        deployment = root.Q<VisualElement>("deployment");
        results = root.Q<VisualElement>("results");

        missionBar = new MissionBar(root);
        squadBar = new SquadBar(root.Q<VisualElement>("squad-bar"), Select);
        operatorPanel = new OperatorPanel(root);
        Menu = new ActionMenuView(root);

        UiButton.Bind(root.Q<Label>("menu-start"), () => mission.Show(GameScreen.Deployment));
        UiButton.Bind(root.Q<Label>("menu-exit"), mission.Quit);

        deployScreen = new DeploymentScreen(
            root, mission, pointer, deployMarkers,
            area =>
            {
                if (mapCamera != null)
                {
                    mapCamera.FrameInto(area);
                }
            },
            () => mission.Show(GameScreen.MainMenu),
            mission.Deploy);

        resultsScreen = new ResultsScreen(root, mission, mission.ToMenu, mission.Retry);

        if (pointer != null)
        {
            pointer.UiBlocksPointer = OverUi;
        }

        // The map's markers and the panel's list are two ways of making the same choice.
        mission.ChoiceChanged += OnChoiceChanged;
        mission.DeployRefused += OnDeployRefused;
    }

    private void OnDisable()
    {
        // Left set, every click in the world would be swallowed by a panel that is no longer drawn.
        if (pointer != null)
        {
            pointer.UiBlocksPointer = null;
        }

        if (mission != null)
        {
            mission.ChoiceChanged -= OnChoiceChanged;
            mission.DeployRefused -= OnDeployRefused;
        }
    }

    private void OnChoiceChanged()
    {
        if (shown == GameScreen.Deployment)
        {
            deployScreen.Refresh();
        }
    }

    private void OnDeployRefused()
    {
        if (shown == GameScreen.Deployment)
        {
            deployScreen.Refuse();
        }
    }

    private void Update()
    {
        Follow(mission.Screen);

        if (mission.Screen == GameScreen.Deployment)
        {
            deployScreen.Tick();
        }

        if (mission.Screen != GameScreen.Mission)
        {
            return;
        }

        // The simulated clock, not the wall clock: play begins paused so the player can plan, and a
        // mission timer that ran while nothing moved would be timing the player, not the mission.
        elapsed = mission.Elapsed;

        Rescan();

        missionBar.Update(elapsed, enemies, operators);
        squadBar.Update(operators);
        operatorPanel.Update(selection != null ? selection.Selected : null);
    }

    /// <summary>
    ///     Shows whichever screen the mission says. Building on the change rather than every frame:
    ///     deployment's choices cannot move while it is up, and the results are of something that
    ///     has already finished happening.
    /// </summary>
    private void Follow(GameScreen screen)
    {
        if (shown == screen)
        {
            return;
        }

        shown = screen;

        var playing = screen == GameScreen.Mission;

        // Deployment is the odd one out: its panel sits to one side and the map stays live, because
        // the entry points are chosen by clicking them on the building. The other two are modal, so
        // they dim the map and take every click with them.
        var modal = screen == GameScreen.MainMenu || screen == GameScreen.Results;

        overlay.EnableInClassList("dim", modal);
        overlay.pickingMode = modal ? PickingMode.Position : PickingMode.Ignore;

        Display(gameplay, playing);
        Display(overlay, !playing);
        Display(mainMenu, screen == GameScreen.MainMenu);
        Display(deployment, screen == GameScreen.Deployment);
        Display(results, screen == GameScreen.Results);

        if (!playing)
        {
            Menu.Hide();
        }
        else if (mapCamera != null)
        {
            // The deployment panels are gone; the map gets the whole screen back.
            mapCamera.ClearFocus();
        }

        switch (screen)
        {
            case GameScreen.Deployment:
                // Framing happens inside, once the side panels have been laid out and it is known
                // how much of the screen is left for the building.
                deployScreen.Build();
                break;

            case GameScreen.Results:
                resultsScreen.Build();
                break;
        }
    }

    private static void Display(VisualElement element, bool visible)
    {
        if (element != null)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void Select(Operator op)
    {
        if (selection != null)
        {
            selection.Select(op);
        }
    }

    /// <summary>
    ///     Whether a screen position lands on the interface. Everything that is only layout is
    ///     picking-mode="Ignore" in the UXML, so the gaps between panels pick nothing and the map
    ///     stays clickable through them — except under a menu, which covers the map on purpose.
    /// </summary>
    private bool OverUi(Vector2 screenPosition)
    {
        var panel = root?.panel;

        if (panel == null)
        {
            return false;
        }

        return panel.Pick(PanelSpace.FromScreen(panel, screenPosition)) != null;
    }

    private void Rescan()
    {
        if (Time.unscaledTime < nextScan && operators.Count > 0)
        {
            return;
        }

        nextScan = Time.unscaledTime + rescanInterval;

        operators.Clear();
        operators.AddRange(FindObjectsByType<Operator>(FindObjectsSortMode.InstanceID));

        enemies.Clear();
        enemies.AddRange(FindObjectsByType<Enemy>(FindObjectsSortMode.InstanceID));
    }
}
