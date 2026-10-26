using UnityEngine;

// Blocks the child until the bird clears it.
public class Obstacle : MonoBehaviour
{
    public void Clear()
    {
        gameObject.SetActive(false);
    }
}
