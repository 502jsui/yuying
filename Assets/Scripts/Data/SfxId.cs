/// <summary>
/// 游戏里会用到的音效种类。实际 AudioClip 配在 Resources/SfxLibrary.asset 里，
/// 换素材只需要改资产，不用改代码。
/// </summary>
public enum SfxId
{
    None,

    /// <summary>玩家触碰物品成功。</summary>
    Touch,

    /// <summary>物品被放到场景里（住户「拿出东西」）。</summary>
    ItemPlace,

    /// <summary>物品被收走 / 被小女孩移动。</summary>
    ItemTake,

    /// <summary>住户开门出门。</summary>
    DoorOpen,

    /// <summary>住户关门回家。</summary>
    DoorClose,

    /// <summary>玩家脚步（一楼 / 室内）。</summary>
    FootstepWood,

    /// <summary>玩家脚步（后院 / 室外）。</summary>
    FootstepConcrete,

    /// <summary>玩家脚步（备用：地毯）。</summary>
    FootstepCarpet,

    /// <summary>强行触碰的警告音。</summary>
    Warn,

    /// <summary>较柔和的一下（强碰生效 / 轻提示）。</summary>
    WarnSoft
}
