using UnityEngine;

/// <summary>
///     What makes one operator class different from another: how fast he moves, what he carries,
///     what he can see, and the colour he draws in. Mirrors the CLASSES table in the JS prototype.
/// </summary>
/// <remarks>
///     Everything shared by the whole squad lives behind the four references below rather than being
///     copied into each class asset — the loadout, the eyes, the feel of drawing an order, and the
///     marker sizes. Retuning the carbine or the waypoint rings is then one asset edit instead of
///     one per class, and a class asset only ever states what actually distinguishes it.
///     <para>
///         Each reference may be left empty, in which case a default built from the C# initialisers
///         stands in. So a bare <see cref="OperatorSpec" /> still runs, which keeps the game
///         playable while a class is being roughed out.
///     </para>
/// </remarks>
[CreateAssetMenu(fileName = "OperatorSpec", menuName = "Tacticore/Operator Spec")]
public class OperatorSpec : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "ASSAULT";

    [Header("Movement")]
    [Tooltip("World units per second at walking pace.")]
    public float walkSpeed = 2.6f;

    [Tooltip("World units per second when running.")]
    public float runSpeed = 4.4f;

    [Tooltip("Degrees per second the facing can swing.")]
    public float turnRate = 360f;

    [Header("Openings")]
    [Tooltip("How close he must get to a door or window to work on it, in cells. Short of the "
             + "doorstep, so he is clear of the arc the leaf swings through.")]
    public float openingReach = 0.9f;

    [Header("Class colours")]
    [Tooltip("The class colour. #7fd4ff is the prototype's assaulter.")]
    public Color pathColor = new(0.498f, 0.831f, 1f, 1f);

    [Tooltip("Cone light, added not overlaid. #ffdca6 is the prototype's warm tint.")]
    public Color coneColor = new(1f, 0.863f, 0.651f, 0.30f);

    [Header("Shared data")]
    [SerializeField] private WeaponSpec weapon;
    [SerializeField] private VisionSpec vision;
    [SerializeField] private PlanningRules planning;
    [SerializeField] private OperatorStyle style;

    private WeaponSpec weaponFallback;
    private VisionSpec visionFallback;
    private PlanningRules planningFallback;
    private OperatorStyle styleFallback;

    public WeaponSpec Weapon => SpecFallback.Or(weapon, ref weaponFallback);

    public VisionSpec Vision => SpecFallback.Or(vision, ref visionFallback);

    public PlanningRules Planning => SpecFallback.Or(planning, ref planningFallback);

    public OperatorStyle Style => SpecFallback.Or(style, ref styleFallback);

}
