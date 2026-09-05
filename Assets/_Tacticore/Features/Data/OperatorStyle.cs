using UnityEngine;

/// <summary>
///     How an operator and his order are drawn: marker sizes, bar geometry, the placeholder gun.
///     Shared by every class, because these are the squad's visual language rather than anything
///     that distinguishes one man from another. The two colours that <em>do</em> distinguish a class
///     — its path and cone tint — stay on <see cref="OperatorSpec" />.
/// </summary>
[CreateAssetMenu(fileName = "OperatorStyle", menuName = "Tacticore/Operator Style")]
public class OperatorStyle : ScriptableObject
{
    [Header("Waypoints")]
    public float waypointMarkerSize = 0.3f;

    [Tooltip("Ring colour for a waypoint set to run.")]
    public Color waypointRunColor = new(1f, 0.55f, 0.27f, 1f);

    [Tooltip("Ring colour for a waypoint carrying an unfinished action, such as opening a door.")]
    public Color waypointActionColor = new(1f, 0.85f, 0.35f, 1f);

    [Header("Path")]
    [Tooltip("Distance between direction arrows along the path.")]
    public float pathMarkerSpacing = 2.2f;

    public float pathMarkerSize = 0.28f;

    [Header("Action progress")]
    [Tooltip("Width of the bar shown under an operator while he works on an action.")]
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

    [Header("Gun placeholder")]
    [Tooltip("Leave empty for a generated white bar.")]
    public Sprite gunSprite;

    public Vector2 gunOffset = new(0.16f, -0.14f);
    public Vector2 gunSize = new(0.52f, 0.1f);
    public Color gunColor = new(0.13f, 0.14f, 0.16f, 1f);
}
