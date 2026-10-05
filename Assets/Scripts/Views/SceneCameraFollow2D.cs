using UnityEngine;

[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public class SceneCameraFollow2D : MonoBehaviour
{
    public Transform target;
    public float minX = -16f;
    public float maxX = 16f;
    public float fixedY = 0f;
    [Min(0f)] public float smoothTime = 0.12f;

    private Vector3 velocity;

    private void LateUpdate()
    {
        if (target == null) return;

        float targetX = Mathf.Clamp(target.position.x, minX, maxX);
        Vector3 desired = new Vector3(targetX, fixedY, transform.position.z);

        transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime)
            : desired;
    }
}
