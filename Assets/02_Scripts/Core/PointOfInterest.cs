using System.Collections.Generic;
using UnityEngine;

// Something nearby worth a curious glance while walking (a tree, a stump, a fallen log) — not a
// generic "look around at nothing", but the baby noticing a specific thing (design doc: 아기의 호기심).
// The bird is deliberately never one of these: looking up at the bird is a separate, reserved gesture
// meaning "the bird helped me" (design doc §4.1), and must stay the only thing that means that.
public class PointOfInterest : MonoBehaviour
{
    static readonly List<PointOfInterest> all = new();

    // Relative pick weight against other POIs equally close, so a landmark (a log, a stump) can stand
    // out from a crowd of lookalike trees without excluding the trees entirely.
    [SerializeField] float weight = 1f;
    // Roughly where to look, not the object's pivot (a tree's pivot is usually at the ground).
    [SerializeField] Vector3 lookOffset = new(0f, 1.2f, 0f);

    public static IReadOnlyList<PointOfInterest> All => all;
    public float Weight => weight;
    public Vector3 LookPoint => transform.position + lookOffset;

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);
}
