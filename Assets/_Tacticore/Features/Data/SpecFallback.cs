using UnityEngine;

/// <summary>
///     Shared by the spec assets so an unwired reference still runs on its C# defaults rather than
///     throwing. Keeps a half-authored enemy or class playable while it is being roughed out.
/// </summary>
public static class SpecFallback
{
    /// <summary>
    ///     The assigned asset, or a throwaway instance carrying the C# defaults. Never null, so
    ///     callers read <c>spec.Weapon.damage</c> without a null check at every use.
    /// </summary>
    public static T Or<T>(T assigned, ref T fallback) where T : ScriptableObject
    {
        if (assigned != null)
        {
            return assigned;
        }

        if (fallback == null)
        {
            fallback = ScriptableObject.CreateInstance<T>();
            fallback.hideFlags = HideFlags.HideAndDontSave;
        }

        return fallback;
    }
}
