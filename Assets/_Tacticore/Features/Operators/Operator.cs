using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Scene adapter for one operator: reads its numbers from an <see cref="OperatorSpec" />, runs an
///     <see cref="OperatorMotor" /> for the actual movement, and mirrors the result onto the transform
///     and the visuals. Deliberately holds no movement rules of its own.
/// </summary>
[DisallowMultipleComponent]
public class Operator : MonoBehaviour
{
    [SerializeField] private OperatorSpec spec;
    [SerializeField] private LineRenderer pathLine;
    [SerializeField] private LineRenderer lookMarker;

    private static Sprite generatedSprite;

    private OperatorMotor motor;
    private Transform rig;

    // Reused every frame; smoothing a path per frame would otherwise churn the heap.
    private readonly List<Vector3> pathBuffer = new();
    private readonly List<Vector3> smoothBuffer = new();
    private readonly List<SpriteRenderer> dots = new();
    private Transform dotRoot;

    public OperatorMotor Motor => motor;

    public float FacingDegrees => motor?.FacingDegrees ?? 0f;

    public Vector2 Forward => motor?.Forward ?? Vector2.right;

    public float PickRadius => spec != null ? spec.pickRadius : 0.45f;

    public bool IsMoving => motor != null && motor.IsMoving;

    /// <summary>Pace toggle. Applies immediately, mid-path included.</summary>
    public bool IsRunning { get; private set; }

    public void SetRunning(bool running)
    {
        IsRunning = running;

        if (motor != null && spec != null)
        {
            motor.MoveSpeed = running ? spec.runSpeed : spec.walkSpeed;
        }
    }

    public void ToggleRunning()
    {
        SetRunning(!IsRunning);
    }

    private void Awake()
    {
        if (spec == null)
        {
            // Loud rather than silently limping along on defaults that look like a tuning bug.
            Debug.LogError($"{name}: no OperatorSpec assigned. Disabling.", this);
            enabled = false;
            return;
        }

        motor = new OperatorMotor(transform.position)
        {
            MoveSpeed = spec.walkSpeed,
            TurnRate = spec.turnRate
        };

        EnsurePathLine();
        EnsureLookMarker();
        EnsureRig();
        ApplyMotorState();
    }

    private void Update()
    {
        // SimClock, not Time: paused feeds zero so the operator holds while the player plans.
        motor.Tick(SimClock.DeltaTime);
        ApplyMotorState();
        RedrawPath();
        RedrawLookMarker();
    }

    /// <summary>
    ///     Sets a point to keep looking at, or null to hand the facing back to the direction of travel.
    /// </summary>
    public void SetLookTarget(Vector3? target)
    {
        motor.LookTarget = target;
        RedrawLookMarker();
    }

    public Vector3? LookTarget => motor?.LookTarget;

    public void SetPath(List<Vector3> points)
    {
        motor.MoveTo(transform.position);
        motor.SetPath(points);
        RedrawPath();
    }

    public void ClearPath()
    {
        motor.ClearPath();
        RedrawPath();
    }

    private void ApplyMotorState()
    {
        transform.position = motor.Position;

        if (rig != null)
        {
            rig.localRotation = Quaternion.Euler(0f, 0f, motor.FacingDegrees);
        }
    }

    /// <summary>Draws what is left to walk, starting from where the operator actually is.</summary>
    private void RedrawPath()
    {
        if (pathLine == null)
        {
            return;
        }

        if (motor.RemainingCount == 0)
        {
            pathLine.positionCount = 0;
            RedrawDots(null);
            return;
        }

        // Start from where the operator actually is, so the curve stays attached as it walks.
        pathBuffer.Clear();
        pathBuffer.Add(transform.position);
        pathBuffer.AddRange(motor.Remaining);

        PathSmoothing.Smooth(pathBuffer, smoothBuffer, spec.pathSmoothing);

        pathLine.positionCount = smoothBuffer.Count;

        for (var i = 0; i < smoothBuffer.Count; i++)
        {
            pathLine.SetPosition(i, smoothBuffer[i]);
        }

        RedrawDots(smoothBuffer);
    }

    /// <summary>
    ///     Marks the path at a fixed distance cadence, the way the prototype dots each waypoint.
    ///     Ours is a freehand stroke with no waypoints of its own, so the spacing supplies the rhythm.
    ///     Renderers are pooled and hidden rather than destroyed; a walking path redraws every frame.
    /// </summary>
    private void RedrawDots(List<Vector3> curve)
    {
        var used = 0;

        if (curve != null && curve.Count > 1 && spec.pathMarkerSpacing > 0f)
        {
            var carried = 0f;

            for (var i = 1; i < curve.Count; i++)
            {
                var from = curve[i - 1];
                var to = curve[i];
                var segment = Vector3.Distance(from, to);

                if (segment <= 0f)
                {
                    continue;
                }

                // Carry the remainder across segments so spacing stays even, not per-segment.
                var travelled = spec.pathMarkerSpacing - carried;
                var heading = (to - from) / segment;

                while (travelled <= segment)
                {
                    PlaceMarker(used++, Vector3.Lerp(from, to, travelled / segment), heading);
                    travelled += spec.pathMarkerSpacing;
                }

                carried = segment - (travelled - spec.pathMarkerSpacing);
            }
        }

        for (var i = used; i < dots.Count; i++)
        {
            dots[i].enabled = false;
        }
    }

