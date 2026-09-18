using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     The mission brief laid over the left of the map, the recruits down the right, and the building
///     framed in the space the recruit panel leaves.
/// </summary>
/// <remarks>
///     A recruit is sent in by dragging his card onto an entry marker on the map, or by selecting
///     him and clicking the marker. Both go through <see cref="MissionState.Assign" />, so the two
///     cannot disagree. The tag in a card's corner undeploys him; dragging him back onto the recruit
///     panel does the same.
///     <para>
///         The brief is an overlay, not a column. The map is framed as though it were closed, so
///         opening and closing it never moves the building under the player's cursor.
///     </para>
///     <para>
///         Entry points are whatever <see cref="DeployPoints" /> has as children, named whatever
///         they are named. Nothing here assumes a compass direction or a count.
///     </para>
/// </remarks>
public sealed class DeploymentScreen
{
    private sealed class Card
    {
        public VisualElement Root;
        public VisualElement Frame;
        public VisualElement Badge;
        public Label BadgeNumber;
        public VisualElement TagBox;
        public Label Tag;
        public bool TagHovered;
        public Color Accent;
        public string Name;
    }

    /// <summary>Panel pixels a press has to travel before it is a drag rather than a click.</summary>
    private const float DragThreshold = 8f;

    /// <summary>Panel pixels kept clear between the building and the panel beside it.</summary>
    private const float FramePadding = 18f;

    /// <summary>Where the stat bars are full. Display scales only, not game rules.</summary>
    private const float HealthScale = 150f;

    private const float ArmorScale = 100f;
    private const float RangeScale = 20f;

    /// <summary>Card colours for operators without a profile, in roster order.</summary>
    private static readonly Color[] Palette =
    {
        new(1f, 0.78f, 0.2f, 1f),
        new(0.25f, 0.72f, 1f, 1f),
        new(0.55f, 0.9f, 0.45f, 1f),
        new(0.96f, 0.46f, 0.4f, 1f)
    };

    private readonly MissionState mission;
    private readonly PointerInput pointer;
    private readonly DeployMarkers markers;
    private readonly Action<Rect> frame;

    private readonly VisualElement layer;
    private readonly VisualElement briefPanel;
    private readonly VisualElement briefToggle;
    private readonly VisualElement recruitPanel;
    private readonly VisualElement recruitList;
    private readonly VisualElement countBlock;
    private readonly VisualElement pips;
    private readonly VisualElement objectiveList;
    private readonly VisualElement starList;
    private readonly VisualElement intelList;
    private readonly Label missionName;
    private readonly Label briefMeta;
    private readonly Label briefingText;
    private readonly Label deployedCount;
    private readonly Label summary;
    private readonly Label go;

    private readonly VisualElement ghost;
    private readonly Label ghostBadge;
    private readonly Label ghostTarget;

    private readonly List<Card> cards = new();

    private bool briefOpen = true;

    private int pressed = -1;
    private bool dragging;
    private Vector2 pressedAt;
    private Vector2 lastPointer;
    private int dropPoint = -1;

    private Rect lastFrame;
    private string notice;
    private IVisualElementScheduledItem noticeExpiry;

