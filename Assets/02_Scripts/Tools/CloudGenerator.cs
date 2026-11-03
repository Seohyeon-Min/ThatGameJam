using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Edit-time tool on a cloud: fills it with camera-facing puffs (the CloudPuff material), scattered in an oval. Press Generate in the
// Inspector; nothing runs in the game. On a prefab instance, Generate rebuilds the prefab itself, so every instance of it changes.
public class CloudGenerator : MonoBehaviour
{
    const string PuffPrefix = "Puff_";

    [SerializeField] Material puffMaterial;
    [SerializeField, Min(1)] int puffCount = 14;
    [Tooltip("How far puffs reach from the centre, in world units, along X and Z.")]
    [SerializeField] Vector2 spread = new(7f, 5f);
    [Tooltip("How far puffs reach above and below the centre, in world units. More reads as a thicker cloud to fly through.")]
    [SerializeField, Min(0f)] float heightSpread = 1f;
    [Tooltip("Smallest puff across, in world units.")]
    [SerializeField, Min(0f)] float minPuffSize = 3.2f;
    [Tooltip("Largest puff across, in world units.")]
    [SerializeField, Min(0f)] float maxPuffSize = 5.2f;
    [Tooltip("How much smaller puffs get toward the rim (0 = the same all over), for a rounder outline.")]
    [SerializeField, Range(0f, 1f)] float rimShrink = 0.35f;
    [Tooltip("Above 0.5 gathers puffs in the middle, below it pushes them outward.")]
    [SerializeField, Range(0.2f, 1f)] float centreBias = 0.7f;
    [Tooltip("Leaves a clear round hole this wide around the centre, in world units, to see through. 0 = none.")]
    [SerializeField, Min(0f)] float holeRadius;
    [Tooltip("Changes the scatter.")]
    [SerializeField] int seed = 1842;
    [Tooltip("The way the intro camera looks. Puffs are drawn in a fixed order along it, farthest first, so overlapping puffs don't swap places and flicker as the camera moves.")]
    [SerializeField] Vector3 drawOrderView = new(-0.28f, -0.92f, 0.28f);

    // Draw order per centimetre along Draw Order View. All of it above 0, so the clouds draw over the snow and other see-through things
    // below them. Whatever must show over the clouds (the bird's particles) is ordered higher still.
    const float OrderPerMetre = 100f;
    const int MiddleOrder = 10000;

#if UNITY_EDITOR
    public int PuffCount
    {
        get
        {
            int count = 0;
            foreach (Transform child in transform)
                if (child.name.StartsWith(PuffPrefix))
                    count++;
            return count;
        }
    }

    public void Generate()
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(this))
        {
            Build();
            return;
        }

        // Children of an instance can't be removed, so the settings go to the prefab and it is rebuilt there.
        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
        {
            var generator = scope.prefabContentsRoot.GetComponent<CloudGenerator>();
            if (generator == null)
            {
                Debug.LogWarning($"CloudGenerator: the prefab {path} has no CloudGenerator.", this);
                return;
            }
            EditorUtility.CopySerialized(this, generator);
            generator.Build();
        }
        PrefabUtility.RevertObjectOverride(this, InteractionMode.AutomatedAction);
        SortAllPuffs();
    }

    // Every cloud's puffs in the scene, off ones too, in one order: from where they stand, so the clouds needn't know of each other.
    public static void SortAllPuffs()
    {
        foreach (CloudGenerator cloud in FindObjectsByType<CloudGenerator>(FindObjectsInactive.Include))
            cloud.SortPuffs();
    }

    // Farther along Draw Order View draws first (a lower sortingOrder), whatever the camera's distance to each puff is that frame.
    void SortPuffs()
    {
        Vector3 view = drawOrderView.sqrMagnitude > 0f ? drawOrderView.normalized : Vector3.down;
        foreach (Transform child in transform)
        {
            if (!child.name.StartsWith(PuffPrefix) || !child.TryGetComponent(out Renderer renderer))
                continue;
            int order = Mathf.Clamp(MiddleOrder - Mathf.RoundToInt(Vector3.Dot(child.position, view) * OrderPerMetre), short.MinValue, short.MaxValue);
            if (renderer.sortingOrder == order)
                continue;
            Undo.RecordObject(renderer, "Sort Cloud Puffs");
            renderer.sortingOrder = order;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }

    void Build()
    {
        if (puffMaterial == null)
        {
            Debug.LogWarning("CloudGenerator: assign the Puff Material first.", this);
            return;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
            if (transform.GetChild(i).name.StartsWith(PuffPrefix))
                DestroyImmediate(transform.GetChild(i).gameObject);

        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        // Outside a prefab the order is set right here. In one, Generate sorts the scene's instances afterwards, where they stand.
        var random = new System.Random(seed);
        for (int i = 0; i < puffCount; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float reach = Mathf.Pow((float)random.NextDouble(), centreBias);
            float height = ((float)random.NextDouble() * 2f - 1f) * heightSpread * (1f - 0.5f * reach);
            float size = Mathf.Lerp(minPuffSize, maxPuffSize, (float)random.NextDouble()) * (1f - rimShrink * reach);

            var puff = new GameObject(PuffPrefix + i, typeof(MeshFilter), typeof(MeshRenderer));
            puff.transform.SetParent(transform, false);
            // A puff's own half-width stays out of the hole too.
            float inner = holeRadius > 0f ? holeRadius + size * 0.5f : 0f;
            puff.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * Mathf.Lerp(inner, Mathf.Max(spread.x, inner), reach),
                height,
                Mathf.Sin(angle) * Mathf.Lerp(inner, Mathf.Max(spread.y, inner), reach));
            puff.transform.localScale = new Vector3(size, size, 1f);
            puff.GetComponent<MeshFilter>().sharedMesh = quad;
            var meshRenderer = puff.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = puffMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        if (gameObject.scene.IsValid() && !EditorSceneManager.IsPreviewScene(gameObject.scene))
            SortPuffs();
        EditorUtility.SetDirty(gameObject);
        if (gameObject.scene.IsValid() && !EditorSceneManager.IsPreviewScene(gameObject.scene))
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
}
