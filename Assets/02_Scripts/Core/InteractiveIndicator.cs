using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// A looping yellow sparkle hinting that this Perchable still needs the bird (a device, not a decorative perch):
// plays while CanPerch is true, stops once it's been used or disabled.
// Adding this component auto-spawns the InteractiveSparkle prefab at the Perchable's PerchPoint and wires it up
// (Reset(), Editor-only) so no manual dragging is needed; it only fills sparkle in when empty.
[RequireComponent(typeof(Perchable))]
public class InteractiveIndicator : MonoBehaviour
{
    const string SparklePrefabPath = "Assets/03_Prefabs/InteractiveSparkle.prefab";

    [SerializeField] ParticleSystem sparkle;

    Perchable perchable;
    bool showing;

    void Awake() => perchable = GetComponent<Perchable>();

#if UNITY_EDITOR
    void Reset()
    {
        if (sparkle != null)
            return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SparklePrefabPath);
        if (prefab == null)
            return;
        Transform parent = GetComponent<Perchable>().PerchPoint;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        sparkle = instance.GetComponent<ParticleSystem>();
    }
#endif

    void Update()
    {
        bool shouldShow = perchable.CanPerch;
        if (shouldShow == showing)
            return;
        showing = shouldShow;
        if (showing)
            sparkle.Play();
        else
            sparkle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
