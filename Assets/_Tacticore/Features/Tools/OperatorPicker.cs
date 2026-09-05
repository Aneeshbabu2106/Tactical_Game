using UnityEngine;

/// <summary>
///     Shared hit-test so the path and look tools agree on what counts as clicking an operator.
/// </summary>
public static class OperatorPicker
{
    public static Operator At(Vector3 worldPosition)
    {
        Operator best = null;
        var bestDistance = float.MaxValue;

        foreach (var candidate in Object.FindObjectsByType<Operator>(FindObjectsSortMode.None))
        {
            if (!candidate.IsAlive)
            {
                continue;
            }

            // 2D: the operator sprite may sit on any z for sorting, the cursor is always on z 0.
            var distance = Vector2.Distance(candidate.transform.position, worldPosition);

            if (distance <= candidate.PickRadius && distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>
    ///     The closest operator regardless of pick radius, for orders aimed at the world rather than
    ///     at a man — a door gets worked by whoever is nearest it.
    /// </summary>
    public static Operator Nearest(Vector3 worldPosition)
    {
        Operator best = null;
        var bestDistance = float.MaxValue;

        foreach (var candidate in Object.FindObjectsByType<Operator>(FindObjectsSortMode.None))
        {
            if (!candidate.IsAlive)
            {
                continue;
            }

            var distance = Vector2.Distance(candidate.transform.position, worldPosition);

            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }
}
