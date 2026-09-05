using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
///     What one enemy does when nobody is in his sights: walks his route, turns toward a noise, goes
///     and looks at a loud one, and comes back still jumpy.
/// </summary>
/// <remarks>
///     Ported from the prototype's updateAI, which is a priority-ordered list rather than a switch —
///     the order is the design, so it is kept. Engaging beats holding an angle, which beats
///     searching, which beats standing and listening, which beats patrolling.
///     <para>
///         Two deliberate departures. The prototype never comes down from alerted and never goes
///         home after a search, so one mistake ends the level; here both decay, and a man who
///         searched and found nothing rejoins his route still at a raised alert level — quicker on
///         the turn and quicker to shoot for a while afterwards.
///     </para>
/// </remarks>
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(EnemyCombat))]
[DisallowMultipleComponent]
public class EnemyBrain : MonoBehaviour
{
    [SerializeField] private Tilemap navigation;

    [Tooltip("The route he walks when nothing is wrong. Left empty, he holds his post facing.")]
    [SerializeField] private PatrolRoute route;

    private readonly List<NoiseEvent> heard = new();

    private Enemy self;
    private EnemyCombat combat;
    private Health health;

    private AlertLevel alert;
    private float alertTimer;
    private float searchTimer;
    private int noiseCursor;

    private Vector3? interest;
    private bool routed;

    /// <summary>Seconds left standing at a patrol point, and how long that stand was to last.</summary>
    private float dwell;
    private float dwellTotal;
    private float scanCentre;

    /// <summary>Set once a search ends, so he patrols while still alert rather than standing.</summary>
    private bool searched;

    public AlertLevel Alert => alert;

    /// <summary>Where he last heard or saw something. Null when nothing is on his mind.</summary>
    public Vector3? Interest => interest;

    /// <summary>For the probe and, later, for an on-screen readout.</summary>
    public string State =>
        !self.IsAlive ? "DOWN"
        : combat.IsEngaging ? "ENGAGING"
        : alert.ToString().ToUpperInvariant();

    private void Awake()
    {
        self = GetComponent<Enemy>();
        combat = GetComponent<EnemyCombat>();

        if (navigation == null)
        {
            navigation = FindFirstObjectByType<Tilemap>();
        }

        if (navigation == null)
        {
            Debug.LogError($"{name}: no Tilemap assigned. Disabling.", this);
            enabled = false;
        }

        if (TryGetComponent(out health))
        {
            health.Damaged += OnDamaged;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
        }
    }

    /// <summary>
    ///     Being shot is not a noise to be weighed against a threshold — it is the fact of a hostile,
    ///     and it puts him straight to the top of the ladder facing wherever it came from. Without
    ///     this a man could be hit repeatedly and still be deciding at his unaware pace.
    /// </summary>
    private void OnDamaged(Health _, GameObject from)
    {
        alert = AlertLevel.Alerted;
        alertTimer = 0f;
        searched = false;
        routed = false;
        dwell = 0f;

        if (from != null)
        {
            interest = from.transform.position;
        }
    }

    private void Update()
    {
        var dt = SimClock.DeltaTime;

        if (dt <= 0f)
        {
            return;
        }

        if (!self.IsAlive)
        {
            return;
        }

        Listen();

        self.Alert = alert;
        alertTimer += dt;

        // Seeing someone beats everything, and beats it without a timer: combat owns his facing
        // and his feet for as long as it has a target.
        if (combat.IsEngaging)
        {
            alert = AlertLevel.Alerted;
            alertTimer = 0f;
            searched = false;
            interest = combat.Target != null ? combat.Target.transform.position : interest;
            self.Stop();
            return;
        }

        switch (alert)
        {
            case AlertLevel.Alerted:
                HoldTheAngle();
                break;

            case AlertLevel.Searching:
                Search(dt);
                break;

            case AlertLevel.Suspicious:
                Listening(dt);
                break;

            default:
                Patrol(dt);
                break;
        }
    }

    /// <summary>Reads the noise log and raises his alert to match the loudest thing he made out.</summary>
    private void Listen()
    {
        heard.Clear();
        Noise.Collect(ref noiseCursor, heard);

        foreach (var noise in heard)
        {
            // His own shot is not news to him.
            if (noise.Source == combat || noise.Source == self)
            {
                continue;
            }

            var intensity = SoundQuery.HeardIntensity(
                navigation, noise.At, transform.position, noise.Loudness);

            if (SoundQuery.CanLocate(intensity))
            {
                Raise(AlertLevel.Searching, noise.At);
            }
            else if (SoundQuery.CanNotice(intensity))
            {
                Raise(AlertLevel.Suspicious, noise.At);
            }
        }
    }

    /// <summary>
    ///     Only ever raises. A faint noise while he is already searching must not talk him down to
    ///     merely suspicious — but it does move where he is headed, because it is newer.
    /// </summary>
    private void Raise(AlertLevel level, Vector3 at)
    {
        interest = at;

        if (level < alert)
        {
            return;
        }

        if (level > alert || level == AlertLevel.Searching)
        {
            routed = false;
        }

        if (level >= AlertLevel.Searching)
        {
            searchTimer = self.Spec.Alert.searchSeconds;
        }

        alert = level;
        alertTimer = 0f;
        searched = false;
        dwell = 0f;
    }

