/// <summary>
/// 结局档位（文档 4.6 的 8 个结局）。
/// 本次 Demo 不做结局，但判定写成纯函数，方便提前验证数值；正式版直接接上。
/// </summary>
public enum EndingId
{
    /// <summary>告别</summary>
    Farewell,

    /// <summary>互相隐瞒</summary>
    MutualConcealment,

    /// <summary>只有母亲一个人知道</summary>
    OnlyMotherKnows,

    /// <summary>谁也没开口</summary>
    NobodySpoke,

    /// <summary>全楼都知道了，却没人敢告诉母亲</summary>
    BuildingKnowsSilentToMother,

    /// <summary>整栋楼都不知道他已经死了</summary>
    WholeBuildingUnaware,

    /// <summary>雨没停</summary>
    RainNeverStopped,

    /// <summary>只有小女孩还记着他</summary>
    OnlyGirlRemembers
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
/// 小女孩的知晓度不参与判定（文档 4.6）。
///
/// 「除母亲外所有人」指老婆婆与大叔（Demo 里的两位大人）。
/// </summary>
public static class EndingRules
{
    public static EndingResult Resolve(GameSession session, GameConfig config)
    {
        if (session == null || config == null) return Make(EndingId.RainNeverStopped, "缺少本局数据");

        float high = config.highThreshold; // 60

        ResidentData mother = session.GetResident(ResidentId.Mother);
        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        ResidentData aunt = session.GetResident(ResidentId.Uncle);
        ResidentData girl = session.GetResident(ResidentId.Girl);

        if (mother == null || grandma == null || aunt == null || girl == null)
        {
            return Make(EndingId.RainNeverStopped, "住户数据不完整");
        }

        bool motherAwarenessHigh = mother.awareness > high;
        bool motherWarmthHigh = mother.warmth > high;
        bool motherOpen = motherAwarenessHigh && motherWarmthHigh;

        // 「除母亲外所有人」= 老婆婆 与 大叔。
        bool othersAwarenessHigh = grandma.awareness > high && aunt.awareness > high;
        bool othersWarmthHigh = grandma.warmth > high && aunt.warmth > high;
        bool girlWarmthHigh = girl.warmth > high;

        // ① 母亲已经开门（知晓 > 60 且 暖意 > 60）
        if (motherOpen)
        {
            if (othersAwarenessHigh && othersWarmthHigh)
                return Make(EndingId.Farewell, "母亲与邻居都知道了，而且都还暖着");

            if (othersAwarenessHigh)
                return Make(EndingId.MutualConcealment, "大家都知道，但邻居的暖意掉了");

            if (!othersAwarenessHigh && !othersWarmthHigh)
                return Make(EndingId.OnlyMotherKnows, "只有母亲那边被点亮");

            // 母亲开门、邻居知道但暖意高 → 文档没覆盖，按「只有母亲一个人知道」处理。
            return Make(EndingId.OnlyMotherKnows, "母亲开门、邻居知道但暖意还在（文档未覆盖）");
        }

        // ② 母亲知晓 > 60 但暖意 ≤ 60：门知道了，但没亮起来。
        if (motherAwarenessHigh && !motherWarmthHigh)
        {
            if (othersAwarenessHigh) return Make(EndingId.NobodySpoke, "母亲知道了却没亮起来，邻居也都知道了");
            if (othersWarmthHigh) return Make(EndingId.WholeBuildingUnaware, "邻居还暖着，但都不知道");
            return girlWarmthHigh
                ? Make(EndingId.OnlyGirlRemembers, "只有小女孩的暖意过线")
                : Make(EndingId.RainNeverStopped, "全楼都低");
        }

        // ③ 母亲低（知晓 ≤ 60）
        if (othersAwarenessHigh && othersWarmthHigh)
            return Make(EndingId.BuildingKnowsSilentToMother, "邻居都知道了、暖着，母亲还关着门");

        if (othersAwarenessHigh)
            return Make(EndingId.NobodySpoke, "邻居知道了，但暖意掉了");

        if (othersWarmthHigh)
            return Make(EndingId.WholeBuildingUnaware, "大家还暖着，但都不知道他已经死了");

        return girlWarmthHigh
            ? Make(EndingId.OnlyGirlRemembers, "只有小女孩的暖意过线")
            : Make(EndingId.RainNeverStopped, "所有人的暖意与知晓都在低档");
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
            case EndingId.Farewell: return "告别";
            case EndingId.MutualConcealment: return "互相隐瞒";
            case EndingId.OnlyMotherKnows: return "只有母亲一个人知道";
            case EndingId.NobodySpoke: return "谁也没开口";
            case EndingId.BuildingKnowsSilentToMother: return "全楼都知道了，却没人敢告诉母亲";
            case EndingId.WholeBuildingUnaware: return "整栋楼都不知道他已经死了";
            case EndingId.RainNeverStopped: return "雨没停";
            case EndingId.OnlyGirlRemembers: return "只有小女孩还记着他";
            default: return id.ToString();
        }
    }

    private static EndingResult Make(EndingId id, string reason)
    {
        return new EndingResult { id = id, title = TitleOf(id), reason = reason };
    }
}
