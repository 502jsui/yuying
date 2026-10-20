/// <summary>
/// NPC 状态机（对应文档 3.2）。纯规则函数，方便单测。
///
///   正常     在家，常规行动
///   坐着     老婆婆缝衣服 / 大叔雕刻；此时仍在场景里，但不占区域
///   出门行动  本轮外出做事（一次行动期间）
///   区域持续  行动后没回家，停在某个区域 → 该区域本回合不可用
///   疑惑     下次行动必去敲母亲的门
///   惊吓     下次行动必为交流，且让对方暖意减少
/// </summary>
public static class NpcStateRules
{
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

    /// <summary>这个住户有没有专属坐姿，只用于老婆婆和大叔。</summary>
    public static bool CanSit(ResidentId id)
    {
        return id == ResidentId.Grandma || id == ResidentId.Uncle;
    }

    /// <summary>一次外出行动结束、回家。</summary>
    public static NpcState OnReturnHome(NpcState current)
    {
        if (current == NpcState.GoingOut) return NpcState.Normal;
        return current;
    }

    /// <summary>疑惑 / 惊吓的那次行动做完之后，回到正常。</summary>
    public static NpcState AfterForcedAction(NpcState current)
    {
        if (current == NpcState.Confused || current == NpcState.Startled) return NpcState.Normal;
        return current;
    }

}
