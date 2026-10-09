using System;

[Serializable]
public class ItemData
{
    public string itemId;
    public ItemOwner owner;
    public ItemType type = ItemType.Unknown;

    /// <summary>物品是在主人手里（物品池）还是已经放到场景中。</summary>
    public ItemPosition position = ItemPosition.OwnerHand;

    /// <summary>物品所在区域，只有 position == Scene 时才有意义。</summary>
    public AreaId area = AreaId.None;

    /// <summary>放置点 ID（对应场景里的 PlacementPoint），用于对接表现层。</summary>
    public string placementId;

    public bool hasTemperature;

    /// <summary>是否还在物品池里、还没被拿出来。</summary>
    public bool isInPool;

    /// <summary>被拿回之后不再出现。</summary>
    public bool isReturned;

    public ItemData(string id, ItemOwner owner)
    {
        this.itemId = id;
        this.owner = owner;
        this.hasTemperature = false;
        this.isInPool = true;
        this.isReturned = false;
        this.position = ItemPosition.OwnerHand;
        this.area = AreaId.None;
    }

    /// <summary>物品是否可以被玩家触碰：已经在场景里，且没有被收回。</summary>
    public bool IsTouchable
    {
        get { return position == ItemPosition.Scene && !isReturned; }
    }

    /// <summary>是否已经放在场景中。</summary>
    public bool IsInScene
    {
        get { return position == ItemPosition.Scene; }
    }
}
