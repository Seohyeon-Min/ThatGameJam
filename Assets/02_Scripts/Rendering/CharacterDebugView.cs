using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

// Debug: two small windows in the Game view, one camera following the baby and another following the bird, so their
// look can be checked while they are off screen. Works in Play mode and in Edit mode (Game view). Turn it on and off
// with Show in the Inspector, Tools > Watercolor > Toggle Character Debug View (Ctrl+Shift+D), or F3 in Play mode.
// Each camera copies the main camera's lens and look direction and renders only while shown.
// Not part of the game; leave it off in builds.
[ExecuteAlways]
public class CharacterDebugView : MonoBehaviour
{
    [Tooltip("Show the windows. Also Tools > Watercolor > Toggle Character Debug View (Ctrl+Shift+D), or F3 in Play mode.")]
    public bool show = true;
    [SerializeField] bool showBaby = true;
    [SerializeField] bool showBird;
    [Tooltip("How far each debug camera stays from its target, in metres.")]
    [SerializeField, Min(0.5f)] float distance = 9f;
    [Tooltip("Field of view of the debug cameras, in degrees (narrower = larger character).")]
    [SerializeField, Range(10f, 60f)] float fieldOfView = 30f;
    [SerializeField] Vector2Int size = new(400, 300);

    // One camera and window per target: 0 baby, 1 bird.
    readonly Camera[] cameras = new Camera[2];
    readonly RenderTexture[] targets = new RenderTexture[2];

#if UNITY_EDITOR
    // In Edit mode Unity does not run LateUpdate every frame, so follow the targets from the editor loop and repaint
    // the Game view (about 20 times a second).
    double nextEditorTick;

    void OnEnable() => UnityEditor.EditorApplication.update += EditorTick;

    void EditorTick()
    {
        if (Application.isPlaying || UnityEditor.EditorApplication.timeSinceStartup < nextEditorTick)
            return;
        nextEditorTick = UnityEditor.EditorApplication.timeSinceStartup + 0.05;
        LateUpdate();
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    }
#endif

    void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
#endif
        for (int i = 0; i < 2; i++)
        {
            if (cameras[i] != null)
                DestroyImmediate(cameras[i].gameObject);
            if (targets[i] != null)
                DestroyImmediate(targets[i]);
            cameras[i] = null;
            targets[i] = null;
        }
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            show = !show;
    }

    void LateUpdate()
    {
        Camera main = Camera.main;
        // In Edit mode the singletons are not set yet, so find the objects.
        Transform baby = Baby.Instance != null ? Baby.Instance.transform : FindAnyObjectByType<Baby>()?.transform;
        Transform bird = BirdController.Instance != null ? BirdController.Instance.transform : FindAnyObjectByType<BirdController>()?.transform;
        Follow(0, show && showBaby ? baby : null, 0.5f, main);
        Follow(1, show && showBird ? bird : null, 0.2f, main);
    }

    void Follow(int i, Transform followed, float lift, Camera main)
    {
        if (followed == null || main == null)
        {
            if (cameras[i] != null)
                cameras[i].enabled = false;
            return;
        }
        if (cameras[i] == null)
        {
            var go = new GameObject(i == 0 ? "Baby Debug Camera" : "Bird Debug Camera") { hideFlags = HideFlags.HideAndDontSave };
            cameras[i] = go.AddComponent<Camera>();
            targets[i] = new RenderTexture(size.x, size.y, 24) { name = go.name, hideFlags = HideFlags.HideAndDontSave };
        }
        Camera camera = cameras[i];
        camera.CopyFrom(main);
        camera.tag = "Untagged";
        camera.fieldOfView = fieldOfView;
        camera.targetTexture = targets[i];
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        Vector3 focus = followed.position + Vector3.up * lift;
        camera.transform.SetPositionAndRotation(focus - main.transform.forward * distance, main.transform.rotation);
        camera.enabled = true;
    }

    void OnGUI()
    {
        if (!show)
            return;
        float y = 30;
        for (int i = 0; i < 2; i++)
        {
            if (cameras[i] == null || !cameras[i].enabled)
                continue;
            var rect = new Rect(Screen.width - size.x - 12, y, size.x, size.y);
            GUI.Box(new Rect(rect.x - 4, rect.y - 22, rect.width + 8, rect.height + 26), i == 0 ? "Baby" : "Bird");
            GUI.DrawTexture(rect, targets[i], ScaleMode.ScaleToFit, false);
            y += size.y + 34;
        }
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Watercolor/Toggle Character Debug View %#d")]
    static void Toggle()
    {
        var view = FindAnyObjectByType<CharacterDebugView>(FindObjectsInactive.Include);
        if (view == null)
        {
            view = new GameObject("Character Debug View").AddComponent<CharacterDebugView>();
            UnityEditor.Undo.RegisterCreatedObjectUndo(view.gameObject, "Character Debug View");
            return;
        }
        UnityEditor.Undo.RecordObject(view, "Toggle Character Debug View");
        view.show = !view.show;
        UnityEditor.EditorUtility.SetDirty(view);
    }
#endif
}
