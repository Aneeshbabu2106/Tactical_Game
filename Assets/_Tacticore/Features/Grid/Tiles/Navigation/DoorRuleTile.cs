using UnityEngine;

[CreateAssetMenu(
    fileName = "DoorRuleTile",
    menuName = "Tacticore/Rule Tiles/Door"
)]
public class DoorRuleTile : NavigationRuleTile
{
    public override NavigationType Type => NavigationType.Door;
}
