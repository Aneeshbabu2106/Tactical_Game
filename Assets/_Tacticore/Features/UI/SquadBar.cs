using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     One card per operator along the bottom: who he is, what state he is in, how much is left in
///     the magazine. Clicking a card selects him.
/// </summary>
/// <remarks>
///     Cards are built once per roster change rather than every frame — rebuilding the tree each
///     frame would throw away hover and any in-flight click. Only the text and the classes are
///     touched on the frames in between.
/// </remarks>
public sealed class SquadBar
{
    private sealed class Card
    {
        public VisualElement Root;
        public Label Name;
        public Label Mode;
        public VisualElement HealthFill;
        public Label Ammo;
    }

    private readonly VisualElement root;
    private readonly Action<Operator> select;
    private readonly List<Card> cards = new();
    private readonly List<Operator> shown = new();

    public SquadBar(VisualElement root, Action<Operator> select)
    {
        this.root = root;
        this.select = select;
    }

    public void Update(IReadOnlyList<Operator> squad)
    {
        Rebuild(squad);

        for (var i = 0; i < cards.Count; i++)
        {
            var op = shown[i];
            var card = cards[i];

            if (op == null)
            {
                continue;
            }

            card.Root.EnableInClassList("selected", op.IsSelected);
            card.Root.EnableInClassList("downed", !op.IsAlive);

            card.Name.text = op.DisplayName;
            card.Mode.text = !op.IsAlive ? "DOWN" : Mode(op);

            var health = op.GetComponent<Health>();
            var fraction = health != null && health.Maximum > 0f
                ? Mathf.Clamp01(health.Current / health.Maximum)
                : 1f;

            card.HealthFill.style.width = Length.Percent(fraction * 100f);
            card.HealthFill.EnableInClassList("hurt", fraction <= 0.6f && fraction > 0.25f);
            card.HealthFill.EnableInClassList("critical", fraction <= 0.25f);

            card.Ammo.text = op.TryGetComponent(out OperatorCombat combat)
                ? Ammo(combat.Weapon)
                : "—";
        }
    }

    private static string Mode(Operator op)
    {
        return op.Engagement switch
        {
            EngagementMode.WaitToClear => "WAIT TO CLEAR",
            EngagementMode.HoldFire => "HOLD FIRE",
            _ => "KEEP MOVING"
        };
    }

    private static string Ammo(Weapon weapon)
    {
        return weapon.IsReloading
            ? $"RELOADING {Mathf.RoundToInt(weapon.ReloadProgress * 100f)}%"
            : $"{weapon.Ammo} / {weapon.MagazineSize}";
    }

    /// <summary>Only touches the tree when the roster itself has changed.</summary>
    private void Rebuild(IReadOnlyList<Operator> squad)
    {
        if (Matches(squad))
        {
            return;
        }

        root.Clear();
        cards.Clear();
        shown.Clear();

        foreach (var op in squad)
        {
            if (op == null)
            {
                continue;
            }

            var target = op;

            var host = new VisualElement();
            host.AddToClassList("squad-card");
            host.RegisterCallback<ClickEvent>(_ => select(target));

            var top = new VisualElement();
            top.AddToClassList("card-top");

            var label = new Label(op.DisplayName);
            label.AddToClassList("card-name");

            var mode = new Label();
            mode.AddToClassList("card-mode");

            top.Add(label);
            top.Add(mode);

            var meter = new VisualElement();
            meter.AddToClassList("meter");

            var fill = new VisualElement();
            fill.AddToClassList("meter-fill");
            fill.AddToClassList("health");
            meter.Add(fill);

            var ammo = new Label();
            ammo.AddToClassList("card-ammo");

            host.Add(top);
            host.Add(meter);
            host.Add(ammo);
            root.Add(host);

            cards.Add(new Card
            {
                Root = host,
                Name = label,
                Mode = mode,
                HealthFill = fill,
                Ammo = ammo
            });

            shown.Add(op);
        }
    }

    private bool Matches(IReadOnlyList<Operator> squad)
    {
        if (shown.Count != squad.Count)
        {
            return false;
        }

        for (var i = 0; i < shown.Count; i++)
        {
            if (shown[i] != squad[i])
            {
                return false;
            }
        }

        return true;
    }
}
