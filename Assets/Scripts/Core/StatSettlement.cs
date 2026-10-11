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
/// 阈值：门缝 30 / 半开 55、高 60、母亲开门 60 / 61。
/// </summary>
public class StatSettlement
{
    /// <summary>门缝开合程度：对应文档「暖意 30 / 55」。</summary>
    public enum DoorSeamState
    {
        Closed,
        Seam,
        HalfOpen
    }

    /// <summary>门缝光颜色：对应文档「知晓 30 / 54」。</summary>
    public enum DoorLightTint
    {
        White,
        PaleYellow,
        Orange
    }

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
    /// **第 6 轮结束后**才判定母亲开门：知晓 &gt; 60 → 暖意跳到 61（只变这一次）。
    /// 返回这次有没有开门。
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
        return true;
    }

    // ------------------------------------------------------------------
    // 阈值查询（30 / 55 / 60 / 61）
    // ------------------------------------------------------------------

    /// <summary>高 / 低档位：> 60 算高。</summary>
    public bool IsHigh(float value)
    {
        return value > config.highThreshold;
    }

    /// <summary>母亲是否已经开门（知晓 &gt; 60）。</summary>
    public bool IsMotherOpen(float awareness)
    {
        return awareness > config.motherOpenAwareness;
    }

    /// <summary>暖意 → 门缝开合：&lt;30 全关 / 30~55 开一条缝 / ≥55 半开。</summary>
    public DoorSeamState ResolveDoorSeam(float warmth)
    {
        if (warmth < config.doorSeamWarmth) return DoorSeamState.Closed;
        if (warmth < config.doorHalfWarmth) return DoorSeamState.Seam;
        return DoorSeamState.HalfOpen;
    }

    /// <summary>知晓度 → 门缝光：&lt;30 白光 / 30~54 浅黄 / &gt;54 黄橙。</summary>
    public DoorLightTint ResolveLightTint(float awareness, bool hasAwareness)
    {
        if (!hasAwareness) return DoorLightTint.White;
        if (awareness < config.lightWhiteAwareness) return DoorLightTint.White;
        if (awareness <= config.lightYellowAwareness) return DoorLightTint.PaleYellow;
        return DoorLightTint.Orange;
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
