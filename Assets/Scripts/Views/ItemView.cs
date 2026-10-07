using UnityEngine;

public class ItemView : MonoBehaviour
{
    [Header("身份")]
    public string itemId;
    public ItemOwner owner;
    public ItemType type = ItemType.Unknown;

    [Tooltip("这件物品在哪个场景。留 None 时按场景名自动判断；跨场景移动时另一边的副本会自动隐藏。")]
    public AreaId area = AreaId.None;

    [Header("引用")]
    public SpriteRenderer spriteRenderer;
    public GameObject temperatureGlow;

    [Header("显示")]
    [Tooltip("数据表示“还在物品池 / 已收回”时，自动隐藏图片。碰撞体始终会被关掉，保证碰不到池里的东西。")]
    public bool hideWhenNotInScene = true;

    [HideInInspector] public ItemData data;

    private Collider2D[] colliders;
    private bool subscribed;

    private void Awake()
    {
        if (area == AreaId.None) area = AreaIdUtil.FromSceneName(gameObject.scene.name);
    }

    private void OnEnable()
    {
        Subscribe();
        RefreshVisual();
    }

    private void Start()
    {
        // PlacementPoint 在 OnEnable 注册，Start 时一定已经就绪，这里再吸附一次。
        RefreshVisual();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        EventBus.Subscribe<ItemTouchedEvent>(OnItemTouched);
        EventBus.Subscribe<ItemAppearedEvent>(OnItemAppeared);
        EventBus.Subscribe<ItemTakenEvent>(OnItemTaken);
        EventBus.Subscribe<ItemMovedEvent>(OnItemMoved);
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        EventBus.Unsubscribe<ItemTouchedEvent>(OnItemTouched);
        EventBus.Unsubscribe<ItemAppearedEvent>(OnItemAppeared);
        EventBus.Unsubscribe<ItemTakenEvent>(OnItemTaken);
        EventBus.Unsubscribe<ItemMovedEvent>(OnItemMoved);
        subscribed = false;
    }

    private void OnItemTouched(ItemTouchedEvent evt)
    {
        if (evt.itemId == itemId) RefreshVisual();
    }

    private void OnItemAppeared(ItemAppearedEvent evt)
    {
        if (evt.itemId == itemId) RefreshVisual();
    }

    private void OnItemTaken(ItemTakenEvent evt)
    {
        if (evt.itemId == itemId) RefreshVisual();
    }

    private void OnItemMoved(ItemMovedEvent evt)
    {
        if (evt.itemId == itemId) RefreshVisual();
    }

    /// <summary>由 GameManager 在场景绑定时调用。</summary>
    public void Bind(ItemData itemData)
    {
        data = itemData;
        RefreshVisual();
    }

    public void RefreshVisual()
    {
        if (data == null) return;

        // 同一件物品在每个场景各有一份 View，只有「数据所在区域 == 本场景」的那份显示。
        bool inThisArea = area == AreaId.None || data.area == AreaId.None || data.area == area;
        bool visible = data.IsInScene && !data.isReturned && inThisArea;

        // 不管显不显示，都不允许碰到池里 / 已收回的物品。
        SetCollidersEnabled(visible);

        if (hideWhenNotInScene && spriteRenderer != null) spriteRenderer.enabled = visible;
        if (temperatureGlow != null) temperatureGlow.SetActive(visible && data.hasTemperature);

        if (visible && !string.IsNullOrEmpty(data.placementId))
        {
            PlacementPoint point = PlacementPoint.Find(data.placementId);
            if (point != null) transform.position = point.AnchorPosition;
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (colliders == null) colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = enabled;
        }
    }

    /// <summary>玩家现在能不能碰到它（在场景里、没被收回、且在当前区域）。</summary>
    public bool IsTouchable
    {
        get
        {
            if (data == null) return false;
            if (!data.IsInScene || data.isReturned) return false;
            if (data.area != AreaId.None && area != AreaId.None && data.area != area) return false;

            // 显示不出来的（在池里 / 区域不对）一律不可碰，避免「看不见却能碰」。
            if (!gameObject.activeInHierarchy) return false;
            if (spriteRenderer != null && !spriteRenderer.enabled) return false;

            return true;
        }
    }
}
