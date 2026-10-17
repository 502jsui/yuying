using UnityEngine;

/// <summary>
/// 数值结算系统：**全局所有暖意 / 知晓度的加减法都集中在这里**，
/// 其他系统（物品、NPC 行为、玩家交互）只调用，不自己算。
///
/// 对应文档：物品状态 / 交流传播 / 母亲开门 / 强行触碰 / 参数汇总。
///
///   · 拿到有温度的东西  → 暖意 +18、知晓 +15
///   · 拿到没温度的东西  → 暖意 +0、知晓 +15
///   · 普通交流         → 暖意 +4、知晓 +8（加在被交流的一方）
///   · 交流母亲         → 暖意 +0、知晓 +11
///   · 小女孩移动东西    → 暖意 +18，并把物品温度清零
///   · 强行触碰         → 暖意 −8
///   · 强碰传染         → 交流对象暖意 −5
///   · 母亲开门         → 知晓 > 60 时暖意跳到 61、知晓 63
///
/// 阈值：高 / 低档位 60、母亲开门 60 / 61。
/// </summary>
public class StatSettlement
{
    private readonly GameConfig config;
    private readonly GameSession session;

    public StatSettlement(GameConfig config, GameSession session)
    {
        this.config = config;
        this.session = session;
    }

    public GameConfig Config { get { return config; } }
    public GameSession Session { get { return session; } }

    // ------------------------------------------------------------------
    // 物品
    // ------------------------------------------------------------------

    /// <summary>住户拿到东西：有温度 +18 暖 / +15 知晓；没温度 0 暖 / +15 知晓。</summary>
    public void ApplyItemTaken(ResidentId by, bool hadTemperature)
    {
        if (hadTemperature)
        {
            AddWarmth(by, config.itemTakenWithTemperatureWarmth);
            AddAwareness(by, config.itemTakenWithTemperatureAwareness);
        }
        else
        {
            AddWarmth(by, 0f);
            AddAwareness(by, config.itemTakenWithoutTemperatureAwareness);
        }
    }

    /// <summary>小女孩移动东西：暖意 +18，并把该物品的温度清零。</summary>
    public void ApplyGirlMovedItem(ItemData item)
    {
        AddWarmth(ResidentId.Girl, config.girlMoveWarmth);
        if (item != null) item.hasTemperature = false;
    }

    // ------------------------------------------------------------------
    // 交流
    // ------------------------------------------------------------------

    /// <summary>
    /// 交流：加在**被交流的一方**。
    /// 普通 +4 暖 / +8 知晓；对象是母亲则 +0 暖 / +11 知晓；
    /// contagion（被强碰者发起）时对方暖意变成 −5。
    /// </summary>
    public void ApplyTalk(ResidentId target, bool contagion)
    {
        bool toMother = target == ResidentId.Mother;

        float warmth = toMother ? 0f : config.talkWarmth;
        float awareness = toMother ? config.talkMotherAwareness : config.talkAwareness;

        if (contagion) warmth = -config.forceTouchContagionPenalty;

        // 母亲的暖意不是独立变量：整局固定 30，只有第 6 轮结束后开门时才跳到 61。
        if (toMother) warmth = 0f;

        AddWarmth(target, warmth);
        AddAwareness(target, awareness);
    }

    /// <summary>强碰传染：下次交流时交流对象暖意 −5。</summary>
    public void ApplyForceTouchContagion(ResidentId target)
    {
        if (target == ResidentId.Mother) return; // 母亲暖意固定
        AddWarmth(target, -config.forceTouchContagionPenalty);
    }

    // ------------------------------------------------------------------
    // 强行触碰
    // ------------------------------------------------------------------

    /// <summary>强行触碰成功：该住户暖意 −8。</summary>
    public void ApplyForceTouch(ResidentId resident)
    {
        AddWarmth(resident, -config.forceTouchWarmthPenalty);
    }

    // ------------------------------------------------------------------
    // 母亲开门
    // ------------------------------------------------------------------

    /// <summary>
    /// **第 6 轮结束后**才判定母亲开门：知晓 &gt; 60 → 暖意至少 61、知晓至少 63。
    /// 已经更高的值不往下压。返回这次有没有开门。
    /// </summary>
    public bool ResolveMotherDoorAtEnd()
    {
        if (config != null && !config.motherEnabled) return false;

        ResidentData mother = session.GetResident(ResidentId.Mother);
        if (mother == null) return false;
        if (mother.awareness <= config.motherOpenAwareness) return false;

        if (mother.warmth < config.motherOpenWarmth)
        {
            AddWarmth(ResidentId.Mother, config.motherOpenWarmth - mother.warmth);
        }
        if (mother.awareness < config.motherOpenAwarenessValue)
        {
            AddAwareness(ResidentId.Mother, config.motherOpenAwarenessValue - mother.awareness);
        }
        return true;
    }

    // ------------------------------------------------------------------
    // 内部：真正改数值的唯一出口
    // ------------------------------------------------------------------

    private float AddWarmth(ResidentId id, float delta)
    {
        return session.AddWarmth(id, delta, config);
    }

    private float AddAwareness(ResidentId id, float delta)
    {
        return session.AddAwareness(id, delta, config);
    }
}
