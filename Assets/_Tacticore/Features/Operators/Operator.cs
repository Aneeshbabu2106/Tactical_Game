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
    private readonly List<SpriteRenderer> waypointRings = new();
    private readonly List<SpriteRenderer> waypointFacings = new();
    private Transform dotRoot;
    private Transform waypointRoot;
    private LineRenderer actionBar;
    private LineRenderer actionBarTrack;

    public OperatorMotor Motor => motor;

    public float FacingDegrees => motor?.FacingDegrees ?? 0f;

    public Vector2 Forward => motor?.Forward ?? Vector2.right;

    public float PickRadius => spec != null ? spec.Planning.operatorPickRadius : 0.45f;

    public float WaypointPickRadius => spec != null ? spec.Planning.waypointPickRadius : 0.32f;

    public float PathStartClearance => spec != null ? spec.Planning.pathStartClearance : 0.55f;

    public float PathHoverThreshold => spec != null ? spec.Planning.pathHoverThreshold : 0.35f;

    public float OpeningReach => spec != null ? spec.openingReach : 0.9f;

    public float VisionFov => spec != null ? spec.Vision.fovDegrees : 120f;

    public float WeaponDamage => spec != null ? spec.Weapon.damage : 34f;

    public float RoundsPerMinute => spec != null ? spec.Weapon.roundsPerMinute : 750f;

    public int MagazineSize => spec != null ? spec.Weapon.magazineSize : 30;

    public float ReloadSeconds => spec != null ? spec.Weapon.reloadSeconds : 2f;

    public float WeaponRange => spec != null ? spec.Weapon.range : 12f;

    public float WeaponAccuracy => spec != null ? spec.Weapon.accuracy : 0.66f;

    public float VisionRange => spec != null ? spec.Vision.range : 12f;

    public float VisionNearRadius => spec != null ? spec.Vision.nearRadius : 1.6f;

    public float VisionStepDegrees => spec != null ? spec.Vision.stepDegrees : 2f;

    public Color ConeColor => spec != null ? spec.coneColor : new Color(1f, 0.863f, 0.651f, 0.30f);

    /// <summary>
    ///     Whether this operator is the one the player is currently working with. Only the selected
    ///     operator draws a view cone — the prototype's rule, so a squad does not become a mess of
    ///     overlapping wedges.
    /// </summary>
    public bool IsSelected { get; private set; }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
    }

    public float WaypointMarkerSize => spec != null ? spec.Style.waypointMarkerSize : 0.3f;

    public Color PathColor => spec != null ? spec.pathColor : Color.cyan;

    public bool IsMoving => motor != null && motor.IsMoving;

    /// <summary>Index of the point being walked towards; everything before it is already behind.</summary>
    public int NextIndex => motor?.NextIndex ?? 0;

    /// <summary>True while stopped working on a waypoint action, such as opening a door.</summary>
    public bool IsBusy => motor != null && motor.IsBusy;

    /// <summary>Pace toggle. Applies immediately, mid-path included.</summary>
    public bool IsRunning { get; private set; }

    public void SetRunning(bool running)
    {
        IsRunning = running;

        if (motor != null)
        {
            motor.Running = running;
        }
    }

    public void ToggleRunning()
    {
        SetRunning(!IsRunning);
    }

    [Tooltip("How he handles contact when the mission starts. Changed in play from his menu.")]
    [SerializeField] private EngagementMode engagement = EngagementMode.KeepMoving;

    /// <summary>How he handles contact. The order given before he goes in.</summary>
    public EngagementMode Engagement => engagement;

    public void SetEngagement(EngagementMode mode)
    {
        engagement = mode;

        // A mode that cannot hold him must not leave him held.
        if (mode != EngagementMode.WaitToClear)
        {
            Hold(false);
        }
    }

    public void CycleEngagement()
    {
        SetEngagement(Engagement switch
        {
            EngagementMode.KeepMoving => EngagementMode.WaitToClear,
            EngagementMode.WaitToClear => EngagementMode.HoldFire,
            _ => EngagementMode.KeepMoving
        });
    }

    /// <summary>Stops him advancing without losing the route.</summary>
    public void Hold(bool holding)
    {
        if (motor != null)
        {
            motor.Holding = holding;
        }
    }

    public bool IsHolding => motor is { Holding: true };

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
            WalkSpeed = spec.walkSpeed,
            RunSpeed = spec.runSpeed,
            TurnRate = spec.turnRate
        };

        EnsurePathLine();
        EnsureLookMarker();
        EnsureActionBar();
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
        RedrawActionBar();
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

    /// <summary>The route and its waypoints, edited in place by the waypoint tools.</summary>
    public PathPlan Plan => motor?.Plan;

    public void SetPath(List<Vector3> points)
    {
        motor.MoveTo(transform.position);
        motor.SetPath(points, spec.Planning.waypointTurnThreshold, spec.Planning.waypointMinSpacing, spec.Planning.waypointMaxSpacing);
        RedrawPath();
    }

    /// <summary>Call after editing the plan from outside, so the drawn path catches up this frame.</summary>
    public void PathChanged()
    {
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
            Hide(waypointRings, 0);
            Hide(waypointFacings, 0);
            return;
        }

        // Start from where the operator actually is, so the curve stays attached as it walks.
        pathBuffer.Clear();
        pathBuffer.Add(transform.position);
        pathBuffer.AddRange(motor.Remaining);

        PathSmoothing.Smooth(pathBuffer, smoothBuffer, spec.Planning.pathSmoothing);

        pathLine.positionCount = smoothBuffer.Count;

        for (var i = 0; i < smoothBuffer.Count; i++)
        {
            pathLine.SetPosition(i, smoothBuffer[i]);
        }

        RedrawDots(smoothBuffer);
        RedrawWaypoints();
    }

    /// <summary>
    ///     Rings for waypoints still ahead, with an arrowhead on any that carries a facing. Passed
    ///     waypoints are hidden along with the path behind the operator.
    /// </summary>
    private void RedrawWaypoints()
    {
        var rings = 0;
        var facings = 0;
        var plan = motor.Plan;

        foreach (var waypoint in plan.Waypoints)
        {
            if (waypoint.PointIndex < motor.NextIndex || waypoint.PointIndex >= plan.Points.Count)
            {
                continue;
            }

            var at = plan.Points[waypoint.PointIndex];

            var ring = Pooled(waypointRings, rings++, waypointRoot, LineArt.Ring(), 104);

            // Outstanding work outranks pace: it is the reason this waypoint exists.
            ring.color = waypoint.HasPendingAction ? spec.Style.waypointActionColor
                : waypoint.Run ? spec.Style.waypointRunColor
                : spec.pathColor;
            ring.transform.position = at;
            ring.transform.localScale = Vector3.one * spec.Style.waypointMarkerSize;

            if (waypoint.FacingDegrees == null)
            {
                continue;
            }

            var angle = waypoint.FacingDegrees.Value;
            var tick = Pooled(waypointFacings, facings++, waypointRoot, LineArt.Arrow(), 105);
            tick.color = spec.Style.lookMarkerColor;
            tick.transform.position =
                at + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f)
                * spec.Style.waypointMarkerSize;
            tick.transform.localScale = Vector3.one * spec.Style.waypointMarkerSize * 0.8f;
            tick.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        Hide(waypointRings, rings);
        Hide(waypointFacings, facings);
    }

    private static SpriteRenderer Pooled(
        List<SpriteRenderer> pool, int index, Transform parent, Sprite sprite, int sortingOrder)
    {
        while (pool.Count <= index)
        {
            var host = new GameObject("Marker");
            host.transform.SetParent(parent, false);

            var created = host.AddComponent<SpriteRenderer>();
            created.sprite = sprite;
            created.sortingOrder = sortingOrder;
            pool.Add(created);
        }

        var renderer = pool[index];
        renderer.enabled = true;
        return renderer;
    }

    private static void Hide(List<SpriteRenderer> pool, int from)
    {
        for (var i = from; i < pool.Count; i++)
        {
            pool[i].enabled = false;
        }
    }

    /// <summary>
    ///     Marks the path at a fixed distance cadence, the way the prototype dots each waypoint.
    ///     Ours is a freehand stroke with no waypoints of its own, so the spacing supplies the rhythm.
    ///     Renderers are pooled and hidden rather than destroyed; a walking path redraws every frame.
    /// </summary>
    private void RedrawDots(List<Vector3> curve)
    {
        var used = 0;

        if (curve != null && curve.Count > 1 && spec.Style.pathMarkerSpacing > 0f)
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
                var travelled = spec.Style.pathMarkerSpacing - carried;
                var heading = (to - from) / segment;

                while (travelled <= segment)
                {
                    PlaceMarker(used++, Vector3.Lerp(from, to, travelled / segment), heading);
                    travelled += spec.Style.pathMarkerSpacing;
                }

                carried = segment - (travelled - spec.Style.pathMarkerSpacing);
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
        marker.transform.localScale = Vector3.one * spec.Style.pathMarkerSize;

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
        var arm = spec.Style.lookMarkerSize;

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

    /// <summary>
    ///     A bar under the operator while it works, so a man standing still at a door reads as busy
    ///     rather than stuck. Hidden the rest of the time.
    /// </summary>
    private void RedrawActionBar()
    {
        var busy = motor.IsBusy;

        actionBar.enabled = busy;
        actionBarTrack.enabled = busy;

        if (!busy)
        {
            return;
        }

        var half = spec.Style.actionBarWidth * 0.5f;
        var origin = transform.position + new Vector3(spec.Style.actionBarOffset.x, spec.Style.actionBarOffset.y, 0f);
        var left = origin + Vector3.left * half;

        actionBarTrack.SetPosition(0, left);
        actionBarTrack.SetPosition(1, origin + Vector3.right * half);

        actionBar.SetPosition(0, left);
        actionBar.SetPosition(1, left + Vector3.right * spec.Style.actionBarWidth * motor.ActionProgress);
    }

    private void EnsureActionBar()
    {
        if (actionBar != null)
        {
            return;
        }

        actionBarTrack = BuildBar("ActionBarTrack", spec.Style.actionBarTrackColor, 0.09f, 107);
        actionBar = BuildBar("ActionBar", spec.Style.actionBarColor, 0.07f, 108);
    }

    private LineRenderer BuildBar(string barName, Color color, float width, int sortingOrder)
    {
        var host = new GameObject(barName);
        host.transform.SetParent(transform, false);

        var renderer = host.AddComponent<LineRenderer>();
        renderer.useWorldSpace = true;
        renderer.widthMultiplier = width;
        renderer.positionCount = 2;
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.startColor = color;
        renderer.endColor = color;
        renderer.sortingOrder = sortingOrder;
        renderer.enabled = false;
        return renderer;
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
        lookMarker.startColor = spec.Style.lookMarkerColor;
        lookMarker.endColor = spec.Style.lookMarkerColor;
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
        lookRenderer.SetPosition(1, new Vector3(spec.Style.lookLength, 0f, 0f));
        lookRenderer.widthMultiplier = 0.05f;
        lookRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lookRenderer.startColor = spec.Style.lookColor;
        lookRenderer.endColor = spec.Style.lookColor;
        lookRenderer.sortingOrder = 101;

        var gun = new GameObject("Gun");
        gun.transform.SetParent(rig, false);
        gun.transform.localPosition = spec.Style.gunOffset;
        gun.transform.localScale = new Vector3(spec.Style.gunSize.x, spec.Style.gunSize.y, 1f);

        var gunRenderer = gun.AddComponent<SpriteRenderer>();
        gunRenderer.sprite = spec.Style.gunSprite != null ? spec.Style.gunSprite : GeneratedSprite();
        gunRenderer.color = spec.Style.gunColor;
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

        waypointRoot = new GameObject("Waypoints").transform;
        waypointRoot.SetParent(transform, false);
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
