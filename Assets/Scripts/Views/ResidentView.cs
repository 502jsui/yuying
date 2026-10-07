using UnityEngine;

public class ResidentView : MonoBehaviour
{
    [Header("身份")]
    public ResidentId id;

    [Tooltip("这个 View 在哪个场景。留 None 时按场景名自动判断；住户停在别的区域时这份会自动隐藏。")]
    public AreaId area = AreaId.None;

    [Header("引用")]
    public SpriteRenderer bodyRenderer;
    public DoorLightView doorLight;
    public SpriteRenderer doorLightRenderer;

    [Header("颜色配置")]
    public Color warmthLow = new Color(0.6f, 0.6f, 0.7f);
    public Color warmthHigh = new Color(1f, 0.85f, 0.5f);

    [HideInInspector] public ResidentData data;

    private Vector3 homePosition;
    private Collider2D[] colliders;
    private bool subscribed;

    /// <summary>这个场景里他「家门口」的位置（每轮回家 / 待在门里的落点）。</summary>
    public Vector3 HomePosition { get { return homePosition; } }

    private void Awake()
    {
        if (area == AreaId.None) area = AreaIdUtil.FromSceneName(gameObject.scene.name);
        homePosition = transform.position;
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
        position.x = data.currentX;
        position.y = homePosition.y;
        transform.position = position;
    }

    public void RefreshVisual()
    {
        if (data == null) return;

        bool visible = IsPresentInThisArea();
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
        if (bodyRenderer) bodyRenderer.color = Color.Lerp(warmthLow, warmthHigh, t);
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
}
