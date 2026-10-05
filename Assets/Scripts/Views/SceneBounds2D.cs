using UnityEngine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class SceneBounds2D : MonoBehaviour
{
    public Transform target;
    public float minX = -16f;
    public float maxX = 16f;
    public float fixedY = -2.4f;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 p = target.position;
        p.x = Mathf.Clamp(p.x, minX, maxX);
        p.y = fixedY;
        target.position = p;
    }
}
