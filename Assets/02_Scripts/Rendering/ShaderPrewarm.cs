using System;
using UnityEngine;
using UnityEngine.Rendering;

// The web build makes each shader's GPU program the first time something is drawn with it, which stops the game for up to
// several hundred milliseconds then (the intro's clouds, the forest below them). This draws every material the game uses once,
// shrunk to nothing in front of the camera, in the game's first frames, so those programs are made while the logo starts instead.
// Entries are filled in the Editor by AgentScripts/BuildShaderPrewarm.cs from the scene's renderers; run it again after adding
// materials. Nothing is created: the draws use the scene's own meshes and materials.
// The screen effects (renderer features) are warmed in the same frames: while Warms(camera) is true, each feature records its
// passes as usual but into textures nothing shows, with the Volume Profile's settings where it has a Volume component.
public class ShaderPrewarm : MonoBehaviour
{
    static Camera warming;

    // The screen effects' settings while they are warmed: the Volume Profile whose look uses them.
    public static VolumeProfile Profile { get; private set; }

    // True while this camera's frame should draw the screen effects once, unseen.
    public static bool Warms(Camera camera) => warming != null && camera == warming;

    [Serializable]
    public struct Entry
    {
        public Mesh mesh;
        public int subMesh;
        public Material material;
        public int layer;
        public uint renderingLayerMask;
        public ShadowCastingMode shadowCasting;
        public bool receiveShadows;
    }

    [SerializeField] Camera target;
    [Tooltip("Frames to draw the entries in. One makes the programs; a few more cover passes that start a frame late.")]
    [SerializeField, Min(1)] int frames = 3;
    [SerializeField] Entry[] entries;
    [Tooltip("A Volume Profile with the WatercolorKuwahara and WatercolorBlur settings the game uses, so those passes are warmed.")]
    [SerializeField] VolumeProfile effectsProfile;

    int drawn;

    void LateUpdate()
    {
        if (drawn++ >= frames)
        {
            enabled = false;
            return;
        }
        warming = target;
        Profile = effectsProfile;
        Transform view = target.transform;
        Vector3 at = view.position + view.forward * (target.nearClipPlane + 1f);
        // Large bounds so neither the camera nor the shadow pass culls the draws; the shrunk mesh covers no pixels.
        var bounds = new Bounds(at, Vector3.one * 1000f);
        Matrix4x4 matrix = Matrix4x4.TRS(at, view.rotation, Vector3.one * 1e-5f);
        foreach (Entry entry in entries)
        {
            var render = new RenderParams(entry.material)
            {
                camera = target,
                worldBounds = bounds,
                layer = entry.layer,
                renderingLayerMask = entry.renderingLayerMask,
                shadowCastingMode = entry.shadowCasting,
                receiveShadows = entry.receiveShadows,
            };
            Graphics.RenderMesh(render, entry.mesh, entry.subMesh, matrix);
        }
    }

    void OnDisable()
    {
        warming = null;
        Profile = null;
    }
}
