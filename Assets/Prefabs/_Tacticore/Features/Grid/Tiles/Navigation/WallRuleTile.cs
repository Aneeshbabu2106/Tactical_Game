using UnityEngine;

[CreateAssetMenu(
    fileName = "WallRuleTile",
    menuName = "Tacticore/Rule Tiles/Wall"
)]
public class WallRuleTile : NavigationRuleTile
{
    public override NavigationType Type => NavigationType.Wall;
}
