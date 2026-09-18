using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
///     Wheel zoom for the orthographic map camera, clamped to a range and anchored on the cursor so
///     the tile under the pointer stays put.
/// </summary>
[DisallowMultipleComponent]
public class MapCamera : MonoBehaviour
{
    [SerializeField] private Camera view;
    [SerializeField] private PointerInput pointer;

    [Tooltip("Optional. Keeps the view over the map; leave empty to allow free drift.")]
    [SerializeField] private Tilemap bounds;

    [Header("Zoom range")]
    [Tooltip("Orthographic half-height at full zoom in.")]
    [SerializeField] private float minSize = 4f;

    [Tooltip("Orthographic half-height at full zoom out.")]
    [SerializeField] private float maxSize = 12f;

    [SerializeField] private float sizePerNotch = 1f;

    [Tooltip("Higher snaps to the target zoom faster. Zero disables smoothing.")]
    [SerializeField] private float smoothing = 14f;

    [SerializeField] private bool zoomTowardsCursor = true;

    [Header("Pan")]
    [Tooltip("Middle-drag to pan. Left and right are taken by the operator tools.")]
    [SerializeField] private bool panEnabled = true;

    private float targetSize;
    private Vector3 grabbed;
    private bool panning;

    /// <summary>
    ///     The part of the screen the map is framed into and held within, as a viewport rect. The
    ///     whole screen unless something — the deployment panels — covers the sides of it.
    /// </summary>
    private Rect focus = new(0f, 0f, 1f, 1f);

    /// <summary>Zoom-out limit. Raised by a framing that needs more room than maxSize allows.</summary>
    private float ceiling = -1f;

    private float Ceiling => ceiling > 0f ? ceiling : maxSize;

    private void Awake()
    {
        if (view == null)
        {
            view = GetComponent<Camera>();
        }

        if (view == null)
        {
            view = Camera.main;
        }

        if (pointer == null)
        {
            pointer = FindFirstObjectByType<PointerInput>();
        }

        if (view != null)
        {
            targetSize = Mathf.Clamp(view.orthographicSize, minSize, maxSize);
            view.orthographicSize = targetSize;
        }
    }

    private void Update()
    {
        if (view == null || !view.orthographic)
        {
            return;
        }

        // Not over a panel: the wheel there belongs to the panel, not the map behind it.
        if (pointer != null && pointer.IsAvailable && !pointer.OverUi
            && !Mathf.Approximately(pointer.Scroll, 0f))
        {
            // Scroll up zooms in, which is a smaller orthographic size.
            targetSize = Mathf.Clamp(targetSize - pointer.Scroll * sizePerNotch, minSize, Ceiling);
        }

        ApplyZoom();
        HandlePan();
        ClampToBounds();
    }

    /// <summary>
    ///     Grab-and-drag: the world point picked up on press is kept under the cursor, so the map
    ///     tracks the mouse one-to-one at any zoom rather than at some tuned pixel rate.
    /// </summary>
    private void HandlePan()
    {
        if (!panEnabled || pointer == null || !pointer.IsAvailable)
        {
            panning = false;
            return;
        }

        if (pointer.MiddlePressed)
        {
            grabbed = view.ScreenToWorldPoint(pointer.ScreenPosition);
            panning = true;
            return;
        }

        if (pointer.MiddleReleased || !pointer.MiddleHeld)
        {
            panning = false;
            return;
        }

        if (!panning)
        {
            return;
        }

        // Recomputed against the camera as it is now, so the correction converges instead of drifting.
        var under = view.ScreenToWorldPoint(pointer.ScreenPosition);
        var delta = grabbed - under;
        delta.z = 0f;
        view.transform.position += delta;
    }

    private void ApplyZoom()
    {
        var current = view.orthographicSize;

        if (Mathf.Approximately(current, targetSize))
        {
            return;
        }

        // Exponential decay, so the feel does not change with frame rate.
        var next = smoothing > 0f
            ? Mathf.Lerp(current, targetSize, 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime))
            : targetSize;

        if (!zoomTowardsCursor || pointer == null || !pointer.IsAvailable)
        {
            view.orthographicSize = next;
            return;
        }

