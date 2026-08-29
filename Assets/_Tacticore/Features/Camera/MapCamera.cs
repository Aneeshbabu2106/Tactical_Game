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

        if (pointer != null && pointer.IsAvailable && !Mathf.Approximately(pointer.Scroll, 0f))
        {
            // Scroll up zooms in, which is a smaller orthographic size.
            targetSize = Mathf.Clamp(targetSize - pointer.Scroll * sizePerNotch, minSize, maxSize);
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
    ///     Holds the view over the map. When the map is smaller than the view on an axis it centres
    ///     on that axis instead of clamping, which would otherwise fight itself.
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

        var halfHeight = view.orthographicSize;
        var halfWidth = halfHeight * view.aspect;

        var position = view.transform.position;

        position.x = max.x - min.x <= halfWidth * 2f
            ? (min.x + max.x) * 0.5f
            : Mathf.Clamp(position.x, min.x + halfWidth, max.x - halfWidth);

        position.y = max.y - min.y <= halfHeight * 2f
            ? (min.y + max.y) * 0.5f
            : Mathf.Clamp(position.y, min.y + halfHeight, max.y - halfHeight);

        view.transform.position = position;
    }
}
