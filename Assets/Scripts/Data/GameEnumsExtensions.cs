/// <summary>
/// 枚举之间的小工具转换，避免各处散落 switch。
/// </summary>
public static class GameEnumsExtensions
{
    public static ItemOwner ToItemOwner(this ResidentId id)
    {
        switch (id)
        {
            case ResidentId.Grandma: return ItemOwner.Grandma;
            case ResidentId.Uncle: return ItemOwner.Uncle;
            case ResidentId.Mother: return ItemOwner.Mother;
            default: return ItemOwner.None;
        }
    }

    public static ResidentId ToResidentId(this ItemOwner owner)
    {
        switch (owner)
        {
            case ItemOwner.Grandma: return ResidentId.Grandma;
            case ItemOwner.Uncle: return ResidentId.Uncle;
            case ItemOwner.Mother: return ResidentId.Mother;
            default: return ResidentId.Girl;
        }
    }

    public static string ToDisplayName(this ResidentId id)
    {
        switch (id)
        {
            case ResidentId.Grandma: return "老婆婆";
            case ResidentId.Uncle: return "大叔";
            case ResidentId.Mother: return "母亲";
            case ResidentId.Girl: return "小女孩";
            default: return id.ToString();
        }
    }

    public static string ToDisplayName(this AreaId area)
    {
        switch (area)
        {
            case AreaId.FirstFloor: return "一楼";
            case AreaId.SecondFloor: return "二楼";
            case AreaId.Backyard: return "后院";
            default: return area.ToString();
        }
    }

    public static string ToDisplayName(this ItemType type)
    {
        switch (type)
        {
            case ItemType.Clothes: return "衣服";
            case ItemType.FlowerPot: return "花盆";
            case ItemType.MedicineBag: return "药包";
            case ItemType.ToolBag: return "工具包";
            case ItemType.WoodCarving: return "木雕";
            case ItemType.WoodenBox: return "木箱";
            case ItemType.Diary: return "日记";
            default: return type.ToString();
        }
    }

    public static string ToDisplayName(this ItemOwner owner)
    {
        switch (owner)
        {
            case ItemOwner.Grandma: return "老婆婆";
            case ItemOwner.Uncle: return "大叔";
            case ItemOwner.Mother: return "母亲";
            default: return "无主";
        }
    }
}
