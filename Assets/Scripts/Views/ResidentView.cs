using UnityEngine;

public class ResidentView : MonoBehaviour
{
    [Header("身份")]
    public ResidentId id;

    [Tooltip("这个 View 在哪个场景。留 None 时按场景名自动判断；住户停在别的区域时这份会自动隐藏。")]
    public AreaId area = AreaId.None;

    [Tooltip("他站在哪条地面线上（Y）。不要依赖场景实例位置，避免实例位置没生效时人在半空。")]
    public float groundY = -1.5f;

    [Header("引用")]
    public SpriteRenderer bodyRenderer;
    public DoorLightView doorLight;
    public SpriteRenderer doorLightRenderer;

    [Header("颜色配置")]
    public Color warmthLow = new Color(0.6f, 0.6f, 0.7f);
    public Color warmthHigh = new Color(1f, 0.85f, 0.5f);

    [Tooltip("按暖意给整个立绘染色。占位方块用它做状态提示；换成真美术后关掉（暖意反馈交给门缝光）。")]
    public bool tintBodyByWarmth = true;

    [HideInInspector] public ResidentData data;

    private Vector3 homePosition;
    private Collider2D[] colliders;
    private bool subscribed;

    /// <summary>这个场景里他「家门口」的位置（每轮回家 / 待在门里的落点）。</summary>
    public Vector3 HomePosition { get { return homePosition; } }

    /// <summary>
    /// 玩家现在能不能碰到他：
    /// 必须在这个场景里（不在场景的不能碰）、已经出门（在门里看不见也碰不到）、
    /// 且处于「区域持续」（不回家的那位）。
    /// </summary>
    public bool IsTouchable
    {
        get
        {
            if (data == null) return false;
            if (!gameObject.activeInHierarchy) return false;
            if (area != AreaId.None && data.currentArea != area) return false;
            if (!data.IsOutside) return false;
            return data.state == NpcState.StayOut;
        }
    }

    private void Awake()
    {
        if (area == AreaId.None) area = AreaIdUtil.FromSceneName(gameObject.scene.name);

        // 用显式地面高度，而不是场景里的 Y（实例位置可能是 0，导致人浮在半空）。
        homePosition = new Vector3(transform.position.x, groundY, transform.position.z);
        transform.position = homePosition;
    }

    private void OnEnable()
    {
        Subscribe();
        RefreshVisual();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        EventBus.Subscribe<NpcStateChangedEvent>(OnNpcStateChanged);
        EventBus.Subscribe<NpcMovedEvent>(OnNpcMoved);
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        EventBus.Unsubscribe<NpcStateChangedEvent>(OnNpcStateChanged);
        EventBus.Unsubscribe<NpcMovedEvent>(OnNpcMoved);
        subscribed = false;
    }

    private void OnNpcStateChanged(NpcStateChangedEvent evt)
    {
        if (evt.id == id) RefreshVisual();
    }

    private void OnNpcMoved(NpcMovedEvent evt)
    {
        if (evt.id == id) RefreshVisual();
    }

    /// <summary>由 GameManager 在场景绑定时调用。</summary>
    public void Bind(ResidentData residentData)
    {
        data = residentData;
        ApplyPersistentPosition();
        RefreshVisual();
    }

    /// <summary>按数据里的横向位置还原站位（Y 用本场景门口的高度）。</summary>
    private void ApplyPersistentPosition()
    {
        if (data == null || !data.hasCurrentX) return;
        if (data.currentArea != AreaId.None && data.currentArea != area) return;

        Vector3 position = transform.position;
        // currentX 是「本区域内的局部 X」，换世界坐标要加上拼接偏移。
        position.x = SceneStitcher.WorldX(area, data.currentX);
        position.y = homePosition.y;
        transform.position = position;
    }

    public void RefreshVisual()
    {
        if (data == null) return;

        bool visible = IsPresentInThisArea();
        SyncColliderToBody();
        SetCollidersEnabled(visible);
        if (bodyRenderer) bodyRenderer.enabled = visible;

        // 位置防漂移：永远站回自己场景的门口高度（拼接 / 换场景都不会抬高）。
        Vector3 fixedPosition = transform.position;
        if (!Mathf.Approximately(fixedPosition.y, homePosition.y))
        {
            fixedPosition.y = homePosition.y;
            transform.position = fixedPosition;
        }

        if (!visible) return;

        float t = Mathf.Clamp01(data.warmth / 60f);
        if (bodyRenderer && tintBodyByWarmth) bodyRenderer.color = Color.Lerp(warmthLow, warmthHigh, t);
        if (doorLight != null) doorLight.Apply(data.warmth, data.awareness, data.hasAwareness);
    }

    /// <summary>
    /// 住户现在在不在这个场景：
    /// 正常 / 出门行动 / 疑惑 / 惊吓 → 在自己家（homeArea）；
    /// 区域持续 → 停在他占着的那个区域（stayOutArea）。
    /// </summary>
    private bool IsPresentInThisArea()
    {
        if (area == AreaId.None) return true;
        if (!data.IsOutside) return false; // 在家 = 在门里面

        AreaId current = data.currentArea != AreaId.None ? data.currentArea : data.homeArea;
        if (current == AreaId.None) return true;
        return current == area;
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (colliders == null) colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = enabled;
        }
    }

    /// <summary>让碰撞体严格等于模型大小 / 位置，避免「模型和判定不一致」。</summary>
    private void SyncColliderToBody()
    {
        if (bodyRenderer == null) return;

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null) return;

        // 判定框 = 立绘的实际范围（含枢轴偏移）。占位方块是 1x1 单位、中心枢轴，结果和以前一致。
        Transform body = bodyRenderer.transform;
        Vector3 lossy = body.lossyScale;
        Vector2 size;
        Vector3 centerWorld;

        if (bodyRenderer.sprite != null)
        {
            Bounds bounds = bodyRenderer.sprite.bounds;
            size = new Vector2(Mathf.Abs(bounds.size.x * lossy.x), Mathf.Abs(bounds.size.y * lossy.y));
            centerWorld = body.position + Vector3.Scale(lossy, bounds.center);
        }
        else
        {
            size = new Vector2(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y));
            centerWorld = body.position;
        }

        Vector3 offset = transform.InverseTransformPoint(centerWorld);

        if (!Mathf.Approximately(box.size.x, size.x) || !Mathf.Approximately(box.size.y, size.y))
        {
            box.size = size;
        }
        if (!Mathf.Approximately(box.offset.x, offset.x) || !Mathf.Approximately(box.offset.y, offset.y))
        {
            box.offset = offset;
        }
    }

    /// <summary>在 Scene 视图里画出"判定"位置，方便对比模型和判定。</summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(new Vector3(transform.position.x, homePosition.y, 0f), 0.15f);
        Gizmos.DrawLine(
            new Vector3(transform.position.x, homePosition.y - 0.4f, 0f),
            new Vector3(transform.position.x, homePosition.y + 0.4f, 0f));

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            new Vector3(transform.position.x, homePosition.y, 0f),
            new Vector3(0.9f, 2.2f, 0.1f));
    }
}
