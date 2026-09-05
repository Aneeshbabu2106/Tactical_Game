using UnityEngine;

/// <summary>
///     One enemy type: what it takes to kill him, what he is holding, how well he sees and how well
///     he shoots. The opposition's answer to <see cref="OperatorSpec" />.
/// </summary>
/// <remarks>
///     Ported from the prototype's TIERS table, which is deliberately thin — an archetype is a
///     weapon, a health pool and an accuracy, not a behaviour. Every type runs the same brain; a
///     heavy is not braver than a thug, he is simply harder to put down and carries a shotgun.
///     <para>
///         The reaction timings are not here. They live on <see cref="AlertProfile" />, shared by
///         every type, because in the prototype they vary with how alert a man is and not with who
///         he is.
///     </para>
/// </remarks>
[CreateAssetMenu(fileName = "EnemySpec", menuName = "Tacticore/Enemy Spec")]
public class EnemySpec : ScriptableObject
{
    public string displayName = "TANGO";

    [Header("Health")]
    public float maxHealth = 100f;

    [Tooltip("Fraction of incoming damage stopped, times 0.4. The prototype's heavy is 0.60, so it "
             + "takes 76% of what hits it.")]
    [Range(0f, 1f)]
    public float armour;

    [Tooltip("Colour the body is tinted once it goes down.")]
    public Color downedTint = new(0.32f, 0.16f, 0.18f, 1f);

    [Header("Shooting")]
    [Tooltip("Chance to hit at comfortable range, before range and movement are taken into account.")]
    [Range(0f, 1f)]
    public float accuracy = 0.58f;

    [Tooltip("Scales the weapon's damage for this type. The prototype's tierMult.")]
    public float damageMultiplier = 1f;

    [Header("Movement")]
    [Tooltip("Cells per second while patrolling or walking to a noise.")]
    public float moveSpeed = 2.3f;

    [Header("Placeholder marker")]
    [Tooltip("The disc drawn until there is a sprite for this type.")]
    public Color markerColor = new(0.93f, 0.29f, 0.31f, 1f);

    public float markerSize = 0.62f;

    [Header("Weapon placeholder")]
    [Tooltip("A bar for the gun he is holding, as the operator has. Sized per type, so a shotgun "
             + "reads as a shotgun before there is any art.")]
    public Vector2 gunOffset = new(0.16f, -0.14f);

    public Vector2 gunSize = new(0.52f, 0.1f);
    public Color gunColor = new(0.13f, 0.14f, 0.16f, 1f);

    [Header("Shared data")]
    [SerializeField] private WeaponSpec weapon;
    [SerializeField] private VisionSpec vision;
    [SerializeField] private AlertProfile alert;

    private WeaponSpec weaponFallback;
    private VisionSpec visionFallback;
    private AlertProfile alertFallback;

    public WeaponSpec Weapon => SpecFallback.Or(weapon, ref weaponFallback);

    public VisionSpec Vision => SpecFallback.Or(vision, ref visionFallback);

    public AlertProfile Alert => SpecFallback.Or(alert, ref alertFallback);

    /// <summary>Damage actually dealt by one round from this type.</summary>
    public float DamagePerRound => Weapon.damage * damageMultiplier;

    /// <summary>What is left of a hit after this type's armour.</summary>
    public float Absorb(float damage)
    {
        return damage * (1f - armour * 0.4f);
    }
}
