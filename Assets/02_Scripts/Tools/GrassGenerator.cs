using UnityEngine;

// Scatters grass prefabs over the whole of its areas (ScatterGenerator), paths included. Starts denser and more even than flowers.
public class GrassGenerator : ScatterGenerator
{
    protected override Color GizmoColor => new(0.45f, 0.85f, 0.35f);

    // Editor only: the starting values when the component is added.
    void Reset()
    {
        density = 6f;
        minSpacing = 0.15f;
        clustering = 0.4f;
    }
}
