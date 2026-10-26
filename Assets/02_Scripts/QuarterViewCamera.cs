using UnityEngine;

// Fixed-angle quarter-view camera that smoothly follows a target.
public class QuarterViewCamera : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 offset = new Vector3(-8f, 10f, -8f);
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
