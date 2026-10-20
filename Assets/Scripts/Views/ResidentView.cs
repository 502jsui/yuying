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

    [Header("坐姿")]
    [Tooltip("老婆婆缝衣 / 大叔雕刻的坐姿立绘。留空时保持站立外观。")]
    public Sprite sittingSprite;
    public float sittingHeight = 1.8f;
    public float sittingFootOffsetY = -0.9f;
    public int sittingSortingOrder = 5;

    [Header("走路立绘")]
    public Sprite walkingSprite;
    public float walkingHeight = 2.3f;
    public float walkingFootOffsetY = -1.15f;
    public int walkingSortingOrder = 5;

    [Header("交流立绘")]
    public Sprite talkingSprite;
    public float talkingHeight = 2.3f;
    public float talkingFootOffsetY = -1.15f;
    public int talkingSortingOrder = 5;

    [Header("颜色配置")]
    public Color warmthLow = new Color(0.6f, 0.6f, 0.7f);
    public Color warmthHigh = new Color(1f, 0.85f, 0.5f);

    [Tooltip("按暖意给整个立绘染色。占位方块用它做状态提示；换成真美术后关掉（暖意反馈交给门缝光）。")]
    public bool tintBodyByWarmth = true;

    [HideInInspector] public ResidentData data;

    private Vector3 homePosition;
    private Collider2D[] colliders;
    private bool subscribed;
    private Sprite standingSprite;
    private Vector3 standingLocalScale;
    private Vector3 standingLocalPosition;
    private Color standingColor;
    private int standingSortingOrder;
    private bool poseCached;
    private bool customPoseActive;

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
        LoadPoseArtFromConfig();
        CacheStandingPose();
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
        EventBus.Subscribe<NpcPoseChangedEvent>(OnNpcPoseChanged);
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        EventBus.Unsubscribe<NpcStateChangedEvent>(OnNpcStateChanged);
        EventBus.Unsubscribe<NpcMovedEvent>(OnNpcMoved);
        EventBus.Unsubscribe<NpcPoseChangedEvent>(OnNpcPoseChanged);
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

    private void OnNpcPoseChanged(NpcPoseChangedEvent evt)
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
        position.y = homePosition.y + SceneStitcher.OffsetYFor(area);
        transform.position = position;
    }

    public void RefreshVisual()
    {
        if (data == null) return;

        bool sit = data.state == NpcState.Sitting && !data.isOut && sittingSprite != null;
        bool customPose = ApplyBodyPose(sit, data.visualPose);
        if (bodyRenderer) bodyRenderer.flipX = data.facingLeft;

        bool visible = IsPresentInThisArea();
        SyncColliderToBody();
        SetCollidersEnabled(visible);
        if (bodyRenderer) bodyRenderer.enabled = visible;

        // 位置防漂移：永远站回自己场景的门口高度（拼接 / 换场景都不会抬高）。
        Vector3 fixedPosition = transform.position;
        float targetY = homePosition.y + SceneStitcher.OffsetYFor(area);
        if (!Mathf.Approximately(fixedPosition.y, targetY))
        {
            fixedPosition.y = targetY;
            transform.position = fixedPosition;
        }

        if (!visible) return;

        float t = Mathf.Clamp01(data.warmth / 60f);
        if (bodyRenderer && tintBodyByWarmth && !customPose)
            bodyRenderer.color = Color.Lerp(warmthLow, warmthHigh, t);
        if (doorLight != null) doorLight.Apply(data.warmth, data.awareness, data.hasAwareness);
    }

    public void SetFacing(bool faceLeft)
    {
        if (data != null) data.facingLeft = faceLeft;
        if (bodyRenderer != null) bodyRenderer.flipX = faceLeft;
    }

    private void LoadPoseArtFromConfig()
    {
        CharacterArtConfig config = CharacterArtConfig.Load();
        if (config == null) return;

        CharacterArtEntry standing = config.ForResident(id);
        if (standing != null && standing.HasArt)
        {
            ApplyConfiguredStandingArt(standing);
            tintBodyByWarmth = false;
        }

        if (sittingSprite == null)
        {
            CharacterArtEntry entry = config.ForResidentSitting(id);
            if (entry != null && entry.HasArt)
            {
                sittingSprite = entry.sprite;
                sittingHeight = entry.height;
                sittingFootOffsetY = entry.footOffsetY;
                sittingSortingOrder = entry.sortingOrder;
            }
        }

        if (walkingSprite == null)
        {
            CharacterArtEntry walking = config.ForResidentWalking(id);
            if (walking != null && walking.HasArt)
            {
                walkingSprite = walking.sprite;
                walkingHeight = walking.height;
                walkingFootOffsetY = walking.footOffsetY;
                walkingSortingOrder = walking.sortingOrder;
            }
        }

        if (talkingSprite == null)
        {
            CharacterArtEntry talking = config.ForResidentTalking(id);
            if (talking != null && talking.HasArt)
            {
                talkingSprite = talking.sprite;
                talkingHeight = talking.height;
                talkingFootOffsetY = talking.footOffsetY;
                talkingSortingOrder = talking.sortingOrder;
            }
        }
    }

    private void ApplyConfiguredStandingArt(CharacterArtEntry entry)
    {
        if (bodyRenderer == null || entry == null || entry.sprite == null) return;

        Sprite sprite = entry.sprite;
        float spriteHeight = Mathf.Max(0.0001f, sprite.bounds.size.y);
        float scale = Mathf.Max(0.05f, entry.height) / spriteHeight;
        bodyRenderer.sprite = sprite;
        bodyRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        float bottom = sprite.bounds.min.y * scale;
        bodyRenderer.transform.localPosition =
            new Vector3(0f, entry.footOffsetY - bottom, 0f);
        bodyRenderer.color = Color.white;
        bodyRenderer.sortingOrder = entry.sortingOrder;
    }

    private void CacheStandingPose()
    {
        if (bodyRenderer == null || poseCached) return;
        standingSprite = bodyRenderer.sprite;
        standingLocalScale = bodyRenderer.transform.localScale;
        standingLocalPosition = bodyRenderer.transform.localPosition;
        standingColor = bodyRenderer.color;
        standingSortingOrder = bodyRenderer.sortingOrder;
        poseCached = true;
    }

    private bool ApplyBodyPose(bool sit, NpcVisualPose actionPose)
    {
        if (bodyRenderer == null) return false;
        CacheStandingPose();

        Sprite sprite = null;
        float height = 0f;
        float footOffset = 0f;
        int sortingOrder = standingSortingOrder;

        if (sit && sittingSprite != null)
        {
            sprite = sittingSprite;
            height = sittingHeight;
            footOffset = sittingFootOffsetY;
            sortingOrder = sittingSortingOrder;
        }
        else if (actionPose == NpcVisualPose.Walking && walkingSprite != null)
        {
            sprite = walkingSprite;
            height = walkingHeight;
            footOffset = walkingFootOffsetY;
            sortingOrder = walkingSortingOrder;
        }
        else if (actionPose == NpcVisualPose.Talking && talkingSprite != null)
        {
            sprite = talkingSprite;
            height = talkingHeight;
            footOffset = talkingFootOffsetY;
            sortingOrder = talkingSortingOrder;
        }

        if (sprite != null)
        {
            customPoseActive = true;
            bodyRenderer.sprite = sprite;
            float spriteHeight = Mathf.Max(0.0001f, sprite.bounds.size.y);
            float scale = Mathf.Max(0.05f, height) / spriteHeight;
            bodyRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            float bottom = sprite.bounds.min.y * scale;
            bodyRenderer.transform.localPosition =
                new Vector3(standingLocalPosition.x, footOffset - bottom, standingLocalPosition.z);
            bodyRenderer.color = Color.white;
            bodyRenderer.sortingOrder = sortingOrder;
            return true;
        }

        if (customPoseActive)
        {
            customPoseActive = false;
            bodyRenderer.sprite = standingSprite;
            bodyRenderer.transform.localScale = standingLocalScale;
            bodyRenderer.transform.localPosition = standingLocalPosition;
            bodyRenderer.color = standingColor;
            bodyRenderer.sortingOrder = standingSortingOrder;
        }
        return false;
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
