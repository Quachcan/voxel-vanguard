using System;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Interface optional: Component provide state name.
/// </summary>
public interface IDebugStateProvider
{
    string GetDebugStateName();
}

/// <summary>
/// Optional: provider for sub-state name.
/// </summary>
public interface IDebugSubStateProvider
{
    string GetDebugSubStateName();
}

public class DebugOverlay : MonoBehaviour
{
    [Header("State (Optional)")]
    [Tooltip("Component implement IDebugStateProvider to display state name.")]
    public MonoBehaviour stateProvider;
    [Tooltip("Component implement IDebugSubStateProvider to display sub-state name.")]
    public MonoBehaviour subStateProvider;

    [Header("Target (Optional)")]
    public Transform target;

    [Header("Target Zone (Optional)")]
    public Transform targetZoneCenter;
    public float targetZoneRadius = 3f;
    public Color targetZoneColor = new Color(0.2f, 0.8f, 1f, 0.9f);

    [Header("Gizmos Style")]
    public Color gizmoTargetColor = Color.yellow;
    public Color gizmoVelocityColor = Color.cyan;

#if UNITY_EDITOR
    [Header("Handles Style (Editor Only)")]
    public bool showHandles = true;
    public Color handlesLabelColor = new Color(1f, 1f, 0.2f, 1f);
    public float handlesArrowSize = 2f;
#endif

    [Header("GUI Overlay")]
    public bool showGUI = true;
    public bool showGizmos = true;
    public int guiFontSize = 40;
    public Vector2 guiAreaPos = new Vector2(0, 120);
    public Vector2 guiAreaSize = new Vector2(400, 500);

    [Header("Hotkeys")]
    public KeyCode toggleGUIKey = KeyCode.F2;
    public KeyCode toggleGizmosKey = KeyCode.F3;
#if UNITY_EDITOR
    public KeyCode toggleHandlesKey = KeyCode.F4;
#endif

    // Cached styles
    private GUIStyle _labelStyle;
    private GUIStyle _headerStyle;

    // FPS tracking
    private float _fps;
    private float _accum;
    private int _frames;
    private float _fpsTimer;

    void Update()
    {
        // Hotkeys
        if (Input.GetKeyDown(toggleGUIKey)) showGUI = !showGUI;
        if (Input.GetKeyDown(toggleGizmosKey)) showGizmos = !showGizmos;
#if UNITY_EDITOR
        if (Input.GetKeyDown(toggleHandlesKey)) showHandles = !showHandles;
#endif

        // FPS avg update
        _accum += Time.unscaledDeltaTime;
        _frames++;
        _fpsTimer += Time.unscaledDeltaTime;
        if (_fpsTimer >= 0.5f)
        {
            _fps = _frames / _accum;
            _fpsTimer = 0f; _accum = 0f; _frames = 0;
        }
    }

    void OnGUI()
    {
        if (!showGUI) return;

        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = guiFontSize,
                richText = true,
                normal = { textColor = Color.white }
            };
        }
        if (_headerStyle == null)
        {
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = guiFontSize + 2,
                fontStyle = FontStyle.Bold,
                richText = true,
                normal = { textColor = new Color(1f, 0.9f, 0.2f, 1f) }
            };
        }

        GUILayout.BeginArea(new Rect(guiAreaPos, guiAreaSize), GUI.skin.box);
        GUILayout.Label("<b>Debug Overlay</b>", _headerStyle);

        string scene = SceneManager.GetActiveScene().name;
        GUILayout.Label($"Scene: <b>{scene}</b>", _labelStyle);
        GUILayout.Label($"FPS: <b>{_fps:0.0}</b>  |  TimeScale: <b>{Time.timeScale:0.##}</b>", _labelStyle);

        // State (optional)
        string stateName = TryGetStateName();
        if (!string.IsNullOrEmpty(stateName))
            GUILayout.Label($"State: <b>{stateName}</b>", _labelStyle);

        // Sub-State (optional)
        string subStateName = TryGetSubStateName();
        if (!string.IsNullOrEmpty(subStateName))
            GUILayout.Label($"Sub: <b>{subStateName}</b>", _labelStyle);

        // Target info
        if (target)
        {
            Vector3 p = target.position;
            GUILayout.Label($"Target Pos: <b>{p.x:0.00}, {p.y:0.00}, {p.z:0.00}</b>", _labelStyle);

            if (target.TryGetComponent<Rigidbody>(out var rb))
            {
                var v = rb.linearVelocity;
                GUILayout.Label($"Velocity: <b>{v.magnitude:0.00}</b> m/s   ({v.x:0.00}, {v.y:0.00}, {v.z:0.00})", _labelStyle);
            }
        }

#if UNITY_EDITOR
        GUILayout.Space(4);
        GUILayout.Label($"[F2] GUI  [F3] Gizmos  [F4] Handles", _labelStyle);
#else
        GUILayout.Space(4);
        GUILayout.Label($"[F2] GUI  [F3] Gizmos", _labelStyle);
#endif
        GUILayout.EndArea();
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Target marker
        if (target)
        {
            Gizmos.color = gizmoTargetColor;
            Gizmos.DrawWireSphere(target.position, 0.4f);

            if (target.TryGetComponent<Rigidbody>(out var rb))
            {
                Gizmos.color = gizmoVelocityColor;
                Vector3 v = rb.linearVelocity;
                Gizmos.DrawLine(target.position, target.position + v);
            }
        }

        // Target zone
        if (targetZoneCenter && targetZoneRadius > 0f)
        {
            Gizmos.color = targetZoneColor;
            Gizmos.DrawWireSphere(targetZoneCenter.position, targetZoneRadius);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!showGizmos || !target || !showHandles) return;

        Handles.color = handlesLabelColor;
        Handles.ArrowHandleCap(
            0,
            target.position,
            Quaternion.LookRotation(Camera.current ? (Camera.current.transform.position - target.position) : Vector3.forward),
            handlesArrowSize,
            EventType.Repaint
        );

        Handles.Label(target.position + Vector3.up * 1.2f,
            $"Target\n{target.position}",
            new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = handlesLabelColor } });
    }
#endif

    private string TryGetStateName()
    {
        if (stateProvider is IDebugStateProvider sp)
        {
            try { return sp.GetDebugStateName(); }
            catch { return "[State Error]"; }
        }
        return null;
    }

    private string TryGetSubStateName()
    {
        if (subStateProvider is IDebugSubStateProvider ssp)
        {
            try { return ssp.GetDebugSubStateName(); }
            catch { return "[SubState Error]"; }
        }
        
        if (stateProvider is IDebugSubStateProvider ssp2)
        {
            try { return ssp2.GetDebugSubStateName(); }
            catch { return "[SubState Error]"; }
        }

        return null;
    }
}