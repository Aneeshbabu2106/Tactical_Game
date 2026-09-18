using UnityEngine;

/// <summary>
///     Switches the gameplay input tools off unless a mission is actually running.
/// </summary>
/// <remarks>
///     Deployment shows the whole map and lets it be panned and zoomed, but nothing on it may be
///     touched: drawing a path, opening a door or picking a man up before the squad has gone in
///     would be giving orders to a mission that has not started.
///     <para>
///         Everything on the tools object is switched off except <see cref="PointerInput" />, which
///         has to keep running — the deployment markers are clicked through it, and so is the
///         camera. That is the whole rule, which is why this takes one object rather than a list to
///         keep in step with the scene.
///     </para>
///     <para>
///         The camera lives elsewhere and is never touched, so the map stays explorable throughout.
///     </para>
/// </remarks>
[DisallowMultipleComponent]
public class MissionOnly : MonoBehaviour
{
    [SerializeField] private MissionState mission;

    [Tooltip("The object carrying the gameplay input tools. Everything on it but PointerInput is "
             + "disabled outside a mission.")]
    [SerializeField] private GameObject tools;

    private MonoBehaviour[] gated;

    private void Awake()
    {
        if (mission == null)
        {
            mission = FindFirstObjectByType<MissionState>();
        }

        if (tools != null)
        {
            gated = tools.GetComponents<MonoBehaviour>();
        }
    }

    private void Update()
    {
        if (gated == null || mission == null)
        {
            return;
        }

        var playing = mission.Screen == GameScreen.Mission;

        foreach (var behaviour in gated)
        {
            // The pointer itself is not a tool: without it there is no clicking a marker and no
            // panning the map, which is exactly what deployment is for.
            if (behaviour == null || behaviour is PointerInput)
            {
                continue;
            }

            if (behaviour.enabled != playing)
            {
                behaviour.enabled = playing;
            }
        }
    }
}
