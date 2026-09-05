using System;
using UnityEngine;

/// <summary>
///     Hit points for anything that can be shot. Dying leaves the body in place and dimmed rather
///     than removing it — a cleared room should look cleared.
/// </summary>
/// <remarks>
///     Lives beside the operator rather than in Combat because both sides need it and Combat sits
///     above them: an operator that could not ask whether he is still alive would need Operators to
///     reference Combat, which already references Operators. This is the actor layer, not the
///     player's half of it.
/// </remarks>
[DisallowMultipleComponent]
public class Health : MonoBehaviour
{
    [Tooltip("Where the numbers come from. Left empty, the inline fallbacks below are used.")]
    [SerializeField] private EnemySpec spec;

    [SerializeField] private float maximum = 100f;

    [Tooltip("Colour the body is tinted once it goes down.")]
    [SerializeField] private Color downedTint = new(0.32f, 0.16f, 0.18f, 1f);

    private SpriteRenderer[] sprites;

    public float Current { get; private set; }

    public bool IsAlive => Current > 0f;

    /// <summary>Raised once, on the hit that puts this down.</summary>
    public event Action<Health> Died;

    /// <summary>
    ///     Raised on every hit that lands, carrying whoever fired it. Being shot is the loudest
    ///     information there is — without this an enemy can be hit and carry on unaware, which is
    ///     the single biggest thing that made them harmless.
    /// </summary>
    public event Action<Health, GameObject> Damaged;

    private void Awake()
    {
        Current = spec != null ? spec.maxHealth : maximum;
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, null);
    }

    public void TakeDamage(float amount, GameObject from)
    {
        if (!IsAlive || amount <= 0f)
        {
            return;
        }

        // Armour is the type's, so a heavy soaks what drops a thug. Nothing about the shot changes.
        Current = Mathf.Max(0f, Current - (spec != null ? spec.Absorb(amount) : amount));

        Damaged?.Invoke(this, from);

        if (IsAlive)
        {
            return;
        }

        Dim();
        Died?.Invoke(this);
    }

    /// <summary>
    ///     Collected on death rather than in Awake: the placeholder marker is built at runtime, so
    ///     it does not exist yet when this wakes.
    /// </summary>
    private void Dim()
    {
        sprites ??= GetComponentsInChildren<SpriteRenderer>(true);

        foreach (var sprite in sprites)
        {
            if (sprite != null)
            {
                sprite.color = spec != null ? spec.downedTint : downedTint;
            }
        }
    }
}
