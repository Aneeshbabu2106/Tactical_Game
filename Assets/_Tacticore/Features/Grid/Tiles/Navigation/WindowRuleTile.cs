using UnityEngine;

[CreateAssetMenu(
    fileName = "WindowRuleTile",
    menuName = "Tacticore/Rule Tiles/Window"
)]
public class WindowRuleTile : NavigationRuleTile
{
    public override NavigationType Type => NavigationType.Window;
}
