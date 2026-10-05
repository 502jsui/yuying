using UnityEngine;

[DisallowMultipleComponent]
public class NpcPatrol2D : MonoBehaviour
{
    public float minX;
    public float maxX;
    public float speed = 0.6f;
    public float direction = 1f;

    private void Update()
    {
        Vector3 position = transform.position;
        position.x += direction * speed * Time.deltaTime;

        if (position.x >= maxX)
        {
            position.x = maxX;
            direction = -1f;
        }
        else if (position.x <= minX)
        {
            position.x = minX;
            direction = 1f;
        }

        transform.position = position;
    }
}
