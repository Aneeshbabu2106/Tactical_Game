using UnityEngine;

/// <summary>
///     One weapon's numbers. Held apart from whoever carries it, so an operator and an enemy can be
///     issued the same carbine and it is tuned once.
/// </summary>
[CreateAssetMenu(fileName = "WeaponSpec", menuName = "Tacticore/Weapon Spec")]
public class WeaponSpec : ScriptableObject
{
    public string displayName = "CARBINE";

    [Tooltip("Damage per round. The prototype's carbine is 34.")]
    public float damage = 34f;

    [Tooltip("Rounds per minute. Carbine is 750.")]
    public float roundsPerMinute = 750f;

    public int magazineSize = 30;

    [Tooltip("Seconds to change a magazine. The prototype's dry reload is 2.")]
    public float reloadSeconds = 2f;

    [Tooltip("How far the carrier will engage, in cells. Kept at or under his vision range — he "
             + "cannot shoot what he cannot see.")]
    public float range = 12f;

    [Tooltip("Chance to hit at comfortable range. Falls to 55% of this past 70% of the range.")]
    [Range(0f, 1f)]
    public float accuracy = 0.66f;
}
