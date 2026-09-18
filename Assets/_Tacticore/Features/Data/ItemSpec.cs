using UnityEngine;

/// <summary>
///     One piece of kit an operator carries: a helmet, a plate carrier, a grenade. For now it is only
///     a name and a picture on the deployment card; what the item does comes later.
/// </summary>
[CreateAssetMenu(fileName = "ItemSpec", menuName = "Tacticore/Item Spec")]
public class ItemSpec : ScriptableObject
{
    public string displayName = "ITEM";

    [Tooltip("White or light grey on transparent. The card tints it.")]
    public Texture2D icon;

    [Min(1)]
    public int count = 1;
}
