using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     What one enemy shoots at, and how long he takes about it.
/// </summary>
/// <remarks>
///     The prototype's firing pipeline, ported: seeing a man is not the same as shooting him.
///     Decide, then turn, then aim, then fire. The decide stage is read off his alert level, so an
///     unaware man takes 1.3 seconds to get started and an alerted one a quarter of that. That gap
///     is the whole value of coming through a door quietly.
///     <para>
///         Losing sight of a target does not reset the pipeline — it is held for a couple of
///         seconds first. The prototype calls this "no peek-a-boo reset", and without it an operator
///         could bob in and out of a doorway forever without ever being shot at.
///     </para>
/// </remarks>
[RequireComponent(typeof(Enemy))]
[DisallowMultipleComponent]
public class EnemyCombat : MonoBehaviour
{
    private enum Stage
    {
        Idle,
        Decide,
        Turn,
        Aim,
        Firing
    }

    [SerializeField] private VisionField vision;

    [SerializeField] private Color tracerColor = new(1f, 0.45f, 0.35f, 0.9f);
    [SerializeField] private float tracerSeconds = 0.05f;

    [Tooltip("Seconds between sweeps for someone to shoot at.")]
    [SerializeField] private float rescanInterval = 0.5f;

    [Tooltip("Degrees off target that sends him back to turning mid-burst.")]
    [SerializeField] private float breakOffDegrees = 25f;

    private readonly List<Operator> candidates = new();

    private Enemy self;
    private LineRenderer tracer;
    private Stage stage;
    private Health target;
    private float stageTimer;
    private float reacquire;
    private float tracerLeft;
    private float nextScan;

    public Weapon Weapon { get; private set; }

    /// <summary>Rounds sent. The end-of-mission stats will want it, as the operator's does.</summary>
    public int ShotsFired { get; private set; }

    /// <summary>Who he is working on, or null. The brain reads this to know he is busy.</summary>
    public Health Target => target;

    public bool IsEngaging => target != null;

    /// <summary>True only once he is actually shooting, for the probe and later for callouts.</summary>
    public bool IsFiring => stage == Stage.Firing;

    private void Awake()
    {
        self = GetComponent<Enemy>();

        if (vision == null)
        {
            vision = FindFirstObjectByType<VisionField>();
        }

        var weapon = self.Spec.Weapon;
        Weapon = new Weapon(weapon.magazineSize, weapon.roundsPerMinute, weapon.reloadSeconds);

        BuildTracer();
    }

    private void Update()
    {
        var dt = SimClock.DeltaTime;

        FadeTracer();
        Weapon.Tick(dt);

        if (dt <= 0f)
        {
            return;
        }

        if (!self.IsAlive)
        {
            target = null;
            stage = Stage.Idle;
            return;
        }

        var found = PickTarget();

        if (found != null)
        {
            if (target == null)
            {
                target = found;
                Enter(Stage.Decide);
            }
            else if (found != target)
            {
                // A nearer man stepping out does not buy him a fresh reaction time; only the turn
                // is redone, since he is now pointing at the wrong person.
                target = found;
                Enter(Stage.Turn);
            }

            reacquire = self.Spec.Alert.reacquireSeconds;
        }
        else if (target != null)
        {
            reacquire -= dt;

            if (reacquire <= 0f)
            {
                target = null;
                stage = Stage.Idle;
                return;
            }
        }

        if (target == null)
        {
            return;
        }

        Advance(dt);
    }

    private void Enter(Stage next)
    {
        stage = next;
        var profile = self.Spec.Alert;

        stageTimer = next switch
        {
            Stage.Decide => profile.DecideSeconds(self.Alert),
            Stage.Aim => AimSeconds(),
            _ => 0f
        };
    }

    private void Advance(float dt)
    {
        var at = target.transform.position;

        // He looks at whoever he is working on through every stage, so the turn is the motor's
        // doing at his alert level's turn rate rather than a number invented here.
        self.Look(at);

        var off = Mathf.Abs(Mathf.DeltaAngle(self.FacingDegrees, Bearing(at)));

        switch (stage)
        {
            case Stage.Decide:
                // Re-read the decide time every frame rather than trusting the one loaded when the
                // stage began. Being shot at, or a shout, raises his alert level mid-decide, and a
                // man who has just been hit does not go on thinking at the pace of a man who has
                // not. Without this a burst that kills him before his unaware 1.3s elapses means he
                // never fires at all — which is exactly how the first pass played.
                stageTimer = Mathf.Min(stageTimer, self.Spec.Alert.DecideSeconds(self.Alert));
                stageTimer -= dt;

                if (stageTimer <= 0f)
                {
                    Enter(Stage.Turn);
                }

                break;

            case Stage.Turn:
                if (off <= self.Spec.Alert.onTargetDegrees)
                {
                    Enter(Stage.Aim);
                }

                break;

            case Stage.Aim:
                stageTimer -= dt;

                if (stageTimer <= 0f)
                {
                    stage = Stage.Firing;
                }

                break;

            case Stage.Firing:
                if (off > breakOffDegrees)
                {
                    Enter(Stage.Turn);
                    break;
                }

                if (Weapon.TryFire())
                {
                    ShotsFired++;
                    Fire(at);
                }

                break;
        }
    }

    private void Fire(Vector3 at)
    {
        var spec = self.Spec;
        var range = spec.Weapon.range;
        var distance = Vector2.Distance(at, transform.position);

        // Same falloff the operator gets: past most of the weapon's reach he is guessing.
        var chance = spec.accuracy * (distance > range * 0.7f ? 0.55f : 1f);

        ShowTracer(at);
        Noise.Emit(transform.position, Noise.Gunshot, this);

        if (Random.value <= chance)
        {
            target.TakeDamage(spec.DamagePerRound, gameObject);
        }
    }

    /// <summary>Nearest operator he can actually see, within the weapon's reach.</summary>
    private Health PickTarget()
    {
        Rescan();

        Health best = null;
        var bestDistance = float.MaxValue;
        var range = self.Spec.Weapon.range;

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

            if (distance > range || distance >= bestDistance)
            {
                continue;
            }

            // His own eyes, not the operator's: the vision field answers for any pair of them.
            if (!vision.CanSee(
                    transform.position, self.FacingDegrees, self.VisionFov, self.VisionRange,
                    self.VisionNearRadius, at))
            {
                continue;
            }

            bestDistance = distance;
            best = health;
        }

        return best;
    }

    private float AimSeconds()
    {
        var profile = self.Spec.Alert;
        var distance = target != null
            ? Vector2.Distance(target.transform.position, transform.position)
            : 0f;

        return profile.aimSeconds + Mathf.Max(0f, distance - 5f) * profile.aimSecondsPerCell;
    }

    private float Bearing(Vector3 at)
    {
        var to = at - transform.position;
        return Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
    }

    private void Rescan()
    {
        if (Time.unscaledTime < nextScan && candidates.Count > 0)
        {
            return;
        }

        nextScan = Time.unscaledTime + rescanInterval;

        candidates.Clear();
        candidates.AddRange(FindObjectsByType<Operator>(FindObjectsSortMode.None));
    }

    private void ShowTracer(Vector3 to)
    {
        tracer.SetPosition(0, transform.position);
        tracer.SetPosition(1, to);
        tracer.enabled = true;
        tracerLeft = tracerSeconds;
    }

    /// <summary>Real time, not the sim clock: a tracer frozen mid-air on pause looks broken.</summary>
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
}
