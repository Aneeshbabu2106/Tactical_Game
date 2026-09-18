using System;

/// <summary>
///     One line of the context menu: what it says, whether it can be pressed, and what pressing it
///     does. A data row rather than a method, the same shape <see cref="OpeningVerb" /> uses.
/// </summary>
public readonly struct ActionRow
{
    public ActionRow(string label, bool enabled, Action action)
    {
        Label = label;
        Enabled = enabled;
        Action = action;
    }

    public string Label { get; }

    public bool Enabled { get; }

    public Action Action { get; }
}
