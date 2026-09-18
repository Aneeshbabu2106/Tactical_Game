using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     The authority on which screen is up, whether the mission has been won or lost, and what the
///     numbers were when it ended.
/// </summary>
/// <remarks>
///     Rules ported from the prototype's checkObjectives: no enemy left standing is a win, no
///     operator left standing is a loss, and there is no third outcome. Stars come from finish(),
///     with one substitution — the prototype's third star is "no civilians harmed" and there are no
///     civilians here, so it is "undetected" instead, which the alert model can actually answer.
///     <para>
///         Retry reloads the scene rather than unwinding the world by hand. A mission touches
///         health, ammo, positions, paths, alert levels, the noise log and the sim clock; resetting
///         all of that correctly is a standing invitation to miss one, and missing one produces a
///         second run that quietly differs from the first.
///     </para>
/// </remarks>
[DisallowMultipleComponent]
public class MissionState : MonoBehaviour
{
    /// <summary>One line of the briefing's objective list, as the prototype's OBJECTIVES table has.</summary>
    [System.Serializable]
    public struct Objective
    {
        public string text;

        [Tooltip("PRIMARY loses the mission if it fails; BONUS is a star.")]
        public string tag;
    }

    [Header("Briefing")]
    [SerializeField] private string missionName = "RESIDENTIAL — WARRANT SERVICE";

    [TextArea(3, 6)]
    [SerializeField] private string briefing =
        "Single-storey residence, two interior rooms off a perimeter corridor. Three tangos "
        + "reported inside, at least one on the move. Doors are unlocked; the windows are not "
        + "barred. Go in quiet if you can — they react a good deal faster once they know.";

    [SerializeField] private Objective[] objectives =
    {
        new() { text = "Eliminate all terrorists", tag = "PRIMARY" },
        new() { text = "No friendly losses", tag = "BONUS" },
        new() { text = "Stay undetected until first contact", tag = "BONUS" }
    };

    [Tooltip("Seconds the mission should take. Beating it earns the first star.")]
    [SerializeField] private float parSeconds = 90f;

    [Tooltip("Where the operator can start. Left empty, deployment offers nothing and he stays put.")]
    [SerializeField] private DeployPoints deployPoints;

    [Tooltip("What he may carry. The first is the default.")]
    [SerializeField] private WeaponSpec[] loadout;

    [Tooltip("How many of the roster may go in. The prototype's MAX_DEPLOY.")]
    [SerializeField] private int maxDeployed = 2;

    /// <summary>Survives the scene reload that Retry does, so it can skip the menu on the way back.</summary>
    private static GameScreen resumeAt = GameScreen.MainMenu;

