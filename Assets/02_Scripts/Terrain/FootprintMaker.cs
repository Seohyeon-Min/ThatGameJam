using UnityEngine;

// Put on a creature's root and list its foot bones. Each foot leaves a print on the snow where it lands.
// Reads only the final pose of the bones, with no animation events: each foot keeps measuring how high it swings
// relative to the root, so clips can change without touching this. Feet that don't swing (idle, asleep) leave nothing.
public class FootprintMaker : MonoBehaviour
{
    // How fast the measured lowest and highest foot heights forget old values, in metres per second.
    const float MeasureDrift = 0.01f;
    // Shares of the measured swing: a foot lands below the first, and counts as lifted again above the second.
    const float LandShare = 0.25f;
    const float LiftShare = 0.6f;

    struct Foot
    {
        public float lowest;
        public float highest;
        public bool lifted;
        public bool hasPrint;
        public Vector3 lastPrint;
    }

    [SerializeField] Transform[] feet = { };
    [Tooltip("Print width and length, in metres.")]
    [SerializeField] Vector2 printSize = new(0.11f, 0.2f);
    [Tooltip("How far ahead of the foot bone the print's centre sits, in metres.")]
    [SerializeField] float forwardOffset = 0.04f;
    [Tooltip("A foot swinging less than this, in metres, isn't walking. Lower it for small creatures.")]
    [SerializeField, Min(0.001f)] float minSwing = 0.012f;
    [Tooltip("A foot landing this close to its last print, in metres, leaves no new one.")]
    [SerializeField, Min(0f)] float minStep = 0.05f;
    [Tooltip("A foot higher than this over the snow, in metres, leaves no print (on a log or a beam).")]
    [SerializeField, Min(0f)] float maxAboveSnow = 0.25f;

    Foot[] states;

    void Start()
    {
        states = new Foot[feet.Length];
        for (int i = 0; i < feet.Length; i++)
        {
            float height = feet[i].position.y - transform.position.y;
            states[i].lowest = height;
            states[i].highest = height;
        }
    }

    // After the animator has posed the bones.
    void LateUpdate()
    {
        for (int i = 0; i < feet.Length; i++)
            UpdateFoot(ref states[i], feet[i]);
    }

    void UpdateFoot(ref Foot state, Transform foot)
    {
        float height = foot.position.y - transform.position.y;
        float drift = MeasureDrift * Time.deltaTime;
        state.lowest = Mathf.Min(state.lowest + drift, height);
        state.highest = Mathf.Max(state.highest - drift, height);

        float swing = state.highest - state.lowest;
        if (swing < minSwing)
            return;
        float raised = (height - state.lowest) / swing;
        if (state.lifted && raised < LandShare)
        {
            state.lifted = false;
            Land(ref state, foot);
        }
        else if (!state.lifted && raised > LiftShare)
        {
            state.lifted = true;
        }
    }

    void Land(ref Foot state, Transform foot)
    {
        if (FootprintSystem.Instance == null)
            return;
        if (state.hasPrint && (foot.position - state.lastPrint).sqrMagnitude < minStep * minStep)
            return;
        state.hasPrint = true;
        state.lastPrint = foot.position;
        Vector3 forward = transform.forward;
        FootprintSystem.Instance.Stamp(foot.position + forward * forwardOffset, forward, printSize, maxAboveSnow);
    }
}
