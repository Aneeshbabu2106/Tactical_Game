using System;
using UnityEngine;

/// <summary>
///     Hit points for anything that can be shot. Dying leaves the body in place and dimmed rather
///     than removing it — a cleared room should look cleared.
/// </summary>
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

    private void Awake()
    {
        Current = spec != null ? spec.maxHealth : maximum;
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f)
        {
            return;
        }

        Current = Mathf.Max(0f, Current - amount);

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
