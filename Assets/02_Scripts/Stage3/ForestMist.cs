using UnityEngine;

// The forest mist over a fog section (ForestMistFeature draws it): the dream fog's quiet version. The scene stays as it is, with a clear
// blue-green mist lying low over the ground. It comes in as the bird goes into the fog, along the field's own ramp, as the dream fog does.
// Its look is all in the Look material (Hidden/ThatGameJam/ForestMist). Nothing is made or changed in the scene.
public class ForestMist : MonoBehaviour
{
    static readonly int WeightId = Shader.PropertyToID("_ForestMistWeight");
    static readonly int GroundId = Shader.PropertyToID("_ForestMistGround");

    // How far in the mist is now, for ForestMistFeature: 0 = not drawn at all.
    public static float Weight { get; private set; }
    // The material ForestMistFeature draws with: this one's Look, while it is on.
    public static Material Look { get; private set; }

    [SerializeField] FogField field;
    [Tooltip("How strong the mist is once fully in, 0 to 1.")]
    [SerializeField, Range(0f, 1f)] float strength = 1f;
    [Tooltip("Seconds it takes to follow the bird in or out of the fog.")]
    [SerializeField, Min(0.01f)] float ease = 1.5f;
    [Tooltip("ForestMist.mat (Hidden/ThatGameJam/ForestMist): colour, how low it lies, how thick, wisps, light, motes. Edit it in Play Mode and the change shows at once, and stays.")]
    [SerializeField] Material look;

    float weight;

    void OnEnable() => Look = look;

    void OnDisable()
    {
        if (Look == look)
            Look = null;
        weight = 0f;
        Weight = 0f;
        Shader.SetGlobalFloat(WeightId, 0f);
    }

    void LateUpdate()
    {
        float goal = 0f;
        if (field != null && BirdController.Instance != null)
            goal = field.RampAt(BirdController.Instance.transform.position) * strength;
        weight = Mathf.MoveTowards(weight, goal, Time.deltaTime / ease);
        Weight = Mathf.SmoothStep(0f, 1f, weight);
        Shader.SetGlobalFloat(WeightId, Weight);
        Shader.SetGlobalFloat(GroundId, field != null ? field.transform.position.y : 0f);
    }
}
