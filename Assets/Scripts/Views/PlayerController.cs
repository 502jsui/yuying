using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 4f;
    public KeyCode interactKey = KeyCode.E;
    public float interactRadius = 0.8f;
    public LayerMask itemLayer;

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        transform.position += new Vector3(h, 0, 0) * moveSpeed * Time.deltaTime;

        if (Input.GetKeyDown(interactKey))
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, interactRadius, itemLayer);
            foreach (var h2 in hits)
            {
                var item = h2.GetComponent<ItemView>();
                if (item != null) { /* 交给 GameManager 处理 */ break; }
            }
        }
    }
}