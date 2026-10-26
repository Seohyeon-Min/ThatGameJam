using UnityEngine;

// Fixed-angle quarter-view camera that smoothly follows a target.
public class QuarterViewCamera : MonoBehaviour
{
    [SerializeField] Transform target;
    // Seen from behind-right of the road, so the +Z road runs from lower-left to upper-right on screen (director's sketch, image.png).
    [SerializeField] Vector3 offset = new Vector3(6.5f, 6f, -6.5f);
    [SerializeField] float followSharpness = 5f;

    void Start()
    {
        transform.rotation = Quaternion.LookRotation(-offset);
        transform.position = target.position + offset;
    }

    void LateUpdate()
    {
        float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, target.position + offset, t);
    }
}
