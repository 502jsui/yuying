using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 4f;
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("触碰范围只看左右（横向），高度不限制。")]
    public float interactRadius = 1.2f;

    [Tooltip("旧字段，保留兼容；实际按场景里的 ItemView 判定。")]
    public LayerMask itemLayer;

    [Header("脚步")]
    [Tooltip("每走这么多世界单位播一次脚步。")]
    public float footstepInterval = 1.6f;
    [Range(0f, 1f)] public float footstepVolume = 0.6f;

    [Header("兜底提示")]
    [Tooltip("按 E 时，离门这么近就算「对着门」。")]
    public float doorRange = 1.6f;

    [Header("朝向")]
    [Tooltip("立绘渲染器；留空时自动找子物体 Body。走路时按左右方向翻转。")]
    public SpriteRenderer bodyRenderer;

    [Tooltip("走路时是否按左右方向翻转立绘。由角色美术配置写入。")]
    public bool flipWithMovement = true;

    private bool hintShown;
    private float stepDistance;
    private bool facingLeft;

    private readonly List<float> doorXs = new List<float>();
    private string cachedSceneName;

    /// <summary>现在朝左吗（纯表现，不影响判定）。</summary>
    public bool FacingLeft { get { return facingLeft; } }

    void Awake()
    {
        if (bodyRenderer != null) return;

        Transform body = transform.Find("Body");
        if (body != null) bodyRenderer = body.GetComponent<SpriteRenderer>();
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>
    /// 按左右输入算朝向：往左走朝左、往右走朝右，没有输入时保持原朝向（纯函数，方便单测）。
    /// </summary>
    public static bool ResolveFacing(float horizontal, bool currentFacingLeft)
    {
        if (Mathf.Abs(horizontal) < 0.01f) return currentFacingLeft;
        return horizontal < 0f;
    }

    /// <summary>把朝向表现刷到立绘上（翻转只影响表现，不参与判定）。</summary>
    public void ApplyFacing(bool faceLeft)
    {
        facingLeft = faceLeft;
        if (bodyRenderer != null) bodyRenderer.flipX = faceLeft;
    }

    void Start()
    {
        // 从暂停界面回来时，把玩家放回他点暂停时站的位置（只消费一次）。
        float resumeX;
        if (!PauseSnapshot.ConsumePlayerX(out resumeX)) return;

        Vector3 position = transform.position;
        position.x = resumeX;
        transform.position = position;
    }

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
        if (NarrationState.IsBlockingInput)
        {
            // 开场独白 / A1 期间不能动
            UpdateHint(false);
            return;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float dx = h * moveSpeed * Time.deltaTime;
        transform.position += new Vector3(dx, 0, 0);

        bool nextFacingLeft = ResolveFacing(h, facingLeft);
        if (flipWithMovement && nextFacingLeft != facingLeft) ApplyFacing(nextFacingLeft);

        if (Mathf.Abs(dx) > 0f)
        {
            MarkFirstMove(root);
            TickFootstep(root, Mathf.Abs(dx));
        }

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
        else
        {
            // 什么都没碰到：给兜底提示（D1 / D2 / D3）
            ShowStuckHint(root);
        }
    }

    private void MarkFirstMove(GameRoot root)
    {
        if (root == null || root.Session == null) return;
        if (root.Session.tutorialFirstMoveDone) return;
        root.Session.tutorialFirstMoveDone = true;
    }

    private void TickFootstep(GameRoot root, float distance)
    {
        stepDistance += distance;
        if (stepDistance < Mathf.Max(0.1f, footstepInterval)) return;
        stepDistance = 0f;

        if (root == null || root.Audio == null) return;

        bool outdoor = root.Session != null && root.Session.playerArea == AreaId.Backyard;
        root.Audio.PlaySfx(outdoor ? SfxId.FootstepConcrete : SfxId.FootstepWood, footstepVolume);
    }

    private void ShowStuckHint(GameRoot root)
    {
        if (root == null || root.Narration == null) return;

        switch (ResolveStuckHint(IsDoorInRange(), HasTouchableItemInArea()))
        {
            case StuckHint.D2:
                root.Narration.ShowD2();
                break;
            case StuckHint.D1:
                root.Narration.ShowD1();
                break;
            default:
                root.Narration.ShowD3();
                break;
        }
    }

    public enum StuckHint
    {
        None,
        D1,
        D2,
        D3
    }

    /// <summary>按 E 什么都没碰到时该给哪条兜底提示（纯函数，方便单测）。</summary>
    public static StuckHint ResolveStuckHint(bool doorInRange, bool itemInArea)
    {
        if (doorInRange) return StuckHint.D2;
        if (itemInArea) return StuckHint.D1;
        return StuckHint.D3;
    }

    /// <summary>当前区域里有没有"理论上能碰"的物品（用来区分 D1 和 D3）。</summary>
    private static bool HasTouchableItemInArea()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || manager.itemViews == null) return false;

        for (int i = 0; i < manager.itemViews.Length; i++)
        {
            ItemView view = manager.itemViews[i];
            if (view != null && view.IsTouchable) return true;
        }
        return false;
    }

    /// <summary>玩家是不是正站在一扇门前面（门在一楼，名字是 Door_XXX）。</summary>
    private bool IsDoorInRange()
    {
        CacheDoorsIfNeeded();
        for (int i = 0; i < doorXs.Count; i++)
        {
            if (Mathf.Abs(doorXs[i] - transform.position.x) <= doorRange) return true;
        }
        return false;
    }

    private void CacheDoorsIfNeeded()
    {
        string sceneName = gameObject.scene.name;
        if (cachedSceneName == sceneName) return;

        cachedSceneName = sceneName;
        doorXs.Clear();

        GameObject[] roots = gameObject.scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < all.Length; j++)
            {
                if (all[j] != null && all[j].name.StartsWith("Door_")) doorXs.Add(all[j].position.x);
            }
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
