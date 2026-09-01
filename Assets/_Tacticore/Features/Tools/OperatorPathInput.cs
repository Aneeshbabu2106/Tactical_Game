using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
///     Left-press an operator and drag to draw a path; release to send it walking. Reads the
///     pointer from <see cref="PointerInput" /> rather than a device, so this stays gameplay logic.
/// </summary>
public class OperatorPathInput : MonoBehaviour
{
    [SerializeField] private PointerInput pointer;
    [SerializeField] private PointerRouter router;
    [SerializeField] private Tilemap navigation;

    [Tooltip("Distance the cursor must travel before another point is added to the stroke.")]
    [SerializeField] private float sampleSpacing = 0.0875f;

    [Tooltip("Spacing of the walkability samples taken along each new segment.")]
    [SerializeField] private float clearanceStep = 0.2f;

    [Tooltip("A press that moves less than this is a click, not a path.")]
    [SerializeField] private float clickThreshold = 0.25f;

    [SerializeField] private Color strokeColor = Color.white;

    [Tooltip("Dash cycles per world unit along the stroke.")]
    [SerializeField] private float dashDensity = 4f;

    [Tooltip("Curve samples per drawn segment. 1 draws the raw polyline.")]
    [Range(1, 12)]
    [SerializeField] private int strokeSmoothing = 6;
    [SerializeField] private LineRenderer strokeLine;

    /// <summary>Raised on a left-click that was not a drag. The action menu listens for this.</summary>
    public event Action<Operator> Clicked;

    private readonly List<Vector3> stroke = new();
    private readonly List<Vector3> smoothBuffer = new();
    private Operator drawing;
    private float strokeZ;
    private Vector3 pressedAt;
    private bool dragged;

    private void Awake()
    {
        if (pointer == null)
        {
            pointer = FindFirstObjectByType<PointerInput>();
        }

        if (router == null)
        {
            router = FindFirstObjectByType<PointerRouter>();
        }

        EnsureStrokeLine();
    }

    private void Update()
    {
        if (pointer == null || !pointer.IsAvailable || navigation == null)
        {
            return;
        }

        var cursor = pointer.WorldPosition;

        // The router decides what a press is for; a waypoint or a path sitting under the cursor
        // takes precedence over starting a new stroke.
        if (pointer.Pressed && router != null && router.Kind == PointerTargetKind.Operator)
        {
            BeginStroke(cursor);
        }
        else if (drawing != null && pointer.Held)
        {
            ExtendStroke(cursor);
        }
        else if (drawing != null && pointer.Released)
        {
            CommitStroke();
        }
    }

    private void BeginStroke(Vector3 cursor)
    {
        drawing = OperatorPicker.At(cursor);

        if (drawing == null)
        {
            return;
        }

        // Not cleared here: this press may turn out to be a click, and a click must leave the
        // existing path alone. The clear happens once the gesture commits to being a drag.

        // Keep the whole stroke in the operator's own plane; sprite z varies for sorting.
        strokeZ = drawing.transform.position.z;

        pressedAt = cursor;
        dragged = false;

        // Starts empty. The first point is recorded once the cursor clears the operator, so no
        // path point — and therefore no waypoint — can ever sit underneath it.
        stroke.Clear();
        RedrawStroke();
    }

    /// <summary>
    ///     Adds a point only when the straight run from the last accepted point stays on walkable
    ///     cells. Clipping a wall drops that sample rather than ending the stroke, so the path can
    ///     pick up again once the cursor comes back into the open.
    /// </summary>
    private void ExtendStroke(Vector3 cursor)
    {
        if (!dragged)
        {
            if (Vector3.Distance(cursor, pressedAt) < clickThreshold)
            {
                return;
            }

            // Past the threshold once, stay a drag for the rest of the gesture.
            dragged = true;
            drawing.ClearPath();
        }

        var origin = drawing.transform.position;

        // Nothing is recorded inside the operator: those points hide under its sprite, and a
        // waypoint landing there would take the clicks meant for the operator itself.
        if (Vector3.Distance(cursor, origin) < drawing.PathStartClearance)
        {
            return;
        }

        var started = stroke.Count > 0;
        var last = started ? stroke[stroke.Count - 1] : origin;

        if (started && Vector3.Distance(cursor, last) < sampleSpacing)
        {
            return;
        }

        // The first segment is validated from the operator, so leaving it cannot cross a wall.
        if (!NavigationQuery.SegmentIsWalkable(navigation, last, cursor, clearanceStep))
        {
            return;
        }

        stroke.Add(new Vector3(cursor.x, cursor.y, strokeZ));
        RedrawStroke();
    }

    private void CommitStroke()
    {
        if (dragged && stroke.Count > 0)
        {
            drawing.SetPath(stroke);
        }
        else
        {
            Clicked?.Invoke(drawing);
        }

        drawing = null;
        stroke.Clear();
        RedrawStroke();
    }

    private void RedrawStroke()
    {
        if (strokeLine == null)
        {
            return;
        }

        PathSmoothing.Smooth(stroke, smoothBuffer, strokeSmoothing);

        strokeLine.positionCount = smoothBuffer.Count;

        for (var i = 0; i < smoothBuffer.Count; i++)
        {
            strokeLine.SetPosition(i, smoothBuffer[i]);
        }
    }

    private void EnsureStrokeLine()
    {
        if (strokeLine != null)
        {
            return;
        }

        var host = new GameObject("StrokeLine");
        host.transform.SetParent(transform, false);

        strokeLine = host.AddComponent<LineRenderer>();
        strokeLine.useWorldSpace = true;
        strokeLine.widthMultiplier = 0.06f;
        strokeLine.numCapVertices = 4;
        strokeLine.positionCount = 0;
        // Dashes come from a tiled on/off texture; LineRenderer has no dash mode.
        strokeLine.textureMode = LineTextureMode.Tile;
        strokeLine.material = new Material(Shader.Find("Sprites/Default"))
        {
            mainTexture = LineArt.Dash(),
            mainTextureScale = new Vector2(dashDensity, 1f)
        };

        strokeLine.startColor = strokeColor;
        strokeLine.endColor = strokeColor;
        strokeLine.sortingOrder = 99;
    }
}
