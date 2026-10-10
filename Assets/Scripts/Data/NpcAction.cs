/// <summary>
/// 住户能执行的行动（对应文档五、住户行为）。
/// </summary>
public enum NpcActionType
{
    /// <summary>闲逛：没有数值效果，只是表现层的走动。</summary>
    Wander,

    /// <summary>交流：只有老婆婆、大叔能主动做（母亲只能被动交流，小女孩不交流）。</summary>
    Talk,

    /// <summary>拿出东西：从物品池里放一件到场景。</summary>
    TakeOutItem,

    /// <summary>拿回东西：把自己在场景里的物品收回。</summary>
    TakeBackItem,

    /// <summary>小女孩移动东西：移动别人的物品，暖意 +18 并清掉温度。</summary>
    MoveItem
}

/// <summary>
/// 一条已经决定好的住户行动。纯数据，方便单测、回放，也方便表现层消费。
/// </summary>
public struct NpcActionRecord
{
    /// <summary>执行这条行动的住户。</summary>
    public ResidentId actor;

    public NpcActionType action;

    /// <summary>交流对象（只有 Talk 有意义）。</summary>
    public ResidentId target;

    /// <summary>涉及哪件物品（拿出 / 拿回 / 移动）。</summary>
    public string itemId;

    /// <summary>目标放置点（拿出 / 移动）。</summary>
    public AreaId area;
    public string placementId;
    public PlacementSurface surface;

    /// <summary>来自「惊吓」：这次交流让对方暖意减少。</summary>
    public bool contagion;

    /// <summary>来自「疑惑」：这次交流是去敲母亲的门。</summary>
    public bool motherKnock;

    /// <summary>这是本轮该住户的第几个行动（从 1 开始）。</summary>
    public int order;

    public bool IsValid
    {
        get { return action != NpcActionType.Wander || actor != ResidentId.Mother; }
    }

    public static NpcActionRecord None(ResidentId actor)
    {
        return new NpcActionRecord { actor = actor, action = NpcActionType.Wander, order = 0 };
    }
}