    private void PlaceMarker(int index, Vector3 position, Vector3 heading)
    {
        while (dots.Count <= index)
        {
            var host = new GameObject("Arrow");
            host.transform.SetParent(dotRoot, false);

            var renderer = host.AddComponent<SpriteRenderer>();
            renderer.sprite = LineArt.Arrow();
            renderer.sortingOrder = 100;
            dots.Add(renderer);
        }

        var marker = dots[index];
        marker.enabled = true;
        marker.color = spec.pathColor;
        marker.transform.position = position;
        marker.transform.localScale = Vector3.one * spec.pathMarkerSize;

        // The sprite points along +X, so align its right axis with the direction of travel.
        marker.transform.rotation =
            Quaternion.Euler(0f, 0f, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
    }

    /// <summary>
    ///     Draws a small cross at the look target, plus a leader from the operator so it is obvious
    ///     which operator is aiming at it.
    /// </summary>
    private void RedrawLookMarker()
    {
        if (lookMarker == null)
        {
            return;
        }

        if (!motor.LookTarget.HasValue)
        {
            lookMarker.positionCount = 0;
            return;
        }

        var target = motor.LookTarget.Value;
        var arm = spec.lookMarkerSize;

        // One polyline: leader in, then a cross drawn by doubling back through the centre.
        lookMarker.positionCount = 7;
        lookMarker.SetPosition(0, transform.position);
        lookMarker.SetPosition(1, target + new Vector3(-arm, -arm, 0f));
        lookMarker.SetPosition(2, target + new Vector3(arm, arm, 0f));
        lookMarker.SetPosition(3, target);
        lookMarker.SetPosition(4, target + new Vector3(-arm, arm, 0f));
        lookMarker.SetPosition(5, target + new Vector3(arm, -arm, 0f));
        lookMarker.SetPosition(6, target);
    }

    private void EnsureLookMarker()
    {
        if (lookMarker != null)
        {
            return;
        }

        var host = new GameObject("LookMarker");
        host.transform.SetParent(transform, false);

        lookMarker = host.AddComponent<LineRenderer>();
        lookMarker.useWorldSpace = true;
        lookMarker.widthMultiplier = 0.045f;
        lookMarker.positionCount = 0;
        lookMarker.material = new Material(Shader.Find("Sprites/Default"));
        lookMarker.startColor = spec.lookMarkerColor;
        lookMarker.endColor = spec.lookMarkerColor;
        lookMarker.sortingOrder = 103;
    }

    /// <summary>
    ///     A child that carries everything directional, so the operator's own sprite stays upright.
    /// </summary>
    private void EnsureRig()
    {
        if (rig != null)
        {
            return;
        }

        var host = new GameObject("Rig");
        host.transform.SetParent(transform, false);
        rig = host.transform;

        var look = new GameObject("LookIndicator");
        look.transform.SetParent(rig, false);

        // Local space, so the line turns with the rig instead of being pinned to the world.
        var lookRenderer = look.AddComponent<LineRenderer>();
        lookRenderer.useWorldSpace = false;
        lookRenderer.positionCount = 2;
        lookRenderer.SetPosition(0, Vector3.zero);
        lookRenderer.SetPosition(1, new Vector3(spec.lookLength, 0f, 0f));
        lookRenderer.widthMultiplier = 0.05f;
        lookRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lookRenderer.startColor = spec.lookColor;
        lookRenderer.endColor = spec.lookColor;
        lookRenderer.sortingOrder = 101;

        var gun = new GameObject("Gun");
        gun.transform.SetParent(rig, false);
        gun.transform.localPosition = spec.gunOffset;
        gun.transform.localScale = new Vector3(spec.gunSize.x, spec.gunSize.y, 1f);

        var gunRenderer = gun.AddComponent<SpriteRenderer>();
        gunRenderer.sprite = spec.gunSprite != null ? spec.gunSprite : GeneratedSprite();
        gunRenderer.color = spec.gunColor;
        gunRenderer.sortingOrder = 102;
    }

    private void EnsurePathLine()
    {
        if (pathLine != null)
        {
            return;
        }

        var host = new GameObject("PathLine");
        host.transform.SetParent(transform, false);

        pathLine = host.AddComponent<LineRenderer>();
        pathLine.useWorldSpace = true;
        pathLine.widthMultiplier = 0.08f;
        pathLine.numCapVertices = 4;
        pathLine.positionCount = 0;
        pathLine.material = new Material(Shader.Find("Sprites/Default"));
        pathLine.startColor = spec.pathColor;
        pathLine.endColor = spec.pathColor;
        pathLine.sortingOrder = 100;

        dotRoot = new GameObject("PathArrows").transform;
        dotRoot.SetParent(transform, false);
    }

    /// <summary>
    ///     One shared 1x1 white sprite for every operator without art yet. Pivoted at its left edge
    ///     so scaling x lengthens the barrel forward instead of growing it in both directions.
    /// </summary>
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
