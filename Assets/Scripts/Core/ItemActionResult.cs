/// <summary>
/// 物品操作（碰 / 拿出 / 拿回 / 移动）的结果状态。
/// UI 可以根据状态决定弹什么提示；玩法系统可以据此判断要不要继续。
/// </summary>
public enum ItemActionStatus
{
    Success,

    /// <summary>请求本身不合法（例如不是这个东西的主人）。</summary>
    Rejected,

    ItemNotFound,

    /// <summary>物品还在物品池 / 主人手里，不在场景里。</summary>
    NotInScene,

    /// <summary>已经被拿回，不会再次出现。</summary>
    AlreadyReturned,

    /// <summary>已经有温度：提示「你已经触摸过了」。</summary>
    AlreadyTouched,

    /// <summary>物品池里没有可以拿出来的东西了。</summary>
    NoItemInPool,

    /// <summary>放置点不满足该物品的放置规则。</summary>
    PlacementBlocked,

    /// <summary>现在不是玩家输入阶段。</summary>
    NotPlayerInputPhase,

    /// <summary>本轮已经碰过了。</summary>
    TouchUsedUpThisRound,

    /// <summary>该区域被「区域持续」的住户占着，第 1、2 次只给警告。</summary>
    ForceTouchWarned,

    /// <summary>该区域被占，但强碰次数用尽，这次真的碰到了。</summary>
    ForceTouchAccepted
}

/// <summary>
/// 物品操作结果。带一个可以直接显示给玩家的 message。
/// </summary>
public struct ItemActionResult
{
    public ItemActionStatus status;
    public string itemId;
    public string message;

    public bool IsSuccess
    {
        get { return status == ItemActionStatus.Success; }
    }

    /// <summary>是否真的改变了物品状态（警告不算）。</summary>
    public bool ChangedState
    {
        get
        {
            return status == ItemActionStatus.Success
                || status == ItemActionStatus.ForceTouchAccepted;
        }
    }

    public static ItemActionResult Ok(string itemId, ItemActionStatus status, string message)
    {
        return new ItemActionResult { itemId = itemId, status = status, message = message };
    }

    public static ItemActionResult Fail(string itemId, ItemActionStatus status, string message)
    {
        return new ItemActionResult { itemId = itemId, status = status, message = message };
    }
}
