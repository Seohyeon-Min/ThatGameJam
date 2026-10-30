using UnityEngine;

// Light beam marking the baby's destination: one column of light from far above (off screen, over the clouds) down to
// this object's position on the ground. Place this object at the foot of the beam; its up axis is the beam's axis.
// Needs a MeshRenderer whose material uses the ThatGameJam/LightBeam shader; the look is on the material.
// This builds a ribbon mesh (x: offset across the beam in world units, y: height above the foot); the shader turns it to
// face the camera around the up axis. Changing a field here rebuilds the mesh, in Edit Mode too.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LightBeam : MonoBehaviour
{
    [Tooltip("Length of the beam from the ground up, in world units. Make it reach well past the top of the screen.")]
    [SerializeField, Min(0.1f)] float height = 60f;
    [Tooltip("Width of the beam where it meets the ground, in world units.")]
    [SerializeField, Min(0.01f)] float footWidth = 2.5f;
    [Tooltip("Width of the beam at its top, in world units. Wider than the foot spreads it out like light through a gap in the clouds.")]
    [SerializeField, Min(0.01f)] float topWidth = 4f;
    [Tooltip("Rows along the height. More rows make the taper and the edge wobble smoother.")]
    [SerializeField, Range(2, 128)] int rows = 32;

    static readonly int RevealId = Shader.PropertyToID("_Reveal");

    Mesh mesh;
    MaterialPropertyBlock block;

    // How far the beam has come down from the top: 0 hidden, 1 reached the ground. Only this beam changes, not the material.
    public void SetReveal(float reveal)
    {
        block ??= new MaterialPropertyBlock();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.GetPropertyBlock(block);
        block.SetFloat(RevealId, reveal);
        meshRenderer.SetPropertyBlock(block);
        meshRenderer.enabled = reveal > 0f;
    }

    void OnEnable() => Build();

    void OnValidate() => Build();

    void OnDestroy()
    {
        if (mesh == null)
            return;
        if (Application.isPlaying)
            Destroy(mesh);
        else
            DestroyImmediate(mesh);
    }

    void Build()
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "LightBeam" };
            mesh.hideFlags = HideFlags.DontSave;
        }

        var vertices = new Vector3[rows * 2];
        var uvs = new Vector2[rows * 2];
        var triangles = new int[(rows - 1) * 6];
        for (int r = 0; r < rows; r++)
        {
            float t = r / (float)(rows - 1);
            float halfWidth = Mathf.Lerp(footWidth, topWidth, t) * 0.5f;
            float y = t * height;
            vertices[r * 2] = new Vector3(-halfWidth, y, 0f);
            vertices[r * 2 + 1] = new Vector3(halfWidth, y, 0f);
            uvs[r * 2] = new Vector2(0f, t);
            uvs[r * 2 + 1] = new Vector2(1f, t);
        }
        for (int r = 0; r < rows - 1; r++)
        {
            int i = r * 2;
            int k = r * 6;
            triangles[k] = i;
            triangles[k + 1] = i + 2;
            triangles[k + 2] = i + 1;
            triangles[k + 3] = i + 1;
            triangles[k + 4] = i + 2;
            triangles[k + 5] = i + 3;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        // The shader turns the ribbon around the up axis, so the bounds must cover every turn.
        float reach = Mathf.Max(footWidth, topWidth) * 0.5f;
        mesh.bounds = new Bounds(new Vector3(0f, height * 0.5f, 0f), new Vector3(reach * 2f, height, reach * 2f));
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }
}
