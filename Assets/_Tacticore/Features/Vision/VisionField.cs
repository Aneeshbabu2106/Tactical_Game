using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
///     Who can see what. Casts each operator's sight once a frame — a short circle of awareness all
///     round him, and a longer cone in the direction he faces — and decides from it which enemies
///     are currently visible.
/// </summary>
/// <remarks>
///     Visibility is a plain boolean per target, which is what the prototype does: its renderer asks
///     <c>playerSees(w, u)</c> and simply skips drawing anyone who fails. Masking the screen to the
///     cone shape would hide the same pixels, but it produces no state — and "is this enemy spotted"
///     is exactly what the AI, the callouts and the shooting all need to ask later.
///     <para>
///         The environment is not hidden at all. Only what is alive in it is.
///     </para>
/// </remarks>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
public class VisionField : MonoBehaviour
{
    [SerializeField] private Tilemap navigation;

    [Tooltip("How far the drawn cone reaches into a blocking cell. The wall's visible face is 0.30 "
             + "in from the boundary, so stopping at the boundary leaves a gap in front of it.")]
    [SerializeField] private float coneCover = 0.32f;

    [Tooltip("Seconds between rescans of the scene, rather than searching it every frame.")]
    [SerializeField] private float rescanInterval = 0.5f;

    [Header("Debug")]
    [Tooltip("Shows every enemy regardless of who can see them. Safe to tick and untick while "
             + "playing — it is read every frame, so it is a live switch rather than a setup step.")]
    [SerializeField] private bool revealAll;

    /// <summary>One operator's sight this frame: the near circle and the directional cone.</summary>
    public sealed class Fan
    {
        public readonly List<Vector3> Cone = new();
        public readonly List<Vector3> Circle = new();
    }

    private readonly List<Operator> operators = new();
    private readonly List<Spottable> targets = new();
    private readonly Dictionary<Operator, Fan> fans = new();

    private float nextScan;

    /// <summary>
    ///     The selected operator, taken from the list this already caches so nothing else has to
    ///     sweep the scene for him every frame.
    /// </summary>
    public Operator Selected { get; private set; }

    /// <summary>
    ///     Debug reveal. Backed by the serialized field so the inspector tick and anything that sets
    ///     this agree — the field is pushed to <see cref="Spottable" /> every frame, so setting the
    ///     flag directly on Spottable would be overwritten within the frame.
    /// </summary>
    public bool RevealAll
    {
        get => revealAll;
        set => revealAll = value;
    }

    public bool TryGetFan(Operator op, out Fan fan)
    {
        return fans.TryGetValue(op, out fan);
    }

    private void Awake()
    {
        if (navigation == null)
        {
            navigation = FindFirstObjectByType<Tilemap>();
        }

        if (navigation == null)
        {
            Debug.LogError($"{name}: no Tilemap assigned. Disabling.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        // Time, not SimClock: the look-target drag works while paused, so his facing — and what he
        // can see — changes while the simulation is held.
        Rescan();

        // Driven from the field rather than set once, so the inspector tick is live during play.
        Spottable.RevealAll = revealAll;

        Selected = null;

        foreach (var op in operators)
        {
            if (op == null)
            {
                continue;
            }

            if (op.IsSelected)
            {
                Selected = op;
            }

            if (!fans.TryGetValue(op, out var fan))
            {
                fan = new Fan();
                fans[op] = fan;
            }

            var origin = op.transform.position;

            VisionFan.Cast(
                navigation, origin, op.FacingDegrees, op.VisionFov, op.VisionRange,
                op.VisionStepDegrees, coneCover, coneCover, fan.Cone, null);

            VisionFan.CastCircle(
                navigation, origin, op.VisionNearRadius, op.VisionStepDegrees * 2f, coneCover,
                fan.Circle);
        }

        foreach (var target in targets)
        {
            if (target != null)
            {
                target.SetVisible(AnyoneSees(target.transform.position));
            }
        }
    }

    /// <summary>True if any operator can see this point right now.</summary>
    public bool AnyoneSees(Vector3 point)
    {
        foreach (var op in operators)
        {
            if (op != null && CanSee(op, point))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     In range, then inside either the cone or the close circle, then not behind anything. The
    ///     circle is what stops a man being blind to someone standing at his shoulder.
    /// </summary>
    public bool CanSee(Operator op, Vector3 point)
    {
        return CanSee(
            op.transform.position, op.FacingDegrees, op.VisionFov, op.VisionRange,
            op.VisionNearRadius, point);
    }

    /// <summary>
    ///     The same question asked of a bare pair of eyes rather than an operator, so an enemy can
    ///     use it too. Everything the operator overload knew was these five numbers.
    /// </summary>
    public bool CanSee(
        Vector3 origin, float facingDegrees, float fovDegrees, float range, float nearRadius,
        Vector3 point)
    {
        var to = point - origin;
        var distance = ((Vector2)to).magnitude;

        if (distance > range)
        {
            return false;
        }

        var bearing = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
        var off = Mathf.Abs(Mathf.DeltaAngle(facingDegrees, bearing));

        // Inside the near radius he is aware in every direction, so the cone does not apply.
        if (off > fovDegrees * 0.5f && distance > nearRadius)
        {
            return false;
        }

        var hit = VisionFan.CastRay(navigation, origin, bearing, range);
        return hit.Distance >= distance - 0.01f;
    }

    private void Rescan()
    {
        if (Time.unscaledTime < nextScan && operators.Count > 0)
        {
            return;
        }

        nextScan = Time.unscaledTime + rescanInterval;

        operators.Clear();
        operators.AddRange(FindObjectsByType<Operator>(FindObjectsSortMode.None));

        targets.Clear();
        targets.AddRange(FindObjectsByType<Spottable>(FindObjectsSortMode.None));
    }
}
