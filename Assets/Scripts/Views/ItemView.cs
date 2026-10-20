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
    private ItemArtEntry art;
    private Sprite activeArtSprite;

    private void Awake()
    {
        if (area == AreaId.None) area = AreaIdUtil.FromSceneName(gameObject.scene.name);
        if (spriteRenderer == null)
        {
            Transform body = transform.Find("Body");
            if (body != null) spriteRenderer = body.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }
        ItemArtConfig config = ItemArtConfig.Load();
        art = config != null ? config.Get(itemId) : null;
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
            if (point != null)
            {
                transform.position = point.AnchorPosition;
                Sprite side = point.surface == PlacementSurface.Ground && art != null ? art.sideSprite : null;
                ApplyArt(side);
            }
        }
        else if (visible)
        {
            ApplyArt(null);
        }
    }

    private void ApplyArt(Sprite preferredSide)
    {
        if (spriteRenderer == null || art == null) return;

        Sprite sprite = preferredSide != null ? preferredSide : art.frontSprite;
        if (sprite == null) return;
        if (activeArtSprite == sprite) return;
        activeArtSprite = sprite;

        spriteRenderer.sprite = sprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = art.sortingOrder;

        float spriteHeight = Mathf.Max(0.0001f, sprite.bounds.size.y);
        float scale = Mathf.Max(0.05f, art.worldHeight) / spriteHeight;
        spriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        spriteRenderer.transform.localPosition = new Vector3(
            0f,
            art.hangFromTop ? -art.worldHeight : 0f,
            0f);

        float worldWidth = sprite.bounds.size.x * scale;
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.size = new Vector2(worldWidth * 0.85f, art.worldHeight * 0.9f);
            box.offset = new Vector2(0f, art.hangFromTop ? -art.worldHeight * 0.5f : art.worldHeight * 0.5f);
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

    /// <summary>在 Scene 视图里画出"玩家能碰到它的横向范围"（高度不限制）。</summary>
    private void OnDrawGizmosSelected()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        float radius = player != null ? player.interactRadius : 0.8f;
        float halfWidth = 0.4f;

        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null) halfWidth = Mathf.Max(0.1f, collider.bounds.extents.x);

        float centerX = transform.position.x;
        float top = 5.4f;
        float bottom = -5.4f;

        Gizmos.color = new Color(1f, 1f, 0f, 0.9f);
        Gizmos.DrawLine(new Vector3(centerX - radius - halfWidth, bottom, 0f), new Vector3(centerX - radius - halfWidth, top, 0f));
        Gizmos.DrawLine(new Vector3(centerX + radius + halfWidth, bottom, 0f), new Vector3(centerX + radius + halfWidth, top, 0f));
    }
}