    /// <summary>
    ///     Statics outlive play mode in the editor — with domain reload turned off they outlive it
    ///     between sessions too — so a Retry from one run would drop the next straight into
    ///     deployment and skip the menu. SimClock and Noise are reset for the same reason.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        resumeAt = GameScreen.MainMenu;
    }

    private readonly List<Operator> squad = new();
    private readonly List<Enemy> enemies = new();

    /// <summary>The whole roster, in a fixed order, and the entry point each is going in by.</summary>
    private readonly List<Operator> roster = new();
    private readonly List<int> assigned = new();

    /// <summary>When each man was sent in, so the squad can be numbered in the order it was picked.</summary>
    private readonly List<int> sentAt = new();

    private int sequence;

    private float nextScan;

    public GameScreen Screen { get; private set; }

    public MissionOutcome Outcome { get; private set; }

    /// <summary>Simulated seconds since GO. Not wall time — the clock stops when the game does.</summary>
    public float Elapsed { get; private set; }

    public float ParSeconds => parSeconds;

    public int EnemyCount => enemies.Count;

    public int EnemiesDown { get; private set; }

    public int OperatorsLost { get; private set; }

    public int ShotsFired { get; private set; }

    public int Hits { get; private set; }

    /// <summary>True while no enemy has ever seen anybody. Lost the moment one does; never regained.</summary>
    public bool Undetected { get; private set; } = true;

    public bool StarTime => Elapsed <= parSeconds;

    public bool StarNoCasualties => OperatorsLost == 0;

    public bool StarUndetected => Undetected;

    public int Stars =>
        (StarTime ? 1 : 0) + (StarNoCasualties ? 1 : 0) + (StarUndetected ? 1 : 0);

    public DeployPoints Points => deployPoints;

    public string MissionName => missionName;

    public string Briefing => briefing;

    public IReadOnlyList<Objective> Objectives => objectives;

    /// <summary>Everyone who could go in, whether or not they are going.</summary>
    public IReadOnlyList<Operator> Roster => roster;

    public int MaxDeployed => maxDeployed;

    /// <summary>Whose insertion point the next click on the map sets.</summary>
    public int Active { get; private set; }

    /// <summary>Which entry point a man is going in by, or -1 if he is staying behind.</summary>
    public int AssignedPoint(int rosterIndex)
    {
        return rosterIndex >= 0 && rosterIndex < assigned.Count ? assigned[rosterIndex] : -1;
    }

    /// <summary>
    ///     His place in the squad, 1-based, in the order men were sent in; 0 if he is not going.
    ///     Moving him to another entry keeps his number.
    /// </summary>
    public int DeploySlot(int rosterIndex)
    {
        if (rosterIndex < 0 || rosterIndex >= assigned.Count || assigned[rosterIndex] < 0)
        {
            return 0;
        }

        var slot = 1;

        for (var i = 0; i < assigned.Count; i++)
        {
            if (i != rosterIndex && assigned[i] >= 0 && sentAt[i] < sentAt[rosterIndex])
            {
                slot++;
            }
        }

        return slot;
    }

    public int DeployedCount
    {
        get
        {
            var n = 0;

            foreach (var point in assigned)
            {
                if (point >= 0)
                {
                    n++;
                }
            }

            return n;
        }
    }

    /// <summary>How many distinct ways in the team is using, for the readout.</summary>
    public int UsedPoints
    {
        get
        {
            var seen = 0;
            var mask = 0;

            foreach (var point in assigned)
            {
                if (point < 0 || (mask & (1 << point)) != 0)
                {
                    continue;
                }

                mask |= 1 << point;
                seen++;
            }

            return seen;
        }
    }

    public void SetActive(int rosterIndex)
    {
        Active = Mathf.Clamp(rosterIndex, 0, Mathf.Max(0, roster.Count - 1));
        ChoiceChanged?.Invoke();
    }

    /// <summary>Who is going in by this entry, or -1 if nobody is.</summary>
    public int Occupant(int pointIndex)
    {
        for (var i = 0; i < assigned.Count; i++)
        {
            if (assigned[i] == pointIndex)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>A click on a marker: sends whoever is selected on the roster.</summary>
    public void AssignActiveTo(int pointIndex)
    {
        Assign(Active, pointIndex);
    }

    /// <summary>
    ///     Sends a man in by an entry and stands him on it, so the map shows the choice.
    /// </summary>
    /// <remarks>
    ///     One man per entry, as the prototype's POINTS cap them: whoever held it steps back to
    ///     standby. Refused, with <see cref="DeployRefused" /> raised, when it would put one man
    ///     over the limit — quietly dropping somebody else to make room would undo a choice the
    ///     player never touched.
    /// </remarks>
    public bool Assign(int rosterIndex, int pointIndex)
    {
        if (rosterIndex < 0 || rosterIndex >= roster.Count || deployPoints == null
            || pointIndex < 0 || pointIndex >= deployPoints.Count)
        {
            return false;
        }

        var holder = Occupant(pointIndex);

        if (holder == rosterIndex)
        {
            Active = rosterIndex;
            ChoiceChanged?.Invoke();
            return true;
        }

        // Moving between entries, or taking one over from somebody, never adds a man.
        var adds = assigned[rosterIndex] < 0 && holder < 0;

        if (adds && DeployedCount >= maxDeployed)
        {
            DeployRefused?.Invoke();
            return false;
        }

        if (holder >= 0)
        {
            assigned[holder] = -1;
            Reveal(roster[holder], false);
        }

        if (assigned[rosterIndex] < 0)
        {
            sentAt[rosterIndex] = ++sequence;
        }

        assigned[rosterIndex] = pointIndex;
        Active = rosterIndex;

        Reveal(roster[rosterIndex], true);
        Stand(roster[rosterIndex], deployPoints.Position(pointIndex));
        ChoiceChanged?.Invoke();
        return true;
    }

    /// <summary>Takes a man off the assault. Off the map too, until he is sent in again.</summary>
    public void Withdraw(int rosterIndex)
    {
        if (rosterIndex < 0 || rosterIndex >= assigned.Count)
        {
            return;
        }

        assigned[rosterIndex] = -1;
        Reveal(roster[rosterIndex], false);
        ChoiceChanged?.Invoke();
    }

    /// <summary>
    ///     Only men who are going in stand on the map. One left at his scene position would look
    ///     like a choice the player made.
    /// </summary>
    private static void Reveal(Operator op, bool visible)
    {
        if (op != null && op.gameObject.activeSelf != visible)
        {
            op.gameObject.SetActive(visible);
        }
    }

    private static void Stand(Operator op, Vector3 at)
    {
        if (op == null || op.Motor == null)
        {
            return;
        }

        op.Motor.MoveTo(at);
        op.ClearPath();
    }

    public int SelectedWeapon { get; private set; }

    public WeaponSpec Weapon =>
        loadout != null && SelectedWeapon >= 0 && SelectedWeapon < loadout.Length
            ? loadout[SelectedWeapon]
            : null;

    /// <summary>Raised whenever who-goes-where changes, so the roster and the markers redraw.</summary>
    public event System.Action ChoiceChanged;

    /// <summary>Raised when a deployment is turned down for being over the squad limit.</summary>
    public event System.Action DeployRefused;

    public void SelectWeapon(int index)
    {
        if (loadout == null || loadout.Length == 0)
        {
            return;
        }

        SelectedWeapon = Mathf.Clamp(index, 0, loadout.Length - 1);
        ChoiceChanged?.Invoke();
    }

    public IReadOnlyList<WeaponSpec> Loadout => loadout;

    private void Awake()
    {
        if (deployPoints == null)
        {
            deployPoints = FindFirstObjectByType<DeployPoints>();
        }

        Screen = resumeAt;
        resumeAt = GameScreen.MainMenu;

        // Fixed for the run, and in a stable order: the roster chips and the assignments are both
        // indexed by it, and a rescan that reordered them would silently swap two men's entries.
        roster.AddRange(FindObjectsByType<Operator>(FindObjectsSortMode.InstanceID));

        // By name, so the cards read in the order the scene lists the men, not creation order.
        roster.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        for (var i = 0; i < roster.Count; i++)
        {
            assigned.Add(-1);
            sentAt.Add(0);
        }

        // Nothing moves behind a menu, and the mission starts paused for planning either way.
        SimClock.SetPaused(true);
    }

    public void Show(GameScreen screen)
    {
        Screen = screen;

        if (screen == GameScreen.Deployment)
        {
            // Only the men already sent in are on the map, standing on their entries. The first
            // man is active, so the first click on a marker has somebody to send.
            for (var i = 0; i < roster.Count; i++)
            {
                var going = assigned[i] >= 0;
                Reveal(roster[i], going);

                if (going)
                {
                    Stand(roster[i], deployPoints.Position(assigned[i]));
                }
            }

            Active = 0;
            ChoiceChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Puts the operator on his mark with the weapon he was given and hands over to the mission.
    ///     Still paused: the player plans first, then presses Space.
    /// </summary>
    public void Deploy()
    {
        var weapon = Weapon;

        for (var i = 0; i < roster.Count; i++)
        {
            var op = roster[i];

            if (op == null)
            {
                continue;
            }

            var point = assigned[i];

            // Anyone without an entry is not on this job. Off the map rather than standing about on
            // it: an operator you did not send should not be shootable, visible or countable.
            if (point < 0)
            {
                op.gameObject.SetActive(false);
                continue;
            }

            op.gameObject.SetActive(true);

            if (weapon != null)
            {
                op.SetWeapon(weapon);

                if (op.TryGetComponent(out OperatorCombat combat))
                {
                    combat.Rearm();
                }
            }

            Stand(op, deployPoints.Position(point));
        }

        Rescan();

        Elapsed = 0f;
        Outcome = MissionOutcome.None;
        Undetected = true;
        Screen = GameScreen.Mission;
        SimClock.SetPaused(true);
    }

    /// <summary>Runs the mission again from the deployment screen, on a clean world.</summary>
    public void Retry()
    {
        Restart(GameScreen.Deployment);
    }

    public void ToMenu()
    {
        Restart(GameScreen.MainMenu);
    }

    public void Quit()
    {
        Application.Quit();
    }

    private static void Restart(GameScreen at)
    {
        resumeAt = at;
        SimClock.SetPaused(true);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void Update()
    {
        if (Screen != GameScreen.Mission)
        {
            return;
        }

        Elapsed += SimClock.DeltaTime;

        Rescan();
        Tally();

        if (Outcome != MissionOutcome.None)
        {
            return;
        }

        // Order matters only in the impossible case of both being true at once, and the prototype
        // calls that a win. Losing on the shot that clears the last room would be a poor joke.
        if (EnemiesDown >= enemies.Count && enemies.Count > 0)
        {
            Finish(MissionOutcome.Win);
            return;
        }

        if (OperatorsLost >= squad.Count && squad.Count > 0)
        {
            Finish(MissionOutcome.Loss);
        }
    }

    private void Finish(MissionOutcome outcome)
    {
        Outcome = outcome;
        Screen = GameScreen.Results;
        SimClock.SetPaused(true);
    }

    private void Tally()
    {
        var down = 0;
        var seen = false;

        foreach (var enemy in enemies)
        {
            if (enemy == null)
            {
                continue;
            }

            if (!enemy.IsAlive)
            {
                down++;
            }

            if (enemy.Alert >= AlertLevel.Alerted)
            {
                seen = true;
            }
        }

        EnemiesDown = down;

        if (seen)
        {
            Undetected = false;
        }

        var lost = 0;
        var fired = 0;
        var hits = 0;

        foreach (var op in squad)
        {
            if (op == null)
            {
                continue;
            }

            if (!op.IsAlive)
            {
                lost++;
            }

            if (op.TryGetComponent(out OperatorCombat combat))
            {
                fired += combat.ShotsFired;
                hits += combat.Hits;
            }
        }

        OperatorsLost = lost;
        ShotsFired = fired;
        Hits = hits;
    }

    private void Rescan()
    {
        if (Time.unscaledTime < nextScan && squad.Count > 0)
        {
            return;
        }

        nextScan = Time.unscaledTime + 0.5f;

        squad.Clear();
        squad.AddRange(FindObjectsByType<Operator>(FindObjectsSortMode.InstanceID));

        enemies.Clear();
        enemies.AddRange(FindObjectsByType<Enemy>(FindObjectsSortMode.InstanceID));
    }
}
