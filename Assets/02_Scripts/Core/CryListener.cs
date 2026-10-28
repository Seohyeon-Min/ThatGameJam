using System.Collections.Generic;
using UnityEngine;

// A living thing that reacts to the bird's cry (design doc §15.7). Each listener decides its own reaction range.
public abstract class CryListener : MonoBehaviour
{
    static readonly List<CryListener> all = new();

    protected abstract float CryRadius { get; }

    public static void Broadcast(Vector3 origin)
    {
        // Copy, because a reaction may enable or disable listeners.
        foreach (CryListener listener in all.ToArray())
        {
            if ((listener.transform.position - origin).sqrMagnitude <= listener.CryRadius * listener.CryRadius)
                listener.OnCry(origin);
        }
    }

    protected virtual void OnEnable() => all.Add(this);

    protected virtual void OnDisable() => all.Remove(this);

    protected abstract void OnCry(Vector3 origin);
}
