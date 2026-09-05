using UnityEngine;

/// <summary>
///     A pair of eyes. Separate from the operator so the cone can be retuned for everyone at once,
///     and so an enemy can later be given a narrower one without touching operator data.
/// </summary>
[CreateAssetMenu(fileName = "VisionSpec", menuName = "Tacticore/Vision Spec")]
public class VisionSpec : ScriptableObject
{
    [Tooltip("Total width of the view cone in degrees. 120 is the prototype's operator.")]
    public float fovDegrees = 120f;

    [Tooltip("How far he can see, in cells. Not the prototype's 18 — that is metres at half-metre "
             + "tiles, and this map is 24 cells across.")]
    public float range = 12f;

    [Tooltip("Radius he is aware of in every direction, so he is never blind at his own shoulder.")]
    public float nearRadius = 1.6f;

    [Tooltip("Angle between rays. Finer is smoother and costs more; under a cell of arc is enough.")]
    [Range(0.5f, 10f)]
    public float stepDegrees = 2f;
}
