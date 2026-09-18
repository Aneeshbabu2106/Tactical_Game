using UnityEngine;

/// <summary>
///     The ways in. Authored as child transforms so entry points are dragged around in the Scene
///     view, the same way patrol routes are.
/// </summary>
/// <remarks>
///     Each child's name is what the deployment screen shows, so call them something a player would
///     recognise — FRONT DOOR, SIDE WINDOW — rather than Point (1).
/// </remarks>
[DisallowMultipleComponent]
public class DeployPoints : MonoBehaviour
{
    [SerializeField] private Color gizmoColor = new(0.5f, 0.85f, 1f, 0.9f);

    [SerializeField] private float gizmoRadius = 0.4f;

    public int Count => transform.childCount;

    public Vector3 Position(int index)
    {
        return transform.GetChild(Mathf.Clamp(index, 0, Count - 1)).position;
    }

    public string Label(int index)
    {
        return transform.GetChild(Mathf.Clamp(index, 0, Count - 1)).name;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;

        for (var i = 0; i < Count; i++)
        {
            Gizmos.DrawWireSphere(transform.GetChild(i).position, gizmoRadius);
        }
    }
}
