using System;

[Serializable]
public class ItemData
{
    public string itemId;
    public ItemOwner owner;
    public bool hasTemperature;
    public bool isInPool;

    public ItemData(string id, ItemOwner owner)
    {
        this.itemId = id;
        this.owner = owner;
        this.hasTemperature = false;
        this.isInPool = true;
    }
}