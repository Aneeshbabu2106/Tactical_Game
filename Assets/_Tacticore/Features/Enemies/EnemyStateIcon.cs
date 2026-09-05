using UnityEngine;

/// <summary>
///     A glyph over an enemy's head saying what he is thinking: a question mark when he has heard
///     something faint, a bang when he is coming to look or has seen you, a cross once he is down.
/// </summary>
/// <remarks>
///     Built in Awake at a negative execution order so the renderer exists before
///     <see cref="Spottable" /> collects what it hides. That matters: the icon must disappear with
///     the man, or it becomes a marker showing exactly where an unseen enemy is standing.
///     <para>
///         Which is also why the state is expressed as the sprite's alpha rather than by switching
///         the renderer off. Spottable owns <c>enabled</c>, and two components fighting over it
///         would leave the icon showing for an enemy nobody can see.
///     </para>
/// </remarks>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(EnemyBrain))]
[DisallowMultipleComponent]
public class EnemyStateIcon : MonoBehaviour
{
    [Tooltip("Height above the enemy, in cells.")]
    [SerializeField] private float height = 0.55f;

    [Tooltip("Height of the glyph itself, in cells.")]
    [SerializeField] private float size = 0.34f;

    [SerializeField] private Color suspiciousColor = new(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color searchingColor = new(1f, 0.55f, 0.2f, 1f);
    [SerializeField] private Color alertedColor = new(1f, 0.3f, 0.25f, 1f);
    [SerializeField] private Color engagingColor = new(1f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color downedColor = new(0.62f, 0.62f, 0.65f, 1f);

    private static Sprite question;
    private static Sprite bang;
    private static Sprite cross;

    private Enemy self;
    private EnemyBrain brain;
    private EnemyCombat combat;
    private SpriteRenderer glyph;

    private void Awake()
    {
        self = GetComponent<Enemy>();
        brain = GetComponent<EnemyBrain>();
        combat = GetComponent<EnemyCombat>();

        var host = new GameObject("StateIcon");
        host.transform.SetParent(transform, false);

        // Not under the rig: the glyph must stay upright while the man turns underneath it.
        host.transform.localPosition = new Vector3(0f, height, 0f);
        host.transform.localScale = Vector3.one * size;

        glyph = host.AddComponent<SpriteRenderer>();
        glyph.sortingOrder = 95;
    }

    private void LateUpdate()
    {
        if (!self.IsAlive)
        {
            Show(Cross(), downedColor);
            return;
        }

        if (combat != null && combat.IsEngaging)
        {
            Show(Bang(), engagingColor);
            return;
        }

        switch (brain.Alert)
        {
            case AlertLevel.Alerted:
                Show(Bang(), alertedColor);
                break;

            case AlertLevel.Searching:
                Show(Bang(), searchingColor);
                break;

            case AlertLevel.Suspicious:
                Show(Question(), suspiciousColor);
                break;

            default:
                // Nothing on his mind, nothing over his head.
                glyph.color = Color.clear;
                break;
        }
    }

    private void Show(Sprite sprite, Color color)
    {
        glyph.sprite = sprite;
        glyph.color = color;
    }

    private static Sprite Question()
    {
        return question ??= Build(new[]
        {
            ".#####.",
            "##...##",
            "##...##",
            "....##.",
            "...##..",
            "...##..",
            ".......",
            "...##..",
            "...##.."
        });
    }

    private static Sprite Bang()
    {
        return bang ??= Build(new[]
        {
            "...##..",
            "...##..",
            "...##..",
            "...##..",
            "...##..",
            "...##..",
            ".......",
            "...##..",
            "...##.."
        });
    }

    private static Sprite Cross()
    {
        return cross ??= Build(new[]
        {
            ".......",
            "##...##",
            ".##.##.",
            "..###..",
            "...#...",
            "..###..",
            ".##.##.",
            "##...##",
            "......."
        });
    }

    /// <summary>
    ///     Turns pixel art into a sprite. Point-filtered and never saved, the same trick the
    ///     placeholder disc and the path markers use — art can replace these without touching this.
    /// </summary>
    private static Sprite Build(string[] rows)
    {
        var width = rows[0].Length;
        var height = rows.Length;

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point
        };

        for (var y = 0; y < height; y++)
        {
            // Rows read top-down; texture space is bottom-up.
            var row = rows[height - 1 - y];

            for (var x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, row[x] == '#' ? Color.white : Color.clear);
            }
        }

        texture.Apply();

        var sprite = Sprite.Create(
            texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), height);

        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
