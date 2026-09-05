using UnityEngine;

/// <summary>
///     Something that only shows when an operator can actually see it. <see cref="VisionField" />
///     drives this every frame; nothing else should.
/// </summary>
/// <remarks>
///     Hides by switching the renderers off rather than the GameObject, so the thing carries on
///     existing and thinking while out of sight — an enemy you cannot see is still there.
/// </remarks>
[DisallowMultipleComponent]
public class Spottable : MonoBehaviour
{
    [Tooltip("What to hide. Left empty, every renderer underneath this object is used.")]
    [SerializeField] private Renderer[] visuals;

    [Tooltip("Draw a placeholder disc, for standing something in the map before there is art.")]
    [SerializeField] private bool placeholder = true;

    [Tooltip("Where the marker's look comes from. Left empty, the inline fallbacks below are used. "
             + "Optional because this is a general visibility switch, not an enemy component.")]
    [SerializeField] private EnemySpec spec;

    [SerializeField] private Color placeholderColor = new(0.93f, 0.29f, 0.31f, 1f);

    [SerializeField] private float placeholderSize = 0.62f;

    private static Sprite disc;

    public bool IsVisible { get; private set; }

    private void Awake()
    {
        if (placeholder && Application.isPlaying)
        {
            BuildPlaceholder();
        }

        if (visuals == null || visuals.Length == 0)
        {
            visuals = GetComponentsInChildren<Renderer>(true);
        }

        // Unseen until something says otherwise, so nothing flashes on for the first frame. Applied
        // directly rather than through SetVisible, which short-circuits when the value is unchanged
        // and would leave the renderers on.
        IsVisible = false;
        Apply();
    }

    public void SetVisible(bool visible)
    {
        if (IsVisible == visible)
        {
            return;
        }

        IsVisible = visible;
        Apply();
    }

    private void Apply()
    {
        foreach (var renderer in visuals)
        {
            if (renderer != null)
            {
                renderer.enabled = IsVisible;
            }
        }
    }

    /// <summary>
    ///     Runtime only, and never serialised. Built in the editor it becomes a real scene object
    ///     holding a generated sprite, and a generated sprite cannot be saved — so it reloads as an
    ///     empty renderer that draws nothing.
    /// </summary>
    private void BuildPlaceholder()
    {
        var host = new GameObject("Marker") { hideFlags = HideFlags.HideAndDontSave };
        host.transform.SetParent(transform, false);
        host.transform.localScale = Vector3.one * (spec != null ? spec.markerSize : placeholderSize);

        var renderer = host.AddComponent<SpriteRenderer>();
        renderer.sprite = Disc();
        renderer.color = spec != null ? spec.markerColor : placeholderColor;
        renderer.sortingOrder = 90;
    }

    /// <summary>A filled circle, built once and shared. Same trick the path markers use.</summary>
    private static Sprite Disc()
    {
        if (disc != null)
        {
            return disc;
        }

        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };

        var centre = (size - 1) * 0.5f;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                // Softened over a pixel at the rim so it does not read as a staircase.
                var d = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                var a = Mathf.Clamp01(centre - d);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        texture.Apply();

        disc = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        disc.hideFlags = HideFlags.HideAndDontSave;
        return disc;
    }
}
