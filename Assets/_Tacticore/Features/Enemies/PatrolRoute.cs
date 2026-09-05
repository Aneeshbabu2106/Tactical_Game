using UnityEngine;

/// <summary>
///     A looping walk, authored as child transforms so the route is dragged around in the Scene view
///     rather than typed in as numbers.
/// </summary>
/// <remarks>
///     Deliberately dumb: it hands out the next point and says when one has been reached. Whether
///     the enemy is walking it, has abandoned it for a noise, or is coming back to it afterwards is
///     the brain's business, not the route's.
/// </remarks>
[DisallowMultipleComponent]
public class PatrolRoute : MonoBehaviour
{
    [Tooltip("How close counts as having reached a point, in cells.")]
    [SerializeField] private float arriveRadius = 0.6f;

    [Header("At each point")]
    [Tooltip("Seconds spent standing at a point before walking on. A guard who never stops reads as "
             + "a patrolling machine; stopping to look is most of what makes him read as a man.")]
    [SerializeField] private float dwellSeconds = 2.4f;

    [Tooltip("Random spread on the dwell, so two men on separate routes do not fall into step.")]
    [SerializeField] private float dwellJitter = 0.8f;

    [Tooltip("How far either side of centre he sweeps while standing there. Zero holds still.")]
    [SerializeField] private float scanDegrees = 55f;

    [Tooltip("Sweeps around the bearing of the leg he is about to walk. Tick this to sweep around "
             + "the point's own Z rotation instead, so a post can be made to watch a doorway.")]
    [SerializeField] private bool useAuthoredFacing;

    [SerializeField] private Color gizmoColor = new(1f, 0.45f, 0.25f, 0.9f);

    private int index;

    /// <summary>Points are the child transforms, in hierarchy order. Fewer than two is no route.</summary>
    public int Count => transform.childCount;

    public bool HasRoute => Count >= 2;

    public Vector3 Current => transform.GetChild(index % Count).position;

    /// <summary>Seconds to stand at a point, varied a little so routes drift out of phase.</summary>
    public float Dwell => Mathf.Max(0f, dwellSeconds + Random.Range(-dwellJitter, dwellJitter));

    public float ScanDegrees => scanDegrees;

    /// <summary>
    ///     Which way to look while standing at the current point: down the next leg by default, or
    ///     the point's own rotation when the route is authored to watch something in particular.
    /// </summary>
    public float FacingAt(Vector3 from)
    {
        if (useAuthoredFacing)
        {
            return transform.GetChild(index % Count).eulerAngles.z;
        }

        var next = transform.GetChild((index + 1) % Count).position - from;

        return next.sqrMagnitude < 1e-6f
            ? 0f
            : Mathf.Atan2(next.y, next.x) * Mathf.Rad2Deg;
    }

    /// <summary>True once the walker is standing on the current point. Does not advance.</summary>
    public bool IsAt(Vector3 at)
    {
        return HasRoute && Vector2.Distance(at, Current) <= arriveRadius;
    }

    /// <summary>Moves on to the next point. Separate from arriving so a walker can stand a while.</summary>
    public void Advance()
    {
        if (HasRoute)
        {
            index = (index + 1) % Count;
        }
    }

    /// <summary>
    ///     Aims the walker at whichever point he is nearest, so an enemy coming back from a search
    ///     rejoins the loop where he stands rather than trekking back to where he left it.
    /// </summary>
    public void RejoinNearest(Vector3 at)
    {
        if (!HasRoute)
        {
            return;
        }

        var best = 0;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < Count; i++)
        {
            var distance = Vector2.Distance(at, transform.GetChild(i).position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        index = best;
    }

    private void OnDrawGizmos()
    {
        if (!HasRoute)
        {
            return;
        }

        Gizmos.color = gizmoColor;

        for (var i = 0; i < Count; i++)
        {
            var from = transform.GetChild(i).position;
            var to = transform.GetChild((i + 1) % Count).position;

            Gizmos.DrawLine(from, to);
            Gizmos.DrawWireSphere(from, arriveRadius * 0.5f);
        }
    }
}
