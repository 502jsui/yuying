using System.Collections.Generic;

/// <summary>
/// 回合层面的纯规则函数，方便单测和回放。
/// </summary>
public static class TurnRules
{
    /// <summary>
    /// 第一步：判定本轮**有没有人**不回家（80% 有 1 人 / 20% 全员在家）。
    /// </summary>
    public static bool ShouldSomeoneStayOut(RandomService random, GameConfig config)
    {
        if (random == null || config == null) return false;
        return random.Chance(config.absentRoundChance);
    }

    /// <summary>
    /// 第二步：判定**是谁**不回家。只在能行动的住户（老婆婆、大叔、小女孩）里挑；
    /// 母亲不行动，永远不会被选中。
    /// </summary>
    public static bool TryPickWhoStaysOut(RandomService random, IList<ResidentData> residents, out ResidentId absent)
    {
        absent = default(ResidentId);
        if (random == null || residents == null) return false;

        List<ResidentId> candidates = new List<ResidentId>();
        for (int i = 0; i < residents.Count; i++)
        {
            ResidentData resident = residents[i];
            if (resident == null || !resident.canAct) continue;
            candidates.Add(resident.id);
        }

        if (candidates.Count == 0) return false;
        absent = random.Pick(candidates);
        return true;
    }

    /// <summary>
    /// 两步合起来：先判有没有，再判是谁。
    /// 返回 true 表示有人不回家，absent 是那个人；返回 false 表示全员在家。
    /// </summary>
    public static bool TryPickAbsentResident(RandomService random, GameConfig config, IList<ResidentData> residents, out ResidentId absent)
    {
        absent = default(ResidentId);
        if (!ShouldSomeoneStayOut(random, config)) return false;
        return TryPickWhoStaysOut(random, residents, out absent);
    }

}
