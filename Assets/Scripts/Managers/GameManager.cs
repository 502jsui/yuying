using UnityEngine;

/// <summary>
/// 场景绑定器：每个游戏场景放一个。
/// 它只负责把本场景里的 View 接到 GameRoot 持有的持久数据上，
/// 自己不再持有游戏数据，所以切场景不会丢进度。
/// </summary>
[DefaultExecutionOrder(-500)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("场景引用")]
    public PlayerController player;
    public ResidentView[] residentViews;
    public ItemView[] itemViews;

    [Header("区域")]
    [Tooltip("留 None 时会根据场景名自动判断。")]
    public AreaId area = AreaId.None;

    [HideInInspector] public ResidentData[] residents;
    [HideInInspector] public ItemData[] items;

    public AreaId Area { get { return area; } }

    private void Awake()
    {
        // 拼接副本（NPC 行动阶段加进来的另一个场景）只负责显示，不接管当前场景。
        if (SceneStitcher.IsLoadingDuplicate)
        {
            enabled = false;
            return;
        }

        Instance = this;
        if (area == AreaId.None) area = AreaIdUtil.FromSceneName(gameObject.scene.name);
    }

    private void OnEnable()
    {
        if (SceneStitcher.IsLoadingDuplicate) return;
        if (GameRoot.Instance != null) GameRoot.Instance.RegisterScene(this);
    }

    private void OnDisable()
    {
        if (GameRoot.Instance != null) GameRoot.Instance.UnregisterScene(this);
        if (Instance == this) Instance = null;
    }

    /// <summary>把本场景的 View 与全局 Session 数据重新绑定。</summary>
    public void Bind()
    {
        GameRoot root = GameRoot.Instance;
        if (root == null || root.Session == null)
        {
            Debug.LogWarning("[GameManager] GameRoot 尚未就绪，本次绑定跳过。");
            return;
        }

        GameSession session = root.Session;
        root.Session.playerArea = area;

        if (residentViews != null)
        {
            residents = new ResidentData[residentViews.Length];
            for (int i = 0; i < residentViews.Length; i++)
            {
                ResidentView view = residentViews[i];
                if (view == null) continue;
                ResidentData data = session.EnsureResident(view.id, root.Config);
                residents[i] = data;
                view.Bind(data);
            }
        }
        else
        {
            residents = new ResidentData[0];
        }

        if (itemViews != null)
        {
            items = new ItemData[itemViews.Length];
            for (int i = 0; i < itemViews.Length; i++)
            {
                ItemView view = itemViews[i];
                if (view == null) continue;
                ItemData data = session.EnsureItem(view.itemId, view.owner, view.type);
                items[i] = data;
                view.Bind(data);
            }
        }
        else
        {
            items = new ItemData[0];
        }
    }

    /// <summary>
    /// 玩家触碰物品的统一入口：先交给物品系统判断（温度 / 二次触碰 / 不可用区域），
    /// 再由物品系统决定要不要消耗「本轮碰一次」。
    /// </summary>
    public ItemActionResult OnPlayerTouch(ItemView item)
    {
        if (item == null)
            return ItemActionResult.Fail(null, ItemActionStatus.ItemNotFound, "没有碰到东西。");

        GameRoot root = GameRoot.Instance;
        if (root == null || root.Items == null)
            return ItemActionResult.Fail(item.itemId, ItemActionStatus.ItemNotFound, "物品系统还没准备好。");

        ItemActionResult result = root.Items.Touch(item.itemId);
        item.RefreshVisual();
        return result;
    }

    /// <summary>玩家触碰「区域持续」住户的统一入口。</summary>
    public ItemActionResult OnPlayerTouchResident(ResidentView resident)
    {
        if (resident == null)
            return ItemActionResult.Fail(null, ItemActionStatus.ItemNotFound, "没有碰到人。");

        GameRoot root = GameRoot.Instance;
        if (root == null || root.Items == null)
            return ItemActionResult.Fail(null, ItemActionStatus.ItemNotFound, "物品系统还没准备好。");

        ItemActionResult result = root.Items.TouchResident(resident.id);
        resident.RefreshVisual();
        return result;
    }

    public ResidentData GetResidentData(ResidentId id)
    {
        GameRoot root = GameRoot.Instance;
        return root != null && root.Session != null ? root.Session.GetResident(id) : null;
    }
}
