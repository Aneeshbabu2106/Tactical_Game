using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Draws the ways in on the map itself and lets one be picked by clicking it.
/// </summary>
/// <remarks>
///     Choosing where to go in is a decision about the building, so it is made on the building
///     rather than off a list beside it. The list is still there and the two stay in step — both
///     read and write the same choice on <see cref="MissionState" />.
///     <para>
///         Only ever on the deployment screen. During the mission these would be four bright rings
///         sitting on the floor meaning nothing, so they are switched off with the screen.
///     </para>
/// </remarks>
[RequireComponent(typeof(DeployPoints))]
[DisallowMultipleComponent]
public class DeployMarkers : MonoBehaviour
{
    [SerializeField] private MissionState mission;
    [SerializeField] private PointerInput pointer;

    [Tooltip("How close a click has to land, in cells.")]
    [SerializeField] private float pickRadius = 0.85f;

    [SerializeField] private float markerSize = 1.15f;

    [SerializeField] private Color idle = new(0.49f, 0.83f, 1f, 0.85f);

    [SerializeField] private Color chosen = new(1f, 0.85f, 0.35f, 1f);

    private static Sprite ring;
    private static Sprite disc;

    /// <summary>
    ///     Domain reload is off in this project, so statics outlive a play session. A sprite cached
    ///     by one session is still referenced by the next but no longer draws — the markers simply
    ///     vanished from the second play onwards. Dropped at the start of every session instead.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ring = null;
        disc = null;
    }

    [SerializeField] private Color hovered = new(1f, 1f, 1f, 1f);

    [Tooltip("How much a marker grows while it is under the cursor or a dragged recruit.")]
    [SerializeField] private float hoverGrowth = 1.3f;

    /// <summary>The marker to draw as targeted, or -1. Set by the deployment screen while dragging.</summary>
    public int Hover { get; set; } = -1;

    private DeployPoints points;
    private Transform markers;
    private readonly List<SpriteRenderer> outers = new();
    private readonly List<SpriteRenderer> inners = new();

    private void Awake()
    {
        points = GetComponent<DeployPoints>();

        if (mission == null)
        {
            mission = FindFirstObjectByType<MissionState>();
        }

        if (pointer == null)
        {
            pointer = FindFirstObjectByType<PointerInput>();
        }

        Build();
    }

    private void Update()
    {
        var deploying = mission != null && mission.Screen == GameScreen.Deployment;

        for (var i = 0; i < outers.Count; i++)
        {
            // Filled where somebody is actually going in, hollow where nobody is. The panel says
            // who; the map says where, and the two are the same assignment read two ways.
            var taken = deploying && mission.Occupant(i) >= 0;
            var aimed = deploying && Hover == i;

            outers[i].enabled = deploying;
            inners[i].enabled = taken;
            outers[i].color = aimed ? hovered : taken ? chosen : idle;
            outers[i].transform.localScale = Vector3.one * (markerSize * (aimed ? hoverGrowth : 1f));
        }

        if (!deploying || pointer == null || !pointer.IsAvailable || !pointer.Pressed)
        {
            return;
        }

        // Presses that land on a panel never reach here — PointerInput swallows those — so a
        // click getting this far was aimed at the map.
        var best = Nearest(pointer.WorldPosition);

        if (best >= 0)
        {
            mission.AssignActiveTo(best);
        }
    }

    /// <summary>
    ///     The entry closest to a world point within the pick radius, or -1. A dragged recruit gets
    ///     a wider radius than a click: the drop lands wherever the hand lets go, not on a pixel.
    /// </summary>
    public int Nearest(Vector2 world, float radiusScale = 1f)
    {
        if (points == null)
        {
            return -1;
        }

        var best = -1;
        var bestDistance = pickRadius * radiusScale;

        for (var i = 0; i < points.Count; i++)
        {
            var distance = Vector2.Distance(world, points.Position(i));

            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    private void Build()
    {
        // Not under DeployPoints: its children *are* the entry points, and it counts them by
        // childCount. Markers parented there would be counted as points, and four ways in would
        // silently become twelve.
        var holder = new GameObject("DeployMarkers") { hideFlags = HideFlags.HideAndDontSave };
        holder.transform.SetParent(transform.parent, false);
        markers = holder.transform;

        for (var i = 0; i < points.Count; i++)
        {
            var at = points.Position(i);

            outers.Add(Marker($"DeployRing{i}", at, Ring(), markerSize, 80));
            inners.Add(Marker($"DeployFill{i}", at, Disc(), markerSize * 0.42f, 81));
        }
    }

    private SpriteRenderer Marker(string markerName, Vector3 at, Sprite sprite, float size, int order)
    {
        var host = new GameObject(markerName) { hideFlags = HideFlags.HideAndDontSave };
        host.transform.SetParent(markers, false);
        host.transform.position = at;
        host.transform.localScale = Vector3.one * size;

        var renderer = host.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        renderer.enabled = false;

        // Unlit, as every other runtime sprite in the project is. The default sprite material is
        // lit under the 2D renderer, and the map is dark everywhere outside an operator's cone —
        // markers on the lit shader simply did not appear, which is the whole point of them.
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        return renderer;
    }

    /// <summary>A hollow ring, so the floor under a marker can still be read.</summary>
    private static Sprite Ring()
    {
        // Unity's null check, not ??=: a destroyed sprite is not C# null.
        if (ring == null)
        {
            ring = Circle(0.72f);
        }

        return ring;
    }

    private static Sprite Disc()
    {
        if (disc == null)
        {
            disc = Circle(0f);
        }

        return disc;
    }

    /// <summary>
    ///     A circle, hollow inside <paramref name="inner" /> as a fraction of the radius. Generated
    ///     and never saved, the same trick the placeholder discs and enemy glyphs use.
    /// </summary>
    private static Sprite Circle(float inner)
    {
        const int size = 64;

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
                var d = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / centre;

                // Softened at both edges over a couple of pixels so the ring does not stair-step.
                var a = Mathf.Clamp01((1f - d) * centre * 0.5f)
                        * (inner <= 0f ? 1f : Mathf.Clamp01((d - inner) * centre * 0.5f));

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        texture.Apply();

        var sprite = Sprite.Create(
            texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);

        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