    /// <summary>Alerted with nobody in sight: stand still, watch the bearing he last saw one on.</summary>
    private void HoldTheAngle()
    {
        self.Stop();
        self.Look(interest);

        if (alertTimer < self.Spec.Alert.alertedSeconds)
        {
            return;
        }

        alert = AlertLevel.Suspicious;
        alertTimer = 0f;
        searched = true;
    }

    /// <summary>Walks to where the noise came from and looks around.</summary>
    private void Search(float dt)
    {
        searchTimer -= dt;

        if (!interest.HasValue)
        {
            EndSearch();
            return;
        }

        if (!routed)
        {
            routed = true;

            var course = CellPathfinder.Find(navigation, transform.position, Standable(interest.Value));

            // Nothing walkable leads there — he heard it through a wall he cannot get around. He
            // still knows roughly where it came from, so he stands and watches that way instead.
            if (course == null)
            {
                EndSearch();
                return;
            }

            self.Walk(course);
        }

        // Facing follows his feet on the way there, so he scans as he walks.
        self.Look(null);

        var arrived = Vector2.Distance(transform.position, Standable(interest.Value))
                      <= self.Spec.Alert.searchArriveRadius;

        if (arrived || searchTimer <= 0f || !self.IsWalking)
        {
            EndSearch();
        }
    }

    /// <summary>
    ///     The nearest spot he could actually stand to look at something. A noise is usually made
    ///     <em>at</em> the thing that made it — a door being kicked, a window breaking — and those
    ///     cells are not walkable, so routing straight to one is refused and the search is abandoned
    ///     before he takes a step. He wants to get next to it, not into it.
    /// </summary>
    private Vector3 Standable(Vector3 at)
    {
        var cell = navigation.WorldToCell(at);

        if (NavigationQuery.IsWalkable(navigation, cell))
        {
            return at;
        }

        // Outward in rings, so he ends up on the side of the door he was already on where possible.
        for (var radius = 1; radius <= 3; radius++)
        {
            var best = Vector3.zero;
            var bestDistance = float.MaxValue;

            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius)
                    {
                        continue;
                    }

                    var near = new Vector3Int(cell.x + dx, cell.y + dy, cell.z);

                    if (!NavigationQuery.IsWalkable(navigation, near))
                    {
                        continue;
                    }

                    var centre = navigation.GetCellCenterWorld(near);
                    centre.z = at.z;

                    var distance = Vector2.Distance(centre, transform.position);

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = centre;
                    }
                }
            }

            if (bestDistance < float.MaxValue)
            {
                return best;
            }
        }

        return at;
    }

    private void EndSearch()
    {
        self.Stop();
        alert = AlertLevel.Suspicious;
        alertTimer = 0f;
        searched = true;
        routed = false;
    }

    /// <summary>
    ///     Suspicious. Before a search that means standing and turning toward what he heard, which
    ///     is the prototype's behaviour. After one it means going back to work still on edge.
    /// </summary>
    private void Listening(float dt)
    {
        if (searched)
        {
            Patrol(dt);
        }
        else
        {
            self.Stop();
            self.Look(interest);
        }

        if (alertTimer < self.Spec.Alert.suspiciousSeconds)
        {
            return;
        }

        alert = AlertLevel.Unaware;
        alertTimer = 0f;
        searched = false;
        interest = null;
    }

    private void Patrol(float dt)
    {
        if (route == null || !route.HasRoute)
        {
            // No route: drift back to the way he was posted and stand there.
            self.Stop();
            self.Look(HomeLook());
            return;
        }

        if (dwell > 0f)
        {
            Stand(dt);
            return;
        }

        if (!routed)
        {
            route.RejoinNearest(transform.position);
            routed = true;
            Head(route.Current);
            return;
        }

        // Arrived: stand a while and look around before taking the next leg. Advancing the moment
        // he touches the point is what made the patrol read as a trolley on rails.
        if (route.IsAt(transform.position))
        {
            dwellTotal = route.Dwell;
            dwell = dwellTotal;
            scanCentre = route.FacingAt(transform.position);
            self.Stop();
            return;
        }

        // Facing follows his feet while he walks.
        self.Look(null);

        // Blocked, or the leg finished short: take the next one rather than standing forever.
        if (!self.IsWalking)
        {
            Head(route.Current);
        }
    }

    /// <summary>
    ///     Standing at a point, sweeping either side of centre and back. One smooth pass rather than
    ///     a snap to each extreme — the motor turns him at his alert level's rate, so the sweep is
    ///     visibly lazier when he has nothing on his mind than when he has.
    /// </summary>
    private void Stand(float dt)
    {
        dwell -= dt;

        var swept = dwellTotal > 0f ? 1f - dwell / dwellTotal : 1f;
        var angle = scanCentre + Mathf.Sin(swept * Mathf.PI * 2f) * route.ScanDegrees;
        var radians = angle * Mathf.Deg2Rad;

        self.Look(transform.position + new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * 2f);

        if (dwell > 0f)
        {
            return;
        }

        route.Advance();
        self.Look(null);
        Head(route.Current);
    }

    private void Head(Vector3 to)
    {
        var course = CellPathfinder.Find(navigation, transform.position, to);

        if (course != null)
        {
            self.Walk(course);
        }
    }

    /// <summary>A point out in front of his post, since the motor turns toward places not angles.</summary>
    private Vector3 HomeLook()
    {
        var radians = self.HomeFacing * Mathf.Deg2Rad;
        return transform.position + new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * 2f;
    }
}
