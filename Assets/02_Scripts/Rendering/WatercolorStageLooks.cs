using System;
using UnityEngine;

// Gives each stage its own watercolor / gouache look. When GameFlow moves to another stage, the scene's
// WatercolorManager blends to that stage's WatercolorLook over Blend Seconds (numbers and colours blend, switches flip
// halfway). A stage without a look keeps the current one. Put it next to the WatercolorManager.
public class WatercolorStageLooks : MonoBehaviour
{
    [Serializable]
    public struct Entry
    {
        public Stage stage;
        public WatercolorLook look;
    }

    [SerializeField] Entry[] looks = { };
    [SerializeField, Min(0f)] float blendSeconds = 2f;

    WatercolorManager manager;
    Stage current;
    WatercolorManager.Settings from;
    WatercolorLook to;
    float blend = 1f;

    void Start()
    {
        manager = GetComponent<WatercolorManager>() ?? FindAnyObjectByType<WatercolorManager>();
    }

    void Update()
    {
        if (manager == null || GameFlow.Instance == null)
            return;
        Stage stage = GameFlow.Instance.CurrentStage;
        if (stage != current)
        {
            bool first = current == null;
            current = stage;
            WatercolorLook look = LookFor(stage);
            if (look != null)
            {
                from = JsonUtility.FromJson<WatercolorManager.Settings>(JsonUtility.ToJson(manager.settings));
                to = look;
                // The first stage starts in its look at once.
                blend = first || blendSeconds <= 0f ? 1f : 0f;
                if (blend >= 1f)
                {
                    manager.Blend(from, to.settings, 1f);
                    to = null;
                }
            }
        }
        if (to == null)
            return;
        blend = Mathf.Min(1f, blend + Time.deltaTime / blendSeconds);
        manager.Blend(from, to.settings, Mathf.SmoothStep(0f, 1f, blend));
        if (blend >= 1f)
            to = null;
    }

    WatercolorLook LookFor(Stage stage)
    {
        foreach (Entry entry in looks)
            if (entry.stage == stage)
                return entry.look;
        return null;
    }
}
