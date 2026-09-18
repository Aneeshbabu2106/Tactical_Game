using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     What happened, in numbers, over the room it happened in.
/// </summary>
/// <remarks>
///     Stars follow the prototype's finish(): under par time, no casualties, and — standing in for
///     its third, which counts civilians we do not have — never being seen. That last one is worth
///     keeping because the alert model can actually answer it, and because it is the one a player
///     has to change how they play to earn.
/// </remarks>
public sealed class ResultsScreen
{
    private readonly MissionState mission;
    private readonly Label title;
    private readonly Label stars;
    private readonly VisualElement lines;

    public ResultsScreen(VisualElement root, MissionState mission, System.Action menu, System.Action retry)
    {
        this.mission = mission;

        title = root.Q<Label>("result-title");
        stars = root.Q<Label>("result-stars");
        lines = root.Q<VisualElement>("result-stats");

        UiButton.Bind(root.Q<Label>("result-menu"), menu);
        UiButton.Bind(root.Q<Label>("result-retry"), retry);
    }

    /// <summary>Built once on arrival. Nothing behind it is still changing.</summary>
    public void Build()
    {
        var won = mission.Outcome == MissionOutcome.Win;

        title.text = won ? "AREA CLEAR" : "OPERATOR DOWN";
        title.EnableInClassList("loss", !won);

        // Three slots either way, so a missing star reads as one you did not get rather than as a
        // shorter row of stars.
        stars.text = (mission.StarTime ? "★" : "☆")
                     + (mission.StarNoCasualties ? "★" : "☆")
                     + (mission.StarUndetected ? "★" : "☆");

        lines.Clear();

        Line("TIME", $"{Mathf.FloorToInt(mission.Elapsed / 60f)}:{mission.Elapsed % 60f:00.0}",
            mission.StarTime);

        Line("PAR", $"{Mathf.FloorToInt(mission.ParSeconds / 60f)}:{mission.ParSeconds % 60f:00.0}");

        Line("TANGOS DOWN", $"{mission.EnemiesDown} / {mission.EnemyCount}");

        Line("ROUNDS FIRED", mission.ShotsFired.ToString());

        // Nothing fired is not 0% accurate; it is a mission with no shooting in it.
        Line("ACCURACY", mission.ShotsFired > 0
            ? $"{(float)mission.Hits / mission.ShotsFired:P0}"
            : "—");

        Line("OPERATORS LOST", mission.OperatorsLost.ToString(), mission.StarNoCasualties);
        Line("UNDETECTED", mission.StarUndetected ? "YES" : "NO", mission.StarUndetected);
    }

    private void Line(string label, string value, bool? good = null)
    {
        var row = new VisualElement();
        row.AddToClassList("stat-line");

        var name = new Label(label);
        name.AddToClassList("stat-line-label");

        var read = new Label(value);
        read.AddToClassList("stat-line-value");

        if (good.HasValue)
        {
            read.AddToClassList(good.Value ? "good" : "bad");
        }

        row.Add(name);
        row.Add(read);
        lines.Add(row);
    }
}
