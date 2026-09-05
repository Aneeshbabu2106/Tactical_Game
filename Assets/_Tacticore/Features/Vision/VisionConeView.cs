using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Draws what the selected operator can see: a circle of awareness around him and the cone he
///     is facing down, built from the rays <see cref="VisionField" /> already cast this frame.
/// </summary>
/// <remarks>
///     Only the selected operator gets one, which is the prototype's rule — "unselected: fog lift
///     only". A cone per man turns a squad into overlapping wedges and stops reading as anyone's
///     direction of attention.
///     <para>
///         Drawn additively, as the prototype does — "a warm kiss inside the SELECTED man's cone" —
///         with the colour falling to nothing at the rim so it fades along its length rather than
///         ending on a hard edge. Adding light reads far better here than taking it away: the floor
///         is already near black, so there is nothing to subtract.
///     </para>
/// </remarks>
[DefaultExecutionOrder(60)]
[DisallowMultipleComponent]
public class VisionConeView : MonoBehaviour
{
    [SerializeField] private VisionField vision;

    [Tooltip("Strength of the close circle relative to the cone. It covers far less ground, so it "
             + "does not need as much to read.")]
    [Range(0f, 2f)]
    [SerializeField] private float circleStrength = 0.7f;

    [SerializeField] private int sortingOrder = 50;

    private readonly List<Vector3> vertices = new();
    private readonly List<Color> colors = new();
    private readonly List<int> triangles = new();

    private Mesh mesh;
    private MeshRenderer meshRenderer;

    private void Awake()
    {
        if (vision == null)
        {
            vision = FindFirstObjectByType<VisionField>();
        }

        Build();
    }

    private void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
        }
    }

    private void Build()
    {
        mesh = new Mesh { name = "VisionCone", hideFlags = HideFlags.HideAndDontSave };
        mesh.MarkDynamic();

        var host = new GameObject("Cone");
        host.transform.SetParent(transform, false);

        host.AddComponent<MeshFilter>().sharedMesh = mesh;

        meshRenderer = host.AddComponent<MeshRenderer>();

        // Additive: the cone adds light rather than laying a film over what is underneath.
        var material = new Material(Shader.Find("Sprites/Default"))
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);

        meshRenderer.sharedMaterial = material;
        meshRenderer.enabled = false;
    }

    private void LateUpdate()
    {
        // After VisionField, so the rays are this frame's.
        var op = vision != null ? vision.Selected : null;

        if (op == null || !vision.TryGetFan(op, out var fan))
        {
            meshRenderer.enabled = false;
            return;
        }

        vertices.Clear();
        colors.Clear();
        triangles.Clear();

        // Toward the camera a little, so it sits over the floor rather than fighting it for depth.
        var apex = op.transform.position;
        apex.z -= 0.01f;

        var color = op.ConeColor;

        AddFan(apex, fan.Cone, color, op.VisionRange);

        var circle = color;
        circle.a *= circleStrength;
        AddFan(apex, fan.Circle, circle, op.VisionNearRadius);

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);

        meshRenderer.enabled = triangles.Count > 0;
    }

    /// <summary>
    ///     One triangle per pair of neighbouring rays. The apex carries the full colour and the rim
    ///     none of it, which gives the falloff the prototype's fanGrad paints — brightest at the man,
    ///     gone by the end of his sight.
    /// </summary>
    private void AddFan(Vector3 apex, List<Vector3> rim, Color color, float range)
    {
        if (rim.Count < 2)
        {
            return;
        }

        var apexIndex = vertices.Count;
        var edge = new Color(color.r, color.g, color.b, 0f);
        var reach = Mathf.Max(range, 0.01f);

        vertices.Add(apex);
        colors.Add(color);

        for (var i = 0; i < rim.Count; i++)
        {
            var point = rim[i];
            point.z = apex.z;

            vertices.Add(point);

            // A ray cut short by a wall has not faded yet, so it keeps some of the light and the
            // shape stays bright right up against whatever stopped it.
            var t = Mathf.Clamp01(Vector2.Distance(point, apex) / reach);
            colors.Add(Color.Lerp(color, edge, t * t));

            if (i > 0)
            {
                triangles.Add(apexIndex);
                triangles.Add(apexIndex + i);
                triangles.Add(apexIndex + i + 1);
            }
        }
    }
}
