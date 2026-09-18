using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     The detail panel for whoever is selected: what he is carrying, what is left of him, and what
///     he has been told to do.
/// </summary>
/// <remarks>
///     Dimmed rather than hidden when nobody is selected. A panel that vanishes and reappears makes
///     the layout jump, and the empty frame is a useful reminder that clicking a man fills it.
/// </remarks>
public sealed class OperatorPanel
{
    private readonly VisualElement panel;
    private readonly Label who;
    private readonly Label role;
    private readonly VisualElement healthFill;
    private readonly Label healthValue;
    private readonly Label weapon;
    private readonly VisualElement ammoFill;
    private readonly Label ammoValue;
    private readonly Label engagement;
    private readonly Label order;

    public OperatorPanel(VisualElement root)
    {
        panel = root.Q<VisualElement>("operator-panel");
        who = root.Q<Label>("op-name");
        role = root.Q<Label>("op-class");
        healthFill = root.Q<VisualElement>("op-health-fill");
        healthValue = root.Q<Label>("op-health-value");
        weapon = root.Q<Label>("op-weapon");
        ammoFill = root.Q<VisualElement>("op-ammo-fill");
        ammoValue = root.Q<Label>("op-ammo-value");
        engagement = root.Q<Label>("op-engagement");
        order = root.Q<Label>("op-order");
    }

    public void Update(Operator op)
    {
        panel.EnableInClassList("empty", op == null);

        if (op == null)
        {
            who.text = "NO ONE SELECTED";
            role.text = "";
            healthValue.text = "—";
            weapon.text = "—";
            ammoValue.text = "—";
            engagement.text = "—";
            order.text = "—";
            healthFill.style.width = Length.Percent(0f);
            ammoFill.style.width = Length.Percent(0f);
            return;
        }

        who.text = op.name;
        role.text = op.DisplayName;

        var health = op.GetComponent<Health>();
        var fraction = health != null && health.Maximum > 0f
            ? Mathf.Clamp01(health.Current / health.Maximum)
            : 1f;

        healthFill.style.width = Length.Percent(fraction * 100f);
        healthFill.EnableInClassList("hurt", fraction <= 0.6f && fraction > 0.25f);
        healthFill.EnableInClassList("critical", fraction <= 0.25f);
        healthValue.text = health != null
            ? $"{Mathf.CeilToInt(health.Current)} / {Mathf.CeilToInt(health.Maximum)}"
            : "—";

        weapon.text = op.WeaponName;

        if (op.TryGetComponent(out OperatorCombat combat))
        {
            var gun = combat.Weapon;
            var loaded = gun.MagazineSize > 0 ? (float)gun.Ammo / gun.MagazineSize : 0f;

            // While reloading the bar shows the reload filling rather than the rounds left, so the
            // one bar answers "can he shoot yet" in both states.
            ammoFill.style.width = Length.Percent(
                (gun.IsReloading ? gun.ReloadProgress : loaded) * 100f);

            ammoFill.EnableInClassList("reloading", gun.IsReloading);

            ammoValue.text = gun.IsReloading
                ? $"RELOADING {Mathf.RoundToInt(gun.ReloadProgress * 100f)}%"
                : $"{gun.Ammo} / {gun.MagazineSize}";
        }
        else
        {
            ammoFill.style.width = Length.Percent(0f);
            ammoValue.text = "—";
        }

        engagement.text = op.Engagement switch
        {
            EngagementMode.WaitToClear => "WAIT TO CLEAR",
            EngagementMode.HoldFire => "HOLD FIRE",
            _ => "KEEP MOVING"
        };

        order.text = Order(op);
    }

    /// <summary>
    ///     Read in the order the motor resolves it, so what is shown is what is actually governing
    ///     him rather than the first thing that happens to be true.
    /// </summary>
    private static string Order(Operator op)
    {
        if (!op.IsAlive)
        {
            return "DOWN";
        }

        if (op.IsBusy)
        {
            return "WORKING";
        }

        if (op.IsHolding)
        {
            return "HOLDING TO SHOOT";
        }

        if (op.IsMoving)
        {
            return op.IsRunning ? "RUNNING" : "WALKING";
        }

        return op.Plan != null && op.Plan.Points.Count > 0 ? "READY" : "NO ORDERS";
    }
}