    public DeploymentScreen(
        VisualElement root, MissionState mission, PointerInput pointer, DeployMarkers markers,
        Action<Rect> frame, Action back, Action begin)
    {
        this.mission = mission;
        this.pointer = pointer;
        this.markers = markers;
        this.frame = frame;

        layer = root.Q<VisualElement>("deployment");
        briefPanel = root.Q<VisualElement>("brief-panel");
        briefToggle = root.Q<VisualElement>("brief-toggle");
        recruitPanel = root.Q<VisualElement>("recruit-panel");
        recruitList = root.Q<VisualElement>("recruit-list");
        countBlock = root.Q<VisualElement>("deploy-count-block");
        pips = root.Q<VisualElement>("deploy-pips");
        objectiveList = root.Q<VisualElement>("objective-list");
        starList = root.Q<VisualElement>("star-list");
        intelList = root.Q<VisualElement>("intel-list");
        missionName = root.Q<Label>("deploy-mission");
        briefMeta = root.Q<Label>("brief-meta");
        briefingText = root.Q<Label>("briefing-text");
        deployedCount = root.Q<Label>("deployed-count");
        summary = root.Q<Label>("deploy-summary");
        go = root.Q<Label>("deploy-go");

        UiButton.Bind(briefToggle, ToggleBrief);
        UiButton.Bind(root.Q<Label>("deploy-back"), back);
        UiButton.Bind(go, begin);

        // Only the recruit panel decides the framing; the brief floats over the map.
        recruitPanel.RegisterCallback<GeometryChangedEvent>(_ => Reframe());

        ghost = new VisualElement { pickingMode = PickingMode.Ignore };
        ghost.AddToClassList("drag-ghost");
        ghostBadge = Text("", "drag-ghost-badge");
        ghostTarget = Text("", "drag-ghost-target");
        ghost.Add(ghostBadge);
        ghost.Add(ghostTarget);
        ghost.style.display = DisplayStyle.None;
        layer.Add(ghost);
    }

    /// <summary>Rebuilt on entering the screen; the briefing and the roster cannot change while up.</summary>
    public void Build()
    {
        EndDrag();

        var entries = mission.Points != null ? mission.Points.Count : 0;
        var hostiles = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Exclude).Length;
        var par = Clock(mission.ParSeconds);

        missionName.text = mission.MissionName;
        briefMeta.text = $"PAR {par}   ·   {hostiles} HOSTILES   ·   {entries} ENTRIES";
        briefingText.text = mission.Briefing;

        BuildObjectives();
        BuildStars(par);

        intelList.Clear();
        intelList.Add(Intel($"{hostiles}", "HOSTILES REPORTED"));
        intelList.Add(Intel($"{entries}", "ENTRY POINTS"));
        intelList.Add(Intel($"{mission.MaxDeployed}", "SQUAD LIMIT"));
        intelList.Add(Intel(par, "PAR TIME"));

        pips.Clear();

        for (var i = 0; i < mission.MaxDeployed; i++)
        {
            var pip = new VisualElement();
            pip.AddToClassList("pip");
            pips.Add(pip);
        }

        BuildCards();
        ApplyBrief();
        Refresh();

