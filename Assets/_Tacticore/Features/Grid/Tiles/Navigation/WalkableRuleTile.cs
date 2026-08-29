using UnityEngine;

[CreateAssetMenu(
    fileName = "WalkableRuleTile",
    menuName = "Tacticore/Rule Tiles/Walkable"
)]
public class WalkableRuleTile : NavigationRuleTile
{
    public override NavigationType Type => NavigationType.Walkable;
}
