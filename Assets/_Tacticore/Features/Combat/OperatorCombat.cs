using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Makes one operator shoot at what he can see. He engages on his own — the player aims him and
///     decides where he stands; the trigger is not a thing you press.
/// </summary>
/// <remarks>
///     Targets come from <see cref="VisionField" />, asked per operator, so he only ever shoots what
///     he personally can see: in range, inside his cone or the circle at his shoulder, and not
///     behind anything. It is the same test the drawn cone is built from, so he can never fire at
///     something you were not shown.
///     <para>
///         Deliberately no auto-turn. The prototype rotates a unit onto its target through a
///         reaction pipeline, but here the facing is the player's instrument — pointing a man at a
///         door is the order. Turning him for him would take that away.
///     </para>
/// </remarks>
[RequireComponent(typeof(Operator))]
[DisallowMultipleComponent]
public class OperatorCombat : MonoBehaviour
{
    [SerializeField] private VisionField vision;

    [Tooltip("Seconds between a target coming into view and the first round leaving. The prototype "
             + "spends this in a decide/turn/settle/aim pipeline.")]
    [SerializeField] private float reactionTime = 0.35f;

    [SerializeField] private Color tracerColor = new(1f, 0.92f, 0.62f, 0.9f);
    [SerializeField] private float tracerSeconds = 0.05f;

    [Header("Reload bar")]
    [Tooltip("Width of the bar shown under him while he changes a magazine.")]
    [SerializeField] private float reloadBarWidth = 0.7f;

    [Tooltip("Offset from the operator, clear of the waypoint-action bar above it.")]
    [SerializeField] private Vector2 reloadBarOffset = new(0f, -0.62f);

    [SerializeField] private Color reloadBarColor = new(1f, 0.62f, 0.3f, 1f);
    [SerializeField] private Color reloadBarTrackColor = new(0.1f, 0.11f, 0.13f, 0.75f);

    [Tooltip("Seconds between rescans for targets, rather than sweeping the scene every frame.")]
    [SerializeField] private float rescanInterval = 0.5f;

    private readonly List<Spottable> candidates = new();

    private Operator self;
    private LineRenderer tracer;
    private Health target;
    private float tracerLeft;
    private float nextScan;
    private float reaction;
    private LineRenderer reloadBar;
    private LineRenderer reloadBarTrack;

    public Weapon Weapon { get; private set; }

    /// <summary>Rounds sent this mission. The end-of-mission stats will want it.</summary>
    public int ShotsFired { get; private set; }

    /// <summary>What he is shooting at, or null. For the UI and, later, for callouts.</summary>
    public Health Target => target;

    private void Awake()
    {
        self = GetComponent<Operator>();

        if (vision == null)
        {
            vision = FindFirstObjectByType<VisionField>();
        }

        Weapon = new Weapon(self.MagazineSize, self.RoundsPerMinute, self.ReloadSeconds);

        BuildTracer();
        BuildReloadBar();
    }

    private void Update()
    {
        // SimClock, not Time: nobody fires while the player is planning.
        var dt = SimClock.DeltaTime;

        FadeTracer();
        Weapon.Tick(dt);
        RedrawReloadBar();

        if (dt <= 0f)
        {
            return;
        }

        // Hold fire means exactly that: he does not even look for a shot, and never stops for one.
        if (self.Engagement == EngagementMode.HoldFire)
        {
            target = null;
            reaction = 0f;
            self.Hold(false);
            return;
        }

        var found = PickTarget();

        // The delay is paid on acquiring, not on every change of target: a man already engaging
        // does not start from scratch because a nearer one stepped out.
        if (target == null && found != null)
        {
            reaction = reactionTime;
        }

        target = found;

        // Wait to clear stops him where he stands until nothing is left in view. Keep moving never
        // breaks stride, so it shoots on the walk.
        self.Hold(self.Engagement == EngagementMode.WaitToClear && target != null);

        if (target == null)
        {
            return;
        }

        if (reaction > 0f)
        {
            reaction -= dt;
            return;
        }

        if (Weapon.TryFire())
        {
            ShotsFired++;
            Fire(target);
        }
    }

