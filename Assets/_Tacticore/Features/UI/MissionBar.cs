using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     The strip along the top: whether the clock is running, how long the mission has taken, how
///     many of them are left and how much has been spent doing it.
/// </summary>
/// <remarks>
///     The clock is the <em>simulated</em> one. Play begins paused so the player can plan, and a
///     mission timer that ran while nothing moved would be measuring the player rather than the
///     mission.
/// </remarks>
public sealed class MissionBar
{
    private readonly VisualElement dot;
    private readonly Label state;
    private readonly Label clock;
    private readonly Label tangos;
    private readonly Label rounds;

    public MissionBar(VisualElement root)
    {
        dot = root.Q<VisualElement>("state-dot");
        state = root.Q<Label>("state-label");
        clock = root.Q<Label>("clock");
        tangos = root.Q<Label>("tangos-value");
        rounds = root.Q<Label>("rounds-value");
    }

    public void Update(float elapsed, IReadOnlyList<Enemy> enemies, IReadOnlyList<Operator> squad)
    {
        var running = !SimClock.IsPaused;

        state.text = running ? "RUNNING" : "PAUSED";
        state.EnableInClassList("running", running);
        dot.EnableInClassList("running", running);

        clock.text = $"{Mathf.FloorToInt(elapsed / 60f)}:{elapsed % 60f:00.0}";

        var alive = 0;

        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.IsAlive)
            {
                alive++;
            }
        }

        tangos.text = $"{alive}/{enemies.Count}";

        var fired = 0;

        foreach (var op in squad)
        {
            if (op != null && op.TryGetComponent(out OperatorCombat combat))
            {
                fired += combat.ShotsFired;
            }
        }

        rounds.text = fired.ToString();
    }
}
