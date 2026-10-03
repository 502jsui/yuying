using System;
using System.Collections.Generic;

[Serializable]
public class ResidentData
{
    public ResidentId id;
    public float warmth;
    public float awareness;
    public bool hasAwareness;

    public List<ItemData> itemPool = new List<ItemData>();

    public int actionMin = 2;
    public int actionMax = 3;

    public NpcState state = NpcState.Normal;
    public bool isHome = true;
    public bool isOut = false;

    public ResidentData(ResidentId id, float warmth, float awareness, bool hasAwareness)
    {
        this.id = id;
        this.warmth = warmth;
        this.awareness = awareness;
        this.hasAwareness = hasAwareness;
        if (id == ResidentId.Girl)
        {
            actionMin = 1;
            actionMax = 2;
        }
    }
}