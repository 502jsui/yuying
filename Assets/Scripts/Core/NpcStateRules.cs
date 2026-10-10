/// <summary>
/// NPC 状态机（对应文档 3.2）。纯规则函数，方便单测。
///
///   正常     在家，常规行动
///   出门行动  本轮外出做事（一次行动期间）
///   区域持续  行动后没回家，停在某个区域 → 该区域本回合不可用
///   疑惑     下次行动必去敲母亲的门
///   惊吓     下次行动必为交流，且让对方暖意减少
/// </summary>
public static class NpcStateRules
{
    /// <summary>这个状态会不会占用区域（占用时该区域不可用，玩家强碰会触发警告）。</summary>
    public static bool BlocksArea(NpcState state)
    {
        return state == NpcState.StayOut;
    }

    /// <summary>这个状态能不能按常规权重随机行动。</summary>
    public static bool CanActNormally(NpcState state)
    {
        return state == NpcState.Normal || state == NpcState.GoingOut;
    }

    /// <summary>这个状态的下一次行动是不是被强制过的。</summary>
    public static bool IsForcedAction(NpcState state)
    {
        return state == NpcState.Confused || state == NpcState.Startled;
    }

    /// <summary>每轮开始：不回家 → 区域持续；否则回家 → 正常。疑惑 / 惊吓不会跨轮保留。</summary>
    public static NpcState OnRoundStart(bool absent)
    {
        return absent ? NpcState.StayOut : NpcState.Normal;
    }

    /// <summary>一次外出行动结束、回家。</summary>
    public static NpcState OnReturnHome(NpcState current)
    {
        if (current == NpcState.GoingOut) return NpcState.Normal;
        return current;
    }

    /// <summary>玩家强行触碰「区域持续」住户所在区域 → 惊吓。</summary>
    public static NpcState OnForceTouched(NpcState current)
    {
        return NpcState.Startled;
    }

    /// <summary>玩家碰了母亲的物品 → 疑惑（已经在惊吓中时保持惊吓）。</summary>
    public static NpcState OnMotherItemTouched(NpcState current)
    {
        return current == NpcState.Startled ? NpcState.Startled : NpcState.Confused;
    }

    /// <summary>疑惑 / 惊吓的那次行动做完之后，回到正常。</summary>
    public static NpcState AfterForcedAction(NpcState current)
    {
        if (current == NpcState.Confused || current == NpcState.Startled) return NpcState.Normal;
        return current;
    }

    /// <summary>下一轮自行回家：区域持续 → 正常。</summary>
    public static NpcState OnNextRound(NpcState current)
    {
        if (current == NpcState.StayOut) return NpcState.Normal;
        return current;
    }

    public static string Describe(NpcState state)
    {
        switch (state)
        {
            case NpcState.Normal: return "正常";
            case NpcState.GoingOut: return "出门行动";
            case NpcState.StayOut: return "区域持续";
            case NpcState.Confused: return "疑惑";
            case NpcState.Startled: return "惊吓";
            default: return state.ToString();
        }
    }
}
