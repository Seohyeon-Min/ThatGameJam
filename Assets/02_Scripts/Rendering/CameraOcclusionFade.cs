using UnityEngine;
using UnityEngine.Rendering;

// Pushes the bird's and baby's world positions as shader globals every frame, so WatercolorFlat (on the trees)
// can fade out whatever stands between the camera and them, instead of only what is close to the camera itself.
// No scene setup needed: installs itself once, the same way WatercolorManager.PushDefaults does.
public static class CameraOcclusionFade
{
    static readonly int Target0Id = Shader.PropertyToID("_WC_OcclusionTarget0");
    static readonly int Target1Id = Shader.PropertyToID("_WC_OcclusionTarget1");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
    {
        Shader.SetGlobalVector(Target0Id, TargetVector(BirdController.Instance != null ? BirdController.Instance.transform : null));
        Shader.SetGlobalVector(Target1Id, TargetVector(Baby.Instance != null ? Baby.Instance.transform : null));
    }

    static Vector4 TargetVector(Transform target)
    {
        if (target == null)
            return Vector4.zero;
        Vector3 p = target.position;
        return new Vector4(p.x, p.y, p.z, 1f);
    }
}