    /// <summary>Nearest target he can see, within the weapon's reach.</summary>
    private Health PickTarget()
    {
        Rescan();

        Health best = null;
        var bestDistance = float.MaxValue;

        foreach (var candidate in candidates)
        {
            if (candidate == null || vision == null)
            {
                continue;
            }

            if (!candidate.TryGetComponent(out Health health) || !health.IsAlive)
            {
                continue;
            }

            var at = candidate.transform.position;
            var distance = Vector2.Distance(at, transform.position);

            if (distance > self.WeaponRange || distance >= bestDistance)
            {
                continue;
            }

            // Asked of the vision field rather than read off Spottable.IsVisible: that flag means
            // "somebody can see it" and is set for rendering, whereas a man may only shoot what is
            // in his own cone. Checked last because it is the most expensive of the three.
            if (!vision.CanSee(self, at))
            {
                continue;
            }

            bestDistance = distance;
            best = health;
        }

        return best;
    }

    private void Fire(Health at)
    {
        // Accuracy falls off past most of the weapon's reach, as the prototype's hitChance does.
        var distance = Vector2.Distance(at.transform.position, transform.position);
        var chance = self.WeaponAccuracy * (distance > self.WeaponRange * 0.7f ? 0.55f : 1f);

        ShowTracer(at.transform.position);

        if (Random.value <= chance)
        {
            at.TakeDamage(self.WeaponDamage);
        }
    }

    /// <summary>
    ///     Shows how far through a magazine change he is. Drawn whenever a reload is outstanding —
    ///     including while the game is paused, where it sits still at whatever it had reached. A
    ///     reload is simulated time like everything else, so ordering one while planning queues it
    ///     rather than performing it, and this is what says so.
    /// </summary>
    private void RedrawReloadBar()
    {
        var reloading = Weapon.IsReloading;

        reloadBar.enabled = reloading;
        reloadBarTrack.enabled = reloading;

        if (!reloading)
        {
            return;
        }

        var half = reloadBarWidth * 0.5f;
        var origin = transform.position + new Vector3(reloadBarOffset.x, reloadBarOffset.y, 0f);
        var left = origin + Vector3.left * half;

        reloadBarTrack.SetPosition(0, left);
        reloadBarTrack.SetPosition(1, origin + Vector3.right * half);

        reloadBar.SetPosition(0, left);
        reloadBar.SetPosition(1, left + Vector3.right * reloadBarWidth * Weapon.ReloadProgress);
    }

    private void BuildReloadBar()
    {
        reloadBarTrack = BuildBar("ReloadBarTrack", reloadBarTrackColor, 0.09f, 111);
        reloadBar = BuildBar("ReloadBar", reloadBarColor, 0.07f, 112);
    }

    private LineRenderer BuildBar(string barName, Color color, float width, int sortingOrder)
    {
        var host = new GameObject(barName);
        host.transform.SetParent(transform, false);

        var renderer = host.AddComponent<LineRenderer>();
        renderer.useWorldSpace = true;
        renderer.widthMultiplier = width;
        renderer.positionCount = 2;
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.startColor = color;
        renderer.endColor = color;
        renderer.sortingOrder = sortingOrder;
        renderer.enabled = false;
        return renderer;
    }

    private void ShowTracer(Vector3 to)
    {
        tracer.SetPosition(0, transform.position);
        tracer.SetPosition(1, to);
        tracer.enabled = true;
        tracerLeft = tracerSeconds;
    }

    /// <summary>
    ///     On real time, not the sim clock: a tracer frozen mid-air on pause would look broken, and
    ///     it is decoration rather than state.
    /// </summary>
    private void FadeTracer()
    {
        if (tracerLeft <= 0f)
        {
            return;
        }

        tracerLeft -= Time.deltaTime;

        if (tracerLeft <= 0f)
        {
            tracer.enabled = false;
        }
    }

    private void BuildTracer()
    {
        var host = new GameObject("Tracer");
        host.transform.SetParent(transform, false);

        tracer = host.AddComponent<LineRenderer>();
        tracer.useWorldSpace = true;
        tracer.widthMultiplier = 0.045f;
        tracer.positionCount = 2;
        tracer.material = new Material(Shader.Find("Sprites/Default"));
        tracer.startColor = tracerColor;
        tracer.endColor = tracerColor;
        tracer.sortingOrder = 110;
        tracer.enabled = false;
    }

    private void Rescan()
    {
        if (Time.unscaledTime < nextScan && candidates.Count > 0)
        {
            return;
        }

        nextScan = Time.unscaledTime + rescanInterval;

        candidates.Clear();
        candidates.AddRange(FindObjectsByType<Spottable>(FindObjectsSortMode.None));
    }
}
