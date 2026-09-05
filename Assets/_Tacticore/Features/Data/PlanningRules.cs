using UnityEngine;

/// <summary>
///     How drawing an order behaves: what the cursor grabs, where waypoints land, how much a stroke
///     is smoothed. Feel of the instrument rather than anything about the man holding it, so every
///     class shares one asset — a squad whose classes each grabbed at a different radius would read
///     as inconsistent input, not as characterisation.
/// </summary>
[CreateAssetMenu(fileName = "PlanningRules", menuName = "Tacticore/Planning Rules")]
public class PlanningRules : ScriptableObject
{
    [Header("Picking")]
    [Tooltip("How close the cursor must be, in cells, to start drawing from an operator.")]
    public float operatorPickRadius = 0.45f;

    public float waypointPickRadius = 0.32f;

    [Tooltip("A drawn path starts this far out from the operator, clear of its sprite and pick radius.")]
    public float pathStartClearance = 0.55f;

    [Tooltip("Cursor distance from the path that shows the add-waypoint marker.")]
    public float pathHoverThreshold = 0.35f;

    [Header("Waypoints")]
    [Tooltip("Degrees of accumulated turn along the stroke that earns a waypoint.")]
    public float waypointTurnThreshold = 40f;

    [Tooltip("Closest two auto-placed waypoints may sit, so a shaky hand cannot cluster them.")]
    public float waypointMinSpacing = 1.2f;

    [Tooltip("A straight run longer than this gets a waypoint anyway. Zero disables.")]
    public float waypointMaxSpacing = 8f;

    [Header("Smoothing")]
    [Tooltip("Curve samples per drawn segment. 1 draws the raw polyline.")]
    [Range(1, 12)]
    public int pathSmoothing = 6;
}
