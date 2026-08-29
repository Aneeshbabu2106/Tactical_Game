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
    [SerializeField] private Tilemap navigation;

    [Tooltip("Distance the cursor must travel before another point is added to the stroke.")]
    [SerializeField] private float sampleSpacing = 0.0875f;

    [Tooltip("Spacing of the walkability samples taken along each new segment.")]
    [SerializeField] private float clearanceStep = 0.2f;

    [SerializeField] private Color strokeColor = Color.white;

    [Tooltip("Dash cycles per world unit along the stroke.")]
    [SerializeField] private float dashDensity = 4f;

    [Tooltip("Curve samples per drawn segment. 1 draws the raw polyline.")]
    [Range(1, 12)]
    [SerializeField] private int strokeSmoothing = 6;
    [SerializeField] private LineRenderer strokeLine;

    private readonly List<Vector3> stroke = new();
    private readonly List<Vector3> smoothBuffer = new();
    private Operator drawing;
    private float strokeZ;

    private void Awake()
    {
        if (pointer == null)
        {
            pointer = FindFirstObjectByType<PointerInput>();
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

        if (pointer.Pressed)
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

        drawing.ClearPath();

        // Keep the whole stroke in the operator's own plane; sprite z varies for sorting.
        strokeZ = drawing.transform.position.z;

        stroke.Clear();
        stroke.Add(drawing.transform.position);
        RedrawStroke();
    }

    /// <summary>
    ///     Adds a point only when the straight run from the last accepted point stays on walkable
    ///     cells. Clipping a wall drops that sample rather than ending the stroke, so the path can
    ///     pick up again once the cursor comes back into the open.
    /// </summary>
    private void ExtendStroke(Vector3 cursor)
    {
        var last = stroke[stroke.Count - 1];

        if (Vector3.Distance(cursor, last) < sampleSpacing)
        {
            return;
        }

        if (!SegmentIsWalkable(last, cursor))
        {
            return;
        }

        stroke.Add(new Vector3(cursor.x, cursor.y, strokeZ));
        RedrawStroke();
    }

    private void CommitStroke()
    {
        // The first point is the operator's own position, so a stroke of one is just a click.
        if (stroke.Count > 1)
        {
            drawing.SetPath(stroke.GetRange(1, stroke.Count - 1));
        }

        drawing = null;
        stroke.Clear();
        RedrawStroke();
    }

    private bool SegmentIsWalkable(Vector3 from, Vector3 to)
    {
        var length = Vector3.Distance(from, to);
        var steps = Mathf.Max(1, Mathf.CeilToInt(length / clearanceStep));

        for (var i = 0; i <= steps; i++)
        {
            var point = Vector3.Lerp(from, to, i / (float)steps);

            if (!NavigationQuery.IsWalkable(navigation, navigation.WorldToCell(point)))
            {
                return false;
            }
        }

        return true;
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