        // Keep whatever is under the cursor pinned: re-project the same pixel before and after.
        var before = view.ScreenToWorldPoint(pointer.ScreenPosition);
        view.orthographicSize = next;
        var after = view.ScreenToWorldPoint(pointer.ScreenPosition);

        var drift = before - after;
        drift.z = 0f;
        view.transform.position += drift;
    }

    /// <summary>
    ///     Pulls back far enough to show the whole building and centres on it. Used at deployment,
    ///     where the decision is about the map as a whole and starting zoomed into one corner of it
    ///     would hide most of what the decision is about.
    /// </summary>
    public void FrameAll()
    {
        FrameInto(new Rect(0f, 0f, 1f, 1f));
    }

    /// <summary>
    ///     Frames the whole building into part of the screen, given as a viewport rect, and keeps
    ///     the view held there until <see cref="ClearFocus" />. Panels down both sides of the
    ///     deployment screen would otherwise cover the ends of the building a full-screen framing
    ///     puts under them.
    /// </summary>
    public void FrameInto(Rect viewport)
    {
        focus = new Rect(
            Mathf.Clamp01(viewport.x), Mathf.Clamp01(viewport.y),
            Mathf.Clamp(viewport.width, 0.05f, 1f), Mathf.Clamp(viewport.height, 0.05f, 1f));

        if (bounds == null || view == null)
        {
            return;
        }

        var local = bounds.localBounds;
        var min = bounds.transform.TransformPoint(local.min);
        var max = bounds.transform.TransformPoint(local.max);

        // Whichever axis needs more room decides the zoom, so nothing is cropped.
        var forHeight = (max.y - min.y) * 0.5f / focus.height;
        var forWidth = (max.x - min.x) * 0.5f / (Mathf.Max(view.aspect, 0.0001f) * focus.width);
        var size = Mathf.Max(forHeight, forWidth) * 1.04f;

        // A narrow focus can need more than maxSize; allow it rather than crop the building.
        ceiling = Mathf.Max(maxSize, size);
        targetSize = Mathf.Clamp(size, minSize, Ceiling);
        view.orthographicSize = targetSize;

        var centre = (min + max) * 0.5f - FocusOffset();
        view.transform.position = new Vector3(centre.x, centre.y, view.transform.position.z);
    }

    /// <summary>Back to the whole screen and the authored zoom range.</summary>
    public void ClearFocus()
    {
        focus = new Rect(0f, 0f, 1f, 1f);
        ceiling = -1f;
        targetSize = Mathf.Clamp(targetSize, minSize, Ceiling);
    }

    /// <summary>World offset from the camera's centre to the centre of the focus area.</summary>
    private Vector3 FocusOffset()
    {
        var halfHeight = view.orthographicSize;
        var halfWidth = halfHeight * view.aspect;

        return new Vector3(
            (focus.center.x - 0.5f) * 2f * halfWidth,
            (focus.center.y - 0.5f) * 2f * halfHeight,
            0f);
    }

    /// <summary>
    ///     Holds the view over the map. When the map is smaller than the view on an axis it centres
    ///     on that axis instead of clamping, which would otherwise fight itself. Measured against
    ///     the focus area, not the whole screen, so a framing into it is not undone next frame.
    /// </summary>
    private void ClampToBounds()
    {
        if (bounds == null)
        {
            return;
        }

        var local = bounds.localBounds;
        var min = bounds.transform.TransformPoint(local.min);
        var max = bounds.transform.TransformPoint(local.max);

        var halfHeight = view.orthographicSize * focus.height;
        var halfWidth = view.orthographicSize * view.aspect * focus.width;

        var offset = FocusOffset();
        var centre = view.transform.position + offset;

        centre.x = max.x - min.x <= halfWidth * 2f
            ? (min.x + max.x) * 0.5f
            : Mathf.Clamp(centre.x, min.x + halfWidth, max.x - halfWidth);

        centre.y = max.y - min.y <= halfHeight * 2f
            ? (min.y + max.y) * 0.5f
            : Mathf.Clamp(centre.y, min.y + halfHeight, max.y - halfHeight);

        view.transform.position = centre - offset;
    }
}
