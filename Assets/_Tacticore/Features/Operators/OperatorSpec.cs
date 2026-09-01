using UnityEngine;

/// <summary>
///     Everything that defines an operator type, kept out of the scene so tuning is one asset edit
///     rather than a hunt through prefabs. Mirrors the CLASSES table in the JS prototype.
/// </summary>
[CreateAssetMenu(
    fileName = "OperatorSpec",
    menuName = "Tacticore/Operator Spec"
)]
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

    [Header("Selection")]
    [Tooltip("How close the cursor must be, in cells, to start drawing from this operator.")]
    public float pickRadius = 0.45f;

    [Header("Waypoints")]
    [Tooltip("Degrees of accumulated turn along the stroke that earns a waypoint.")]
    public float waypointTurnThreshold = 40f;

    [Tooltip("Closest two auto-placed waypoints may sit, so a shaky hand cannot cluster them.")]
    public float waypointMinSpacing = 1.2f;

    [Tooltip("A straight run longer than this gets a waypoint anyway. Zero disables.")]
    public float waypointMaxSpacing = 8f;

    public float waypointPickRadius = 0.32f;

    [Tooltip("A drawn path starts this far out from the operator, clear of its sprite and pick radius.")]
    public float pathStartClearance = 0.55f;

    [Tooltip("Cursor distance from the path that shows the add-waypoint marker.")]
    public float pathHoverThreshold = 0.35f;

    [Header("Openings")]
    [Tooltip("How close the operator must get to a door or window to work on it, in cells. Short of "
             + "the doorstep, so he is clear of the arc the leaf swings through.")]
    public float openingReach = 0.9f;

    public float waypointMarkerSize = 0.3f;

    [Tooltip("Ring colour for a waypoint set to run.")]
    public Color waypointRunColor = new(1f, 0.55f, 0.27f, 1f);

    [Tooltip("Ring colour for a waypoint carrying an unfinished action, such as opening a door.")]
    public Color waypointActionColor = new(1f, 0.85f, 0.35f, 1f);

    [Header("Action progress")]
    [Tooltip("Width of the bar shown under an operator while it works on an action.")]
    public float actionBarWidth = 0.7f;

    [Tooltip("Offset from the operator to the bar, so it does not sit under the sprite.")]
    public Vector2 actionBarOffset = new(0f, -0.45f);

    public Color actionBarColor = new(1f, 0.85f, 0.35f, 1f);
    public Color actionBarTrackColor = new(0.1f, 0.11f, 0.13f, 0.75f);

    [Header("Look indicator")]
    public float lookLength = 0.95f;
    public Color lookColor = new(0.06f, 0.06f, 0.08f, 0.9f);

    [Header("Look target")]
    public Color lookMarkerColor = new(1f, 0.72f, 0.27f, 0.85f);
    public float lookMarkerSize = 0.16f;

    [Header("Path")]
    [Tooltip("The class colour. #7fd4ff is the prototype's assaulter.")]
    public Color pathColor = new(0.498f, 0.831f, 1f, 1f);

    [Tooltip("Distance between direction arrows along the path.")]
    public float pathMarkerSpacing = 2.2f;

    public float pathMarkerSize = 0.28f;

    [Tooltip("Curve samples per drawn segment. 1 draws the raw polyline.")]
    [Range(1, 12)]
    public int pathSmoothing = 6;

    [Header("Gun placeholder")]
    [Tooltip("Leave empty for a generated white bar.")]
    public Sprite gunSprite;

    public Vector2 gunOffset = new(0.16f, -0.14f);
    public Vector2 gunSize = new(0.52f, 0.1f);
    public Color gunColor = new(0.13f, 0.14f, 0.16f, 1f);
}
