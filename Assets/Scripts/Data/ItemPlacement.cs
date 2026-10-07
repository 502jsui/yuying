using System;

/// <summary>
/// 放置面类型，对应设计文档 2.6「物品放置规则」。
/// 一个放置点可以同时属于多种面（Flags），例如「别的物品上」同时算 OnItem。
/// </summary>
[Flags]
public enum PlacementSurface
{
    None = 0,

    /// <summary>地面。</summary>
    Ground = 1 << 0,

    /// <summary>晾衣架 / 挂钩一类只能挂衣服的地方。</summary>
    HangingRack = 1 << 1,

    /// <summary>叠放在别的物品上。</summary>
    OnItem = 1 << 2,

    Any = Ground | HangingRack | OnItem
}

/// <summary>
/// 物品类型 → 允许放哪儿。纯规则函数，方便单测与配置校验。
///
/// 对应文档：
///   衣服        → 晾衣架，不能放地上
///   花盆        → 地上、别的物品上
///   药包/工具包/木雕/日记 → 同花盆，但不能放地上
///   木箱        → 同花盆，但只可放地上
/// </summary>
public static class ItemPlacementRules
{
    public static PlacementSurface GetAllowedSurfaces(ItemType type)
    {
        switch (type)
        {
            case ItemType.Clothes:
                return PlacementSurface.HangingRack;

            case ItemType.FlowerPot:
                return PlacementSurface.Ground | PlacementSurface.OnItem;

            case ItemType.MedicineBag:
            case ItemType.ToolBag:
            case ItemType.WoodCarving:
            case ItemType.Diary:
                return PlacementSurface.OnItem;

            case ItemType.WoodenBox:
                return PlacementSurface.Ground;

            case ItemType.Unknown:
            default:
                // 还没配置类型的物品先不卡住，避免接表现时被规则拦住。
                return PlacementSurface.Any;
        }
    }

    public static bool CanPlace(ItemType type, PlacementSurface surface)
    {
        if (surface == PlacementSurface.None) return false;
        PlacementSurface allowed = GetAllowedSurfaces(type);
        return (allowed & surface) != 0;
    }

    public static string DescribeAllowed(ItemType type)
    {
        PlacementSurface allowed = GetAllowedSurfaces(type);
        if (allowed == PlacementSurface.Any) return "任意放置点";

        string text = "";
        AppendSurface(ref text, allowed, PlacementSurface.Ground, "地面");
        AppendSurface(ref text, allowed, PlacementSurface.HangingRack, "晾衣架");
        AppendSurface(ref text, allowed, PlacementSurface.OnItem, "别的物品上");
        return string.IsNullOrEmpty(text) ? "不能放置" : text;
    }

    private static void AppendSurface(ref string text, PlacementSurface allowed, PlacementSurface flag, string label)
    {
        if ((allowed & flag) == 0) return;
        if (text.Length > 0) text += " / ";
        text += label;
    }
}
