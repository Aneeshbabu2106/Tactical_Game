using UnityEngine;

/// <summary>
///     Who one operator is, as opposed to what his class can do: his number and callsign, his
///     portrait, the colour his card is drawn in, and the kit he carries into this job.
/// </summary>
/// <remarks>
///     Kept apart from <see cref="OperatorSpec" />. The spec is shared by every assaulter; two men of
///     the same class still have different faces and carry different grenades, and putting that on
///     the class asset would make every assaulter the same man.
/// </remarks>
[CreateAssetMenu(fileName = "OperatorProfile", menuName = "Tacticore/Operator Profile")]
public class OperatorProfile : ScriptableObject
{
    [Header("Identity")]
    public string callsign = "OPERATOR";

    [Tooltip("Shown on the badge and after the callsign. Text, so a leading zero survives.")]
    public string number = "01";

    [Tooltip("Card portrait. Greyscale reads best: the card tints it with the accent colour.")]
    public Texture2D portrait;

    [Tooltip("The colour this man's card, badge and bars are drawn in.")]
    public Color accent = new(1f, 0.78f, 0.2f, 1f);

    [Header("Card stats")]
    [Tooltip("Shown on the deployment card. Not yet applied to damage taken.")]
    public int armor = 30;

    [Header("Loadout")]
    [Tooltip("Slots after the weapon, in order. The weapon always takes slot 1.")]
    public ItemSpec[] items;
}
