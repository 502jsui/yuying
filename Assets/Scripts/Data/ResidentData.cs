using System;
using System.Collections.Generic;

[Serializable]
public class ResidentData
{
    public ResidentId id;
    public float warmth;
    public float awareness;
    public bool hasAwareness;

    public int actionMin = 2;
    public int actionMax = 3;

    public NpcState state = NpcState.Normal;
    public bool isHome = true;
    public bool isOut = false;

    /// <summary>他住哪个区域（决定「拿出东西」优先放在哪）。</summary>
    public AreaId homeArea = AreaId.FirstFloor;

    /// <summary>「区域持续」时停在哪（该区域本回合不可用）。</summary>
    public AreaId stayOutArea = AreaId.None;

    /// <summary>他此刻在哪个区域（跨场景移动后由 NpcSystem / NpcPhaseDriver 维护）。</summary>
    public AreaId currentArea = AreaId.FirstFloor;

    /// <summary>他在当前区域里的横向位置（跨场景回来时用来还原站位）。</summary>
    public float currentX;
    public bool hasCurrentX;

    /// <summary>他走路用的地面高度（两个场景统一一个值，跨接缝不会忽然升高 / 降低）。</summary>
    public float groundY;
    public bool hasGroundY;

    /// <summary>本轮已经执行了几个行动，由回合调度器重置。</summary>
    public int actionsTakenThisRound;

    /// <summary>是否刚被玩家强行触碰（用于“惊吓”与强碰传染）。</summary>
    public bool wasForceTouched;

    /// <summary>本次 Demo 母亲不做行动。</summary>
    public bool canAct = true;

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
        if (id == ResidentId.Mother)
        {
            canAct = false;
            actionMin = 0;
            actionMax = 0;
        }
    }

    /// <summary>每轮开始时重置只属于“本轮”的字段。</summary>
    public void BeginRound(bool home)
    {
        isHome = home;
        isOut = false;
        actionsTakenThisRound = 0;
    }

    /// <summary>
    /// 他此刻在不在门外。
    /// 在家 = 在门里面（不可见）；出门行动 / 区域持续 = 在外面（可见）。
    /// </summary>
    public bool IsOutside
    {
        get
        {
            return isOut || state == NpcState.StayOut || state == NpcState.GoingOut;
        }
    }
}
