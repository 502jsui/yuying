/// <summary>
/// 顶层游戏状态：对应“主界面 / 游戏中 / 暂停 / 设置 / 结束”几块。
/// </summary>
public enum GameState
{
    Boot,
    MainMenu,
    Playing,
    Paused,
    Settings,
    Ending
}

/// <summary>
/// 一轮之内的阶段顺序（对应文档“每轮发生什么”）。
/// </summary>
public enum TurnPhase
{
    None,
    RoundStart,
    SceneState,
    PlayerInput,
    ItemSettle,
    NpcAction,
    RoundEnd,
    GameOver
}

/// <summary>
/// 场景 / 区域标识。本次 Demo 只用一楼 + 后院，二楼预留。
/// </summary>
public enum AreaId
{
    None,
    FirstFloor,
    SecondFloor,
    Backyard
}

/// <summary>
/// 物品在场景中的位置状态。
/// </summary>
public enum ItemPosition
{
    /// <summary>还在主人手里 / 物品池里，没有放到场景中。</summary>
    OwnerHand,

    /// <summary>已经放到场景里的某个放置点上。</summary>
    Scene
}

/// <summary>
/// 物品类型。衣服、花盆会同时存在两件，必须靠 itemId 区分而不是类型。
/// </summary>
public enum ItemType
{
    Unknown,
    Clothes,
    FlowerPot,
    MedicineBag,
    ToolBag,
    WoodCarving,
    WoodenBox,
    Diary
}
