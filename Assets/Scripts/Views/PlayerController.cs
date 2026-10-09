using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 4f;
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("触碰范围只看左右（横向），高度不限制。")]
    public float interactRadius = 1.2f;

    [Tooltip("旧字段，保留兼容；实际按场景里的 ItemView 判定。")]
    public LayerMask itemLayer;

    private bool hintShown;

    void Update()
    {
        GameRoot root = GameRoot.Instance;
        if (root != null && root.Flow != null && !root.Flow.CanPlayerMove)
        {
            UpdateHint(false);
            return;
        }
        if (SceneTransitionState.IsLoading)
        {
            UpdateHint(false);
            return;
        }

        float h = Input.GetAxisRaw("Horizontal");
        transform.position += new Vector3(h, 0, 0) * moveSpeed * Time.deltaTime;

        if (root != null && root.Flow != null && !root.Flow.CanPlayerInteract)
        {
            UpdateHint(false);
            return;
        }

        float itemDistance;
        ItemView nearbyItem = FindTouchableItem(out itemDistance);
        float residentDistance;
        ResidentView nearbyResident = FindTouchableResident(out residentDistance);

        UpdateHint(nearbyItem != null || nearbyResident != null);

        if (!Input.GetKeyDown(interactKey)) return;
        if (GameManager.Instance == null) return;

        // 人和物品都能碰，谁更近就碰谁。
        if (nearbyResident != null && (nearbyItem == null || residentDistance < itemDistance))
        {
            GameManager.Instance.OnPlayerTouchResident(nearbyResident);
        }
        else if (nearbyItem != null)
        {
            GameManager.Instance.OnPlayerTouch(nearbyItem);
        }
    }

    /// <summary>
    /// 找身边能碰的物品：**只比左右距离，高度不限制**
    /// （所以挂在高处晾衣架上的衣服，站在下面也能碰）。
    /// </summary>
    private ItemView FindTouchableItem(out float distance)
    {
        distance = float.MaxValue;

        GameManager manager = GameManager.Instance;
        if (manager == null || manager.itemViews == null) return null;

        ItemView best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < manager.itemViews.Length; i++)
        {
            ItemView view = manager.itemViews[i];
            if (view == null || !view.IsTouchable) continue;

            // 有效水平距离 = 中心距 − 物品自身半宽（站旁边就能碰到）。
            float halfWidth = GetHalfWidth(view);
            float dx = Mathf.Abs(view.transform.position.x - transform.position.x) - halfWidth;
            if (dx > interactRadius) continue;

            if (dx < bestDistance)
            {
                bestDistance = dx;
                best = view;
            }
        }

        distance = bestDistance;
        return best;
    }

    /// <summary>
    /// 找身边能碰的「区域持续」（不回家）住户。
    /// 不在当前场景 / 还在门里的住户，IsTouchable 会是 false，所以碰不到。
    /// </summary>
    private ResidentView FindTouchableResident(out float distance)
    {
        distance = float.MaxValue;

        GameManager manager = GameManager.Instance;
        if (manager == null || manager.residentViews == null) return null;

        ResidentView best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < manager.residentViews.Length; i++)
        {
            ResidentView view = manager.residentViews[i];
            if (view == null || !view.IsTouchable) continue;

            float halfWidth = GetHalfWidth(view);
            float dx = Mathf.Abs(view.transform.position.x - transform.position.x) - halfWidth;
            if (dx > interactRadius) continue;

            if (dx < bestDistance)
            {
                bestDistance = dx;
                best = view;
            }
        }

        distance = bestDistance;
        return best;
    }

    private static float GetHalfWidth(ResidentView view)
    {
        Collider2D collider = view.GetComponent<Collider2D>();
        if (collider != null) return Mathf.Max(0.1f, collider.bounds.extents.x);
        if (view.bodyRenderer != null) return Mathf.Max(0.1f, view.bodyRenderer.bounds.extents.x);
        return 0.4f;
    }

    private static float GetHalfWidth(ItemView view)
    {
        Collider2D collider = view.GetComponent<Collider2D>();
        if (collider != null) return Mathf.Max(0.1f, collider.bounds.extents.x);
        if (view.spriteRenderer != null) return Mathf.Max(0.1f, view.spriteRenderer.bounds.extents.x);
        return 0.4f;
    }

    /// <summary>可交互提示：靠近出现、离开消失。</summary>
    private void UpdateHint(bool available)
    {
        if (available == hintShown) return;

        hintShown = available;
        EventBus.Publish(new InteractHintEvent
        {
            available = available,
            itemId = null
        });
    }
}
