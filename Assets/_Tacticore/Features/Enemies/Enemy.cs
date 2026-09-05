using UnityEngine;

/// <summary>
///     Scene adapter for one enemy: reads its numbers from an <see cref="EnemySpec" />, runs an
///     <see cref="OperatorMotor" /> for movement and facing, and mirrors the result onto the
///     transform. The opposition's answer to <see cref="Operator" />, and deliberately the same
///     shape — the motor does not care which side it is driving.
/// </summary>
/// <remarks>
///     Holds no behaviour. What he does is <see cref="EnemyBrain" />; what he shoots at is
///     <see cref="EnemyCombat" />. This is the body.
///     <para>
///         Runs early so the weapon placeholder exists before <see cref="Spottable" /> collects the
///         renderers it hides. Built afterwards it would be missed, and the gun would hang in the
///         air where an unseen man is standing.
///     </para>
/// </remarks>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class Enemy : MonoBehaviour
{
    [SerializeField] private EnemySpec spec;

    [Tooltip("The way he faces on his post, in degrees counter-clockwise from +X. He drifts back to "
             + "this when there is nothing else to look at and no route to walk.")]
    [SerializeField] private float homeFacing = -90f;

    private static Sprite generatedSprite;

    private OperatorMotor motor;
    private Transform rig;
    private Health health;

    public EnemySpec Spec => spec;

    public OperatorMotor Motor => motor;

    public float HomeFacing => homeFacing;

    /// <summary>Set by the brain. Everything about him gets faster as this rises.</summary>
    public AlertLevel Alert { get; set; }

    public float FacingDegrees => motor?.FacingDegrees ?? homeFacing;

    /// <summary>
    ///     The prototype's canAct: a downed man does not think, walk, turn or shoot. Without this he
    ///     keeps working the pipeline from the floor, which is both wrong and hard to notice —
    ///     nothing on screen says the corpse is still aiming.
    /// </summary>
    public bool IsAlive => health == null || health.IsAlive;

    /// <summary>Widens once he has seen someone, as the prototype's alerted vision override does.</summary>
    public float VisionRange =>
        Alert >= AlertLevel.Alerted ? spec.Alert.alertedRange : spec.Vision.range;

    public float VisionFov =>
        Alert >= AlertLevel.Alerted ? spec.Alert.alertedFov : spec.Vision.fovDegrees;

    public float VisionNearRadius => spec.Vision.nearRadius;

    private void Awake()
    {
        if (spec == null)
        {
            Debug.LogError($"{name}: no EnemySpec assigned. Disabling.", this);
            enabled = false;
            return;
        }

        health = GetComponent<Health>();

        motor = new OperatorMotor(transform.position, homeFacing)
        {
            WalkSpeed = spec.moveSpeed,
            RunSpeed = spec.moveSpeed,
            TurnRate = spec.Alert.TurnRate(AlertLevel.Unaware)
        };

        BuildRig();
    }

    private void Update()
    {
        if (motor == null)
        {
            return;
        }

        if (!IsAlive)
        {
            motor.ClearPath();
            return;
        }

        // Turning speed is a function of how alert he is, which is most of what being alert means.
        motor.TurnRate = spec.Alert.TurnRate(Alert);
        motor.Tick(SimClock.DeltaTime);

        transform.position = motor.Position;

        if (rig != null)
        {
            rig.localRotation = Quaternion.Euler(0f, 0f, motor.FacingDegrees);
        }
    }

    /// <summary>Sends him along a route. Points are world space, as the pathfinder returns them.</summary>
    public void Walk(System.Collections.Generic.IReadOnlyList<Vector3> points)
    {
        // No turn threshold and no max spacing: waypoints are the player's instrument for giving
        // orders, and an enemy has nobody to give him one.
        motor.SetPath(points, 360f, float.MaxValue, 0f);
    }

    public void Stop()
    {
        motor.ClearPath();
    }

    public bool IsWalking => motor != null && motor.IsMoving;

    /// <summary>A point to keep looking at, or null to hand facing back to where he is walking.</summary>
    public void Look(Vector3? at)
    {
        motor.LookTarget = at;
    }

    /// <summary>
    ///     A child carrying everything directional, so the placeholder disc stays put while the gun
    ///     swings round. Same arrangement as the operator's rig.
    /// </summary>
    private void BuildRig()
    {
        var host = new GameObject("Rig");
        host.transform.SetParent(transform, false);
        rig = host.transform;

        var gun = new GameObject("Gun");
        gun.transform.SetParent(rig, false);
        gun.transform.localPosition = spec.gunOffset;
        gun.transform.localScale = new Vector3(spec.gunSize.x, spec.gunSize.y, 1f);

        var renderer = gun.AddComponent<SpriteRenderer>();
        renderer.sprite = GeneratedSprite();
        renderer.color = spec.gunColor;
        renderer.sortingOrder = 92;
    }

    /// <summary>A white pixel, stretched into a bar by the transform. Shared, and never saved.</summary>
    private static Sprite GeneratedSprite()
    {
        if (generatedSprite != null)
        {
            return generatedSprite;
        }

        var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        generatedSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0f, 0.5f), 1f);
        generatedSprite.hideFlags = HideFlags.HideAndDontSave;
        return generatedSprite;
    }
}
