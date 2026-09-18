/// <summary>Which screen the player is looking at. The mission only ticks on one of them.</summary>
public enum GameScreen
{
    MainMenu,

    /// <summary>Choosing a way in and what to carry. The map is visible but nothing moves.</summary>
    Deployment,

    /// <summary>Playing. Still begins paused, so the first orders are given before the clock runs.</summary>
    Mission,

    /// <summary>Over, one way or the other, with the numbers on screen.</summary>
    Results
}