        lastFrame = new Rect(-1f, -1f, 0f, 0f);
        layer.schedule.Execute(Reframe);
    }

    /// <summary>Redrawn whenever the choice changes, from the cards or from the map.</summary>
    public void Refresh()
    {
        var men = mission.DeployedCount;
        var max = mission.MaxDeployed;

        for (var i = 0; i < cards.Count; i++)
        {
            var deployed = mission.AssignedPoint(i) >= 0;
            var active = mission.Active == i;
            var card = cards[i];

            card.Root.EnableInClassList("deployed", deployed);
            card.Root.EnableInClassList("active", active);

            // The selected man's card is drawn at full colour; the rest sit back.
            card.Frame.style.unityBackgroundImageTintColor = active ? card.Accent : Fade(card.Accent, 0.4f);

            // The corner number is his place in the squad, so it only exists once he has one.
            card.Badge.style.display = deployed ? DisplayStyle.Flex : DisplayStyle.None;
            card.BadgeNumber.text = mission.DeploySlot(i).ToString("00");

            card.Tag.text = deployed
                ? card.TagHovered ? "UNDEPLOY" : "DEPLOYED"
                : "UNDEPLOYED";

            card.TagBox.EnableInClassList("standby", !deployed);
        }

        deployedCount.text = $"{men} / {max}";

        for (var i = 0; i < pips.childCount; i++)
        {
            pips[i].EnableInClassList("filled", i < men);
        }

        summary.text = notice ?? (men == 0
            ? "NOBODY DEPLOYED YET"
            : men < max
                ? $"{max - men} {(max - men == 1 ? "SLOT" : "SLOTS")} STILL OPEN — OR GO NOW"
                : "SQUAD READY");

        // Nothing to begin with nobody going in.
        go.SetEnabled(men > 0);
    }

    /// <summary>The squad limit turned a deployment down. Says so where the count is.</summary>
    public void Refuse()
    {
        notice = $"SQUAD LIMIT IS {mission.MaxDeployed} — UNDEPLOY SOMEONE FIRST";
        countBlock.AddToClassList("refused");
        Refresh();

        noticeExpiry?.Pause();
        noticeExpiry = countBlock.schedule.Execute(() =>
        {
            notice = null;
            countBlock.RemoveFromClassList("refused");
            Refresh();
        }).StartingIn(1600);
    }

    /// <summary>
    ///     Per frame while the screen is up. The marker under the cursor lights up, so a click or a
    ///     drop shows where it will land before it lands.
    /// </summary>
    public void Tick()
    {
        if (markers == null || pointer == null || !pointer.IsAvailable)
        {
            return;
        }

        var near = pointer.OverUi ? -1 : markers.Nearest(pointer.WorldPosition, dragging ? 1.6f : 1f);
        markers.Hover = near;

        if (!dragging)
        {
            return;
        }

        dropPoint = near;

        var recalling = near < 0 && recruitPanel.worldBound.Contains(lastPointer)
                                 && mission.AssignedPoint(pressed) >= 0;

        ghost.EnableInClassList("on-target", near >= 0);
        ghost.EnableInClassList("recall", recalling);

        ghostTarget.text = near >= 0
            ? $"→ {mission.Points.Label(near)}"
            : recalling ? "UNDEPLOY" : "DROP ON AN ENTRY";
    }

    private void BuildObjectives()
    {
        objectiveList.Clear();

        // Bonus objectives are the star conditions, listed under STAR RATING with the rule that
        // actually scores them. Listing them twice would read as six objectives.
        var shown = 0;

        foreach (var objective in mission.Objectives)
        {
            if (objective.tag == "BONUS")
            {
                continue;
            }

            objectiveList.Add(ObjectiveRow(objective));
            shown++;
        }

        if (shown == 0)
        {
            foreach (var objective in mission.Objectives)
            {
                objectiveList.Add(ObjectiveRow(objective));
            }
        }
    }

    private static VisualElement ObjectiveRow(MissionState.Objective objective)
    {
        var row = new VisualElement();
        row.AddToClassList("objective");

        var tag = Text(objective.tag, "objective-tag");
        tag.EnableInClassList("bonus", objective.tag != "PRIMARY");

        row.Add(tag);
        row.Add(Text(objective.text, "objective-text"));
        return row;
    }

    /// <summary>The three stars, worded from the rules MissionState scores them by.</summary>
    private void BuildStars(string par)
    {
        starList.Clear();
        starList.Add(Star($"Finish inside par time ({par})"));
        starList.Add(Star("No friendly losses"));
        starList.Add(Star("Stay undetected until first contact"));
    }

    private static VisualElement Star(string text)
    {
        var row = new VisualElement();
        row.AddToClassList("star-row");
        row.Add(Text("★", "star-glyph"));
        row.Add(Text(text, "star-text"));
        return row;
    }

    private static VisualElement Intel(string value, string label)
    {
        var cell = new VisualElement();
        cell.AddToClassList("intel-cell");
        cell.Add(Text(value, "intel-value"));
        cell.Add(Text(label, "intel-label"));
        return cell;
    }

    private void BuildCards()
    {
        recruitList.Clear();
        cards.Clear();

        for (var i = 0; i < mission.Roster.Count; i++)
        {
            var op = mission.Roster[i];

            if (op == null)
            {
                continue;
            }

            var card = BuildCard(op, i);
            Draggable(card.Root, i);

            recruitList.Add(card.Root);
            cards.Add(card);
        }
    }

    /// <summary>
    ///     Portrait on the left with his squad number once he has one; name, class and the deploy
    ///     tag across the top; health, armour and range; then the loadout, weapon first. Everything
    ///     coloured comes from the profile, so a new man is a new asset rather than new styling.
    /// </summary>
    private Card BuildCard(Operator op, int index)
    {
        var profile = op.Profile;
        var card = new Card
        {
            Accent = profile != null ? profile.accent : Palette[index % Palette.Length],
            Name = profile != null && !string.IsNullOrEmpty(profile.callsign)
                ? profile.callsign.ToUpperInvariant()
                : Callsign(op.name)
        };

        var accent = card.Accent;
        var weapon = mission.Weapon != null ? mission.Weapon : op.Weapon;
        var health = op.GetComponent<Health>();
        var hp = health != null ? Mathf.CeilToInt(health.Maximum) : 100;
        var armor = profile != null ? profile.armor : 0;
        var range = weapon != null ? Mathf.RoundToInt(weapon.range) : 12;
        var weaponName = weapon != null ? weapon.displayName : op.WeaponName;
        var light = Color.Lerp(accent, Color.white, 0.45f);

        var root = new VisualElement();
        root.AddToClassList("op-card");

        // Portrait column.
        var portrait = Box("op-portrait");

        if (profile != null && profile.portrait != null)
        {
            portrait.style.backgroundImage = new StyleBackground(profile.portrait);
            portrait.style.unityBackgroundImageTintColor = Color.Lerp(accent, Color.white, 0.3f);
        }

        var edge = Box("op-portrait-edge");
        edge.style.unityBackgroundImageTintColor = accent;

        var badge = Box("op-badge");
        badge.style.unityBackgroundImageTintColor = accent;
        var badgeNumber = Text("", "op-badge-number");
        badge.Add(badgeNumber);

        portrait.Add(edge);
        portrait.Add(badge);

        // Body.
        var body = Box("op-body");

        var head = Box("op-head");
        var emblem = Box("op-emblem");
        emblem.style.unityBackgroundImageTintColor = accent;

        var titles = Box("op-titles");
        titles.Add(Text(card.Name, "op-callsign"));
        titles.Add(Text(op.DisplayName, "op-role"));

        // The deploy tag is also the undeploy button. It takes the pointer itself, so pressing it
        // never starts a drag of the card underneath.
        var tagBox = Box("op-tag");
        tagBox.pickingMode = PickingMode.Position;
        SetBorderColor(tagBox, accent);

        var diamond = Box("op-tag-diamond");
        diamond.style.unityBackgroundImageTintColor = accent;
        var tag = Text("UNDEPLOYED", "op-tag-text");
        tag.style.color = accent;
        tagBox.Add(diamond);
        tagBox.Add(tag);

        tagBox.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0)
            {
                return;
            }

            e.StopPropagation();

            if (mission.AssignedPoint(index) >= 0)
            {
                mission.Withdraw(index);
            }
            else
            {
                mission.SetActive(index);
            }
        });

        tagBox.RegisterCallback<PointerEnterEvent>(_ =>
        {
            card.TagHovered = true;
            Refresh();
        });

        tagBox.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            card.TagHovered = false;
            Refresh();
        });

        head.Add(emblem);
        head.Add(titles);
        head.Add(tagBox);

        var stats = Box("op-stats");
        stats.Add(Stat("icon-heart", "HP", hp, hp / HealthScale, accent, light));
        stats.Add(Box("op-divider"));
        stats.Add(Stat("icon-shield", "ARMOR", armor, armor / ArmorScale, accent, light));
        stats.Add(Box("op-divider"));
        stats.Add(Stat("icon-crosshair", "RNG", range, range / RangeScale, accent, light));

        var slots = Box("op-slots");
        slots.Add(Slot(1, weapon != null ? weapon.icon : null, weaponName, accent, true));

        for (var k = 0; k < 4; k++)
        {
            var item = profile != null && profile.items != null && k < profile.items.Length ? profile.items[k] : null;
            slots.Add(Slot(k + 2, item != null ? item.icon : null, item != null ? item.displayName : "", accent, false));
        }

        body.Add(head);
        body.Add(Box("op-rule"));
        body.Add(stats);
        body.Add(Box("op-rule"));
        body.Add(slots);

        // Drawn last, over everything, so the outline and its thick corners are never covered.
        var outline = Box("op-frame");
        outline.style.unityBackgroundImageTintColor = accent;

        root.Add(portrait);
        root.Add(body);
        root.Add(outline);

        card.Root = root;
        card.Frame = outline;
        card.Badge = badge;
        card.BadgeNumber = badgeNumber;
        card.TagBox = tagBox;
        card.Tag = tag;
        return card;
    }

    private static VisualElement Stat(string icon, string label, int value, float fraction, Color accent, Color light)
    {
        var stat = Box("op-stat");

        var glyph = Box("op-stat-icon");
        glyph.AddToClassList(icon);
        glyph.style.unityBackgroundImageTintColor = light;

        var column = Box("op-stat-col");
        column.Add(Text(label, "op-stat-label"));

        var row = Box("op-stat-row");
        row.Add(Text(value.ToString(), "op-stat-value"));

        var bar = Box("op-bar");
        var fill = Box("op-bar-fill");
        fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
        fill.style.backgroundColor = accent;
        bar.Add(fill);
        row.Add(bar);

        column.Add(row);
        stat.Add(glyph);
        stat.Add(column);
        return stat;
    }

    private static VisualElement Slot(int number, Texture2D icon, string label, Color accent, bool weapon)
    {
        var slot = Box("op-slot");
        slot.EnableInClassList("wide", weapon);

        var picture = Box("op-slot-icon");

        if (icon != null)
        {
            picture.style.backgroundImage = new StyleBackground(icon);
        }
        else if (!string.IsNullOrEmpty(label))
        {
            // No picture yet: the name keeps the slot meaningful rather than blank.
            picture.Add(Text(label, "op-slot-name"));
        }

        var outline = Box("op-slot-frame");
        outline.style.unityBackgroundImageTintColor = Fade(accent, 0.75f);

        var tab = Box("op-slot-tab");
        tab.style.unityBackgroundImageTintColor = accent;
        tab.Add(Text(number.ToString(), "op-slot-number"));

        slot.Add(picture);
        slot.Add(outline);
        slot.Add(tab);

        if (weapon)
        {
            var ammo = Box("op-slot-ammo");
            ammo.style.unityBackgroundImageTintColor = accent;
            slot.Add(ammo);
        }

        return slot;
    }

    /// <summary>
    ///     Press, travel past the threshold and it is a drag; release without travelling and it is a
    ///     click. The card holds the pointer for the whole gesture, so the drag survives the cursor
    ///     leaving the card, the panel and the interface altogether.
    /// </summary>
    private void Draggable(VisualElement card, int index)
    {
        card.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0)
            {
                return;
            }

            pressed = index;
            dragging = false;
            pressedAt = e.position;
            lastPointer = e.position;
            card.CapturePointer(e.pointerId);
        });

        card.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (pressed != index || !card.HasPointerCapture(e.pointerId))
            {
                return;
            }

            lastPointer = e.position;

            if (!dragging && (lastPointer - pressedAt).sqrMagnitude > DragThreshold * DragThreshold)
            {
                BeginDrag(index);
            }

            if (dragging)
            {
                var at = layer.WorldToLocal(lastPointer);
                ghost.style.left = at.x + 14f;
                ghost.style.top = at.y + 8f;
            }
        });

        card.RegisterCallback<PointerUpEvent>(e =>
        {
            if (pressed != index)
            {
                return;
            }

            lastPointer = e.position;

            var wasDrag = dragging;
            var target = dropPoint;
            var onPanel = recruitPanel.worldBound.Contains(lastPointer);

            EndDrag();

            // Released before acting: the capture-out it raises would otherwise land mid-change.
            if (card.HasPointerCapture(e.pointerId))
            {
                card.ReleasePointer(e.pointerId);
            }

            if (!wasDrag)
            {
                mission.SetActive(index);
            }
            else if (target >= 0)
            {
                mission.Assign(index, target);
            }
            else if (onPanel && mission.AssignedPoint(index) >= 0)
            {
                mission.Withdraw(index);
            }
        });

        // Focus lost, window left, anything that takes the pointer away mid-drag: drop nothing.
        card.RegisterCallback<PointerCaptureOutEvent>(_ =>
        {
            if (pressed == index)
            {
                EndDrag();
            }
        });
    }

    private void BeginDrag(int index)
    {
        dragging = true;
        dropPoint = -1;

        var card = cards[index];
        ghostBadge.text = card.Name;
        ghostBadge.style.color = card.Accent;
        ghostTarget.text = "DROP ON AN ENTRY";
        ghost.style.display = DisplayStyle.Flex;
        ghost.BringToFront();

        mission.SetActive(index);
        card.Root.AddToClassList("dragging");
    }

    private void EndDrag()
    {
        if (pressed >= 0 && pressed < cards.Count)
        {
            cards[pressed].Root.RemoveFromClassList("dragging");
        }

        pressed = -1;
        dragging = false;
        dropPoint = -1;

        if (ghost != null)
        {
            ghost.style.display = DisplayStyle.None;
            ghost.RemoveFromClassList("on-target");
            ghost.RemoveFromClassList("recall");
        }

        if (markers != null)
        {
            markers.Hover = -1;
        }
    }

    /// <summary>Opens or folds the brief. The map is deliberately left where it is.</summary>
    private void ToggleBrief()
    {
        briefOpen = !briefOpen;
        ApplyBrief();
    }

    private void ApplyBrief()
    {
        briefPanel.style.display = briefOpen ? DisplayStyle.Flex : DisplayStyle.None;
        briefToggle.EnableInClassList("open", briefOpen);
    }

    /// <summary>
    ///     Frames the building into the screen left of the recruit panel, ignoring the brief. Only
    ///     when that space has actually changed, so panning and zooming the map is not undone by a
    ///     stray layout pass.
    /// </summary>
    private void Reframe()
    {
        if (frame == null || layer.resolvedStyle.display == DisplayStyle.None)
        {
            return;
        }

        var total = layer.worldBound;
        var right = recruitPanel.worldBound;

        if (total.width <= 1f || total.height <= 1f || right.width <= 1f || float.IsNaN(right.xMin))
        {
            return;
        }

        var x0 = Mathf.Clamp01(FramePadding / total.width);
        var x1 = Mathf.Clamp01((right.xMin - FramePadding - total.xMin) / total.width);
        var y = FramePadding / total.height;

        if (x1 - x0 < 0.1f)
        {
            return;
        }

        var area = new Rect(x0, y, x1 - x0, 1f - y * 2f);

        if (area == lastFrame)
        {
            return;
        }

        lastFrame = area;
        frame(area);
    }

    private static Label Text(string text, string className)
    {
        var label = new Label(text) { pickingMode = PickingMode.Ignore };
        label.AddToClassList(className);
        return label;
    }

    private static VisualElement Box(string className)
    {
        var box = new VisualElement { pickingMode = PickingMode.Ignore };
        box.AddToClassList(className);
        return box;
    }

    private static void SetBorderColor(VisualElement element, Color color)
    {
        element.style.borderLeftColor = color;
        element.style.borderRightColor = color;
        element.style.borderTopColor = color;
        element.style.borderBottomColor = color;
    }

    private static Color Fade(Color color, float alpha)
    {
        return new Color(color.r, color.g, color.b, color.a * alpha);
    }

    /// <summary>"Operator_01" → "OPERATOR".</summary>
    private static string Callsign(string objectName)
    {
        return objectName.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '_', ' ')
            .Replace('_', ' ')
            .ToUpperInvariant();
    }

    private static string Clock(float seconds)
    {
        var whole = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{whole / 60}:{whole % 60:00}";
    }
}
