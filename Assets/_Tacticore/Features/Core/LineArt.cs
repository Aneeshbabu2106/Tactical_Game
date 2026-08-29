using UnityEngine;

/// <summary>
///     Shared runtime art for line rendering: a dash texture and a dot sprite, so paths can be drawn
///     without shipping any assets yet.
/// </summary>
public static class LineArt
{
    private static Texture2D dashTexture;
    private static Sprite arrowSprite;

    /// <summary>
    ///     A repeating on/off strip matching the prototype's setLineDash([4, 3]). Used with
    ///     <see cref="LineTextureMode.Tile" />, since LineRenderer has no dash support of its own.
    /// </summary>
    public static Texture2D Dash()
    {
        if (dashTexture != null)
        {
            return dashTexture;
        }

        const int on = 4;
        const int off = 3;

        dashTexture = new Texture2D(on + off, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };

        for (var x = 0; x < on + off; x++)
        {
            dashTexture.SetPixel(x, 0, x < on ? Color.white : new Color(1f, 1f, 1f, 0f));
        }

        dashTexture.Apply();
        return dashTexture;
    }

    /// <summary>
    ///     A triangular arrowhead pointing along +X, pivoted centre so it can be rotated to the
    ///     path's direction of travel.
    /// </summary>
    public static Sprite Arrow()
    {
        if (arrowSprite != null)
        {
            return arrowSprite;
        }

        const int size = 24;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        // Apex on the +X edge, base spanning the -X edge.
        var apex = new Vector2(size - 0.5f, (size - 1) * 0.5f);
        var baseTop = new Vector2(0.5f, size - 0.5f);
        var baseBottom = new Vector2(0.5f, 0.5f);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                // 2x2 supersample for coverage, otherwise the diagonals stair-step badly.
                var hits = 0;

                for (var sy = 0; sy < 2; sy++)
                {
                    for (var sx = 0; sx < 2; sx++)
                    {
                        var sample = new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f);

                        if (Inside(sample, apex, baseTop, baseBottom))
                        {
                            hits++;
                        }
                    }
                }

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, hits / 4f));
            }
        }

        texture.Apply();

        arrowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        arrowSprite.hideFlags = HideFlags.HideAndDontSave;
        return arrowSprite;
    }

    /// <summary>Edge-function test: inside when the point is on the same side of all three edges.</summary>
    private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        var d1 = Edge(p, a, b);
        var d2 = Edge(p, b, c);
        var d3 = Edge(p, c, a);

        var negative = d1 < 0f || d2 < 0f || d3 < 0f;
        var positive = d1 > 0f || d2 > 0f || d3 > 0f;

        return !(negative && positive);
    }

    private static float Edge(Vector2 p, Vector2 a, Vector2 b)
    {
        return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
    }
}
