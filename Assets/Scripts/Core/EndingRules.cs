/// <summary>
/// 三档结局。八个旧结局已经合并：
///   RainInTheCorridor = 原「整栋楼都不知道他已经死了」
///   BeforeSnowStops   = 原「告别」
///   WhenSheWasLittle  = 其余六种情况
/// </summary>
public enum EndingId
{
    /// <summary>BE：楼道里的雨</summary>
    RainInTheCorridor,

    /// <summary>OE：她小时候</summary>
    WhenSheWasLittle,

    /// <summary>HE：雪停之前</summary>
    BeforeSnowStops
}

/// <summary>结局判定结果：档位 + 标题 + 判定依据（调试用）。</summary>
public struct EndingResult
{
    public EndingId id;
    public string title;
    public string reason;
}

/// <summary>
/// 结局判定：纯函数，不依赖场景 / 随机，只看四位住户的暖意与知晓度。
///
/// 判定档位：高 = 数值 &gt; 60，低 = 数值 ≤ 60；
/// 小女孩的知晓度不参与判定。
///
/// 「除母亲外所有人」指老婆婆与大叔（Demo 里的两位大人）。
/// </summary>
public static class EndingRules
{
    public static EndingResult Resolve(GameSession session, GameConfig config)
    {
        if (session == null || config == null)
            return Make(EndingId.RainInTheCorridor, "缺少本局数据");

        float high = config.highThreshold;

        ResidentData mother = session.GetResident(ResidentId.Mother);
        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        ResidentData uncle = session.GetResident(ResidentId.Uncle);
        ResidentData girl = session.GetResident(ResidentId.Girl);

        if (mother == null || grandma == null || uncle == null || girl == null)
            return Make(EndingId.RainInTheCorridor, "住户数据不完整");

        bool motherAwarenessHigh = mother.awareness > high;
        bool motherWarmthHigh = mother.warmth > high;
        bool othersAwarenessHigh = grandma.awareness > high && uncle.awareness > high;
        bool othersWarmthHigh = grandma.warmth > high && uncle.warmth > high;

        if (motherAwarenessHigh && motherWarmthHigh && othersAwarenessHigh && othersWarmthHigh)
            return Make(EndingId.BeforeSnowStops, "所有人都知道了，也都还暖着");

        if (!motherAwarenessHigh && !othersAwarenessHigh && othersWarmthHigh)
            return Make(EndingId.RainInTheCorridor, "楼里还暖着，但没有一个人知道他已经死了");

        return Make(EndingId.WhenSheWasLittle, "没有走到告别，也没有落进全员未知");
    }

    /// <summary>只要档位。</summary>
    public static EndingId ResolveId(GameSession session, GameConfig config)
    {
        return Resolve(session, config).id;
    }

    /// <summary>结局标题。</summary>
    public static string TitleOf(EndingId id)
    {
        switch (id)
        {
            case EndingId.RainInTheCorridor: return "楼道里的雨";
            case EndingId.WhenSheWasLittle: return "她小时候";
            case EndingId.BeforeSnowStops: return "雪停之前";
            default: return id.ToString();
        }
    }

    private static EndingResult Make(EndingId id, string reason)
    {
        return new EndingResult { id = id, title = TitleOf(id), reason = reason };
    }
}
