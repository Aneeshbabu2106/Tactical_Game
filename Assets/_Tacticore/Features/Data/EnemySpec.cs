using UnityEngine;

/// <summary>
///     One enemy type. Static for now — it stands where it is placed and is shot at — so this is
///     what it takes to kill it and what it looks like while there is no art for it.
/// </summary>
[CreateAssetMenu(fileName = "EnemySpec", menuName = "Tacticore/Enemy Spec")]
public class EnemySpec : ScriptableObject
{
    public string displayName = "TANGO";

    [Header("Health")]
    public float maxHealth = 100f;

    [Tooltip("Colour the body is tinted once it goes down.")]
    public Color downedTint = new(0.32f, 0.16f, 0.18f, 1f);

    [Header("Placeholder marker")]
    [Tooltip("The disc drawn until there is a sprite for this type.")]
    public Color markerColor = new(0.93f, 0.29f, 0.31f, 1f);

    public float markerSize = 0.62f;
}
