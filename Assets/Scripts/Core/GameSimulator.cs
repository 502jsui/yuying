using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// 模拟器里“玩家”的操作策略。
/// 玩家一局只能碰 1 次/轮，所以“碰哪件、区域被占时认不认怂”会明显影响结局分布。
/// </summary>
public enum SimPlayerPolicy
{
    /// <summary>一直待在一楼；区域被占就放弃这一轮（不碰）。</summary>
    StayAndWait = 0,

    /// <summary>一直待在一楼；区域被占就一直按，直到强行触碰生效（前两次只是警告）。</summary>
    StayAndInsist = 1,

    /// <summary>每轮随机换一楼 / 后院；区域被占就放弃这一轮。</summary>
    RoamAndWait = 2,

    /// <summary>每轮随机换一楼 / 后院；区域被占就强行碰。</summary>
    RoamAndInsist = 3
}

/// <summary>
/// 可选的数值覆盖，用来做“看分布调数值”（七天计划 第 4、6 天）。
/// 只覆盖写了的字段，其余沿用 GameConfig 资产里的真实数值。
/// </summary>
public class SimulationTuning
{
    public float? wanderWeight;
    public float? talkWeight;
    public float? takeOutWeight;
    public float? takeBackWeight;
    public float? girlMoveWeight;
    public float? talkMotherChance;
    public float? absentRoundChance;
    public float? itemTakenWithTemperatureWarmth;
    public float? itemTakenWithTemperatureAwareness;
    public float? itemTakenWithoutTemperatureAwareness;
    public float? talkWarmth;
    public float? talkAwareness;
    public float? talkMotherAwareness;
    public float? girlMoveWarmth;

    [Tooltip("所有「有知晓度」的住户（老婆婆 / 大叔 / 母亲）的初始知晓度。")]
    public float? initialAwareness;

    [Tooltip("只改母亲的初始知晓度（填了就覆盖 initialAwareness 对她的作用）。")]
    public float? motherInitialAwareness;

    public bool IsEmpty
    {
        get
        {
            return !wanderWeight.HasValue && !talkWeight.HasValue && !takeOutWeight.HasValue
                && !takeBackWeight.HasValue && !girlMoveWeight.HasValue && !talkMotherChance.HasValue
                && !absentRoundChance.HasValue && !itemTakenWithTemperatureWarmth.HasValue
                && !itemTakenWithTemperatureAwareness.HasValue && !itemTakenWithoutTemperatureAwareness.HasValue
                && !talkWarmth.HasValue && !talkAwareness.HasValue && !talkMotherAwareness.HasValue
                && !girlMoveWarmth.HasValue && !initialAwareness.HasValue && !motherInitialAwareness.HasValue;
        }
    }

    public void ApplyTo(GameConfig config)
    {
        if (config == null) return;
        if (wanderWeight.HasValue) config.wanderWeight = wanderWeight.Value;
        if (talkWeight.HasValue) config.talkWeight = talkWeight.Value;
        if (takeOutWeight.HasValue) config.takeOutWeight = takeOutWeight.Value;
        if (takeBackWeight.HasValue) config.takeBackWeight = takeBackWeight.Value;
        if (girlMoveWeight.HasValue) config.girlMoveWeight = girlMoveWeight.Value;
        if (talkMotherChance.HasValue) config.talkMotherChance = talkMotherChance.Value;
        if (absentRoundChance.HasValue) config.absentRoundChance = absentRoundChance.Value;
        if (itemTakenWithTemperatureWarmth.HasValue) config.itemTakenWithTemperatureWarmth = itemTakenWithTemperatureWarmth.Value;
        if (itemTakenWithTemperatureAwareness.HasValue) config.itemTakenWithTemperatureAwareness = itemTakenWithTemperatureAwareness.Value;
        if (itemTakenWithoutTemperatureAwareness.HasValue) config.itemTakenWithoutTemperatureAwareness = itemTakenWithoutTemperatureAwareness.Value;
        if (talkWarmth.HasValue) config.talkWarmth = talkWarmth.Value;
        if (talkAwareness.HasValue) config.talkAwareness = talkAwareness.Value;
        if (talkMotherAwareness.HasValue) config.talkMotherAwareness = talkMotherAwareness.Value;
        if (girlMoveWarmth.HasValue) config.girlMoveWarmth = girlMoveWarmth.Value;

        if (initialAwareness.HasValue || motherInitialAwareness.HasValue)
        {
            for (int i = 0; i < config.residents.Count; i++)
            {
                ResidentConfig resident = config.residents[i];
                if (resident == null || !resident.hasAwareness) continue;

                if (resident.id == ResidentId.Mother)
                {
                    if (motherInitialAwareness.HasValue) resident.awareness = motherInitialAwareness.Value;
                    else if (initialAwareness.HasValue) resident.awareness = initialAwareness.Value;
                }
                else if (initialAwareness.HasValue)
                {
                    resident.awareness = initialAwareness.Value;
                }
            }
        }
    }

    /// <summary>列出这次覆盖了哪些参数，方便把结果对上配置。</summary>
    public List<string> Describe()
    {
        List<string> lines = new List<string>();
        Add(lines, "wanderWeight", wanderWeight);
        Add(lines, "talkWeight", talkWeight);
        Add(lines, "takeOutWeight", takeOutWeight);
        Add(lines, "takeBackWeight", takeBackWeight);
        Add(lines, "girlMoveWeight", girlMoveWeight);
        Add(lines, "talkMotherChance", talkMotherChance);
        Add(lines, "absentRoundChance", absentRoundChance);
        Add(lines, "itemTakenWithTemperatureWarmth", itemTakenWithTemperatureWarmth);
        Add(lines, "itemTakenWithTemperatureAwareness", itemTakenWithTemperatureAwareness);
        Add(lines, "itemTakenWithoutTemperatureAwareness", itemTakenWithoutTemperatureAwareness);
        Add(lines, "talkWarmth", talkWarmth);
        Add(lines, "talkAwareness", talkAwareness);
        Add(lines, "talkMotherAwareness", talkMotherAwareness);
        Add(lines, "girlMoveWarmth", girlMoveWarmth);
        Add(lines, "initialAwareness", initialAwareness);
        Add(lines, "motherInitialAwareness", motherInitialAwareness);
        return lines;
    }

    private static void Add(List<string> lines, string name, float? value)
    {
        if (value.HasValue) lines.Add(name + "=" + value.Value.ToString("0.###", CultureInfo.InvariantCulture));
    }
}

public class SimulationOptions
{
    /// <summary>跑多少局。</summary>
    public int runs = 500;

    /// <summary>第 1 局的种子，之后逐局派生（同参数同结果）。</summary>
    public int seedBase = GameSimulator.DefaultSeedBase;

    public SimPlayerPolicy policy = SimPlayerPolicy.StayAndInsist;

    public SimulationTuning tuning = new SimulationTuning();

    /// <summary>本次 Demo 不做母亲：整条母亲链断开（没人找她、她不开门）。</summary>
    public bool ignoreMother;

    /// <summary>把前几局的逐轮明细也打出来（0 = 不打）。</summary>
    public int traceGames;

    /// <summary>&gt;= 0 时只追踪这一局（按局序号，从 0 开始）。</summary>
    public int traceIndex = -1;
}

/// <summary>程序分工 7.4 的验收线。</summary>
public class SimulationAcceptance
{
    public bool requireAllEndings = true;
    public float maxShare = 0.6f;
    public bool checkMinShare = true;
    public float minShare = 0.15f;
}

public struct SimGameResult
{
    public int seed;
    public EndingId ending;
    public bool motherDoorOpen;
    public float motherWarmth;
    public float motherAwareness;
    public float grandmaWarmth;
    public float grandmaAwareness;
    public float uncleWarmth;
    public float uncleAwareness;
    public float girlWarmth;
    public int warmTakeBacks;
    public int coldTakeBacks;
    public int girlMoves;
    public int itemTouches;
    public int forceTouches;
    public int forceWarnings;
    public int actions;
}

public class SimulationReport
{
    public int runs;
    public int seedBase;
    public SimPlayerPolicy policy;
    public bool motherEnabled = true;
    public List<string> tuningApplied = new List<string>();
    public List<SimGameResult> games = new List<SimGameResult>();

    public int[] endingCounts;
    public float[] endingShares;
    public EndingId[] endingOrder;

    public int endingsPresent;
    public int endingsMissing;
    public EndingId maxEnding;
    public float maxShare;
    public EndingId minEnding;
    public float minShare;

    public float motherDoorShare;
    public float highThreshold = 60f;

    /// <summary>「整栋楼都不知道他已经死了」的第一道门槛：婆婆和大叔的暖意都超过阈值。</summary>
    public int neighborsWarmBothHigh;

    /// <summary>上面那些局里，另外两道门槛（至少一人知晓 ≤ 阈值、母亲没开门）也满足的局数。</summary>
    public int buildingUnawareCandidates;

    public float averageWarmTakeBacks;
    public float averageColdTakeBacks;
    public float averageGirlMoves;
    public float averageItemTouches;
    public float averageForceTouches;
    public float averageForceWarnings;

    public float[] averageWarmth;
    public float[] averageAwareness;

    public List<string> trace = new List<string>();

    public string ToText(SimulationAcceptance acceptance, GameConfig config)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("余音 · 结局分布模拟（不开画面）");
        text.AppendLine("局数：" + runs + "    起始种子：" + seedBase
            + "    玩家策略：" + DescribePolicy(policy));
        text.AppendLine("每轮触碰：" + (config != null ? config.touchesPerRound : 1)
            + "    总轮数：" + (config != null ? config.totalRounds : 6)
            + "    场上物品：" + (config != null ? config.sceneItemCount : 3));
        text.AppendLine("母亲：" + (motherEnabled ? "参与本局" : "不参与（Demo 口径：没人找她、她不开门）"));
        if (tuningApplied.Count > 0) text.AppendLine("数值覆盖：" + string.Join("，", tuningApplied.ToArray()));
        text.AppendLine();

        text.AppendLine("结局                                     次数     占比");
        text.AppendLine("---------------------------------------------------------");
        for (int i = 0; i < endingOrder.Length; i++)
        {
            EndingId id = endingOrder[i];
            text.AppendLine(Pad(EndingRules.TitleOf(id), 34)
                + Pad(endingCounts[(int)id].ToString(CultureInfo.InvariantCulture), 9)
                + Percent(endingShares[(int)id]));
        }
        text.AppendLine("---------------------------------------------------------");
        text.AppendLine("出现过的结局：" + endingsPresent + " / " + endingOrder.Length);
        text.AppendLine("最大：" + Pad(EndingRules.TitleOf(maxEnding), 34) + Percent(maxShare));
        text.AppendLine("最小：" + Pad(EndingRules.TitleOf(minEnding), 34) + Percent(minShare));
        text.AppendLine("母亲开门：" + Percent(motherDoorShare));
        text.AppendLine();

        text.AppendLine("平均每局：拿到有温度的东西 " + Num(averageWarmTakeBacks)
            + " 件 / 没温度 " + Num(averageColdTakeBacks)
            + " 件 / 小女孩移动 " + Num(averageGirlMoves)
            + " 次 / 玩家触碰 " + Num(averageItemTouches)
            + " 次 / 强碰生效 " + Num(averageForceTouches)
            + " 次 / 强碰警告 " + Num(averageForceWarnings) + " 次");
        text.AppendLine("「整栋楼都不知道」三道门槛：邻居暖意都 >" + Num(highThreshold) + " 的局 "
            + neighborsWarmBothHigh + " / " + runs
            + "；其中再满足「至少一人知晓 ≤" + Num(highThreshold) + " 且母亲没开门」的局 "
            + buildingUnawareCandidates);
        text.AppendLine("平均最终暖意：老婆婆 " + Num(averageWarmth[(int)ResidentId.Grandma])
            + "  大叔 " + Num(averageWarmth[(int)ResidentId.Uncle])
            + "  小女孩 " + Num(averageWarmth[(int)ResidentId.Girl])
            + "  母亲 " + Num(averageWarmth[(int)ResidentId.Mother]));
        text.AppendLine("平均最终知晓：老婆婆 " + Num(averageAwareness[(int)ResidentId.Grandma])
            + "  大叔 " + Num(averageAwareness[(int)ResidentId.Uncle])
            + "  母亲 " + Num(averageAwareness[(int)ResidentId.Mother]));
        text.AppendLine();

        text.AppendLine("验收：");
        List<string> lines = AcceptanceLines(acceptance);
        for (int i = 0; i < lines.Count; i++) text.AppendLine("  " + lines[i]);

        if (!motherEnabled)
        {
            text.AppendLine();
            text.AppendLine("  注：母亲不参与时，「告别 / 互相隐瞒 / 只有母亲一个人知道」需要她开门，"
                + "结构上不可能出现（其余 5 档可达）。");
        }

        float floor = 1f / Mathf.Max(1, endingOrder.Length);
        if (acceptance != null && acceptance.checkMinShare && acceptance.minShare > floor)
        {
            text.AppendLine();
            text.AppendLine("  注：共 " + endingOrder.Length + " 个结局，若每个都 ≥" + Percent(acceptance.minShare)
                + "，总和至少 " + Percent(acceptance.minShare * endingOrder.Length)
                + "，超过 100% —— 数学上不可能，这条验收线要重新定。");
        }

        if (trace.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("逐轮明细：");
            for (int i = 0; i < trace.Count; i++) text.AppendLine("  " + trace[i]);
        }
        return text.ToString();
    }

    /// <summary>验收结论，每条一行；第一行是总判定。</summary>
    public List<string> AcceptanceLines(SimulationAcceptance acceptance)
    {
        if (acceptance == null) acceptance = new SimulationAcceptance();
        List<string> lines = new List<string>();
        bool pass = true;

        List<string> missing = new List<string>();
        for (int i = 0; i < endingOrder.Length; i++)
        {
            EndingId id = endingOrder[i];
            if (endingCounts[(int)id] == 0) missing.Add(EndingRules.TitleOf(id));
        }

        if (acceptance.requireAllEndings)
        {
            if (missing.Count == 0)
            {
                lines.Add("[通过] 每个结局都出现过（" + endingOrder.Length + " / " + endingOrder.Length + "）");
            }
            else
            {
                pass = false;
                lines.Add("[失败] 每个结局都出现过：出现 0 次的有 " + missing.Count + " 个 —— "
                    + string.Join("、", missing.ToArray()));
            }
        }

        if (maxShare <= acceptance.maxShare)
        {
            lines.Add("[通过] 最大结局占比 ≤ " + Percent(acceptance.maxShare)
                + "（" + EndingRules.TitleOf(maxEnding) + " " + Percent(maxShare) + "）");
        }
        else
        {
            pass = false;
            lines.Add("[失败] 最大结局占比 ≤ " + Percent(acceptance.maxShare)
                + "：现在是 " + EndingRules.TitleOf(maxEnding) + " " + Percent(maxShare));
        }

        if (acceptance.checkMinShare)
        {
            if (minShare >= acceptance.minShare)
            {
                lines.Add("[通过] 最小结局占比 ≥ " + Percent(acceptance.minShare)
                    + "（" + EndingRules.TitleOf(minEnding) + " " + Percent(minShare) + "）");
            }
            else
            {
                pass = false;
                lines.Add("[失败] 最小结局占比 ≥ " + Percent(acceptance.minShare)
                    + "：现在是 " + EndingRules.TitleOf(minEnding) + " " + Percent(minShare));
            }
        }

        lines.Insert(0, pass ? "总判定：通过" : "总判定：不通过");
        return lines;
    }

    public static string DescribePolicy(SimPlayerPolicy policy)
    {
        switch (policy)
        {
            case SimPlayerPolicy.StayAndWait: return "留在一楼 / 区域被占就不碰";
            case SimPlayerPolicy.StayAndInsist: return "留在一楼 / 区域被占就强碰";
            case SimPlayerPolicy.RoamAndWait: return "随机换区域 / 区域被占就不碰";
            case SimPlayerPolicy.RoamAndInsist: return "随机换区域 / 区域被占就强碰";
            default: return policy.ToString();
        }
    }

    private static string Pad(string value, int width)
    {
        if (string.IsNullOrEmpty(value)) value = "";
        int pad = width - DisplayWidth(value);
        return pad > 0 ? value + new string(' ', pad) : value + " ";
    }

    /// <summary>按“一个汉字占两格”估算显示宽度，让表头对得齐。</summary>
    private static int DisplayWidth(string value)
    {
        int width = 0;
        for (int i = 0; i < value.Length; i++) width += value[i] > 0x2E80 ? 2 : 1;
        return width;
    }

    private static string Percent(float value)
    {
        return (value * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }

    private static string Num(float value)
    {
        return value.ToString("0.0", CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// 不开画面的整局模拟器（程序分工 7.4 / 七天计划 第 3、4、6 天）。
///
/// 它把 GameFlow + NpcPhaseDriver 的一轮流程按同样的顺序重跑一遍，但不生成任何场景物体，
/// 用的是真正的 ItemSystem / NpcSystem / StatSettlement / EndingRules，
/// 所以跑出来的结局分布就是当前数值下的真实分布。
///
/// 命令行入口在 Editor/YuyinSimulatorTool.cs。
/// </summary>
public static class GameSimulator
{
    public const int DefaultSeedBase = 1;

    /// <summary>结局表的固定顺序（和 EndingId 的声明顺序一致，方便两次结果对比）。</summary>
    public static readonly EndingId[] AllEndings =
    {
        EndingId.Farewell,
        EndingId.MutualConcealment,
        EndingId.OnlyMotherKnows,
        EndingId.NobodySpoke,
        EndingId.BuildingKnowsSilentToMother,
        EndingId.WholeBuildingUnaware,
        EndingId.RainNeverStopped,
        EndingId.OnlyGirlRemembers
    };

    private static readonly AreaId[] RoamAreas = { AreaId.FirstFloor, AreaId.Backyard };

    // ------------------------------------------------------------------
    // 放置点：和 Tools → 余音 → 在当前场景补齐物品 铺的点一致
    // ------------------------------------------------------------------

    public static void InstallDemoPlacements()
    {
        // 模拟不认场景里注册了什么，用固定的这一套，保证任何环境下跑出来的分布都一样。
        // （一楼 / 后院的放置点如果改了，这里要跟着改。）
        PlacementRegistry.Clear();

        RegisterPlacement("first_rack", AreaId.FirstFloor, PlacementSurface.HangingRack);
        RegisterPlacement("first_ground_a", AreaId.FirstFloor, PlacementSurface.Ground);
        RegisterPlacement("first_ground_b", AreaId.FirstFloor, PlacementSurface.Ground);
        RegisterPlacement("first_ground_c", AreaId.FirstFloor, PlacementSurface.Ground);
        RegisterPlacement("first_on_item", AreaId.FirstFloor, PlacementSurface.OnItem);

        RegisterPlacement("backyard_ground_a", AreaId.Backyard, PlacementSurface.Ground);
        RegisterPlacement("backyard_ground_b", AreaId.Backyard, PlacementSurface.Ground);
        RegisterPlacement("backyard_ground_c", AreaId.Backyard, PlacementSurface.Ground);
        RegisterPlacement("backyard_on_item", AreaId.Backyard, PlacementSurface.OnItem);
    }

    private static void RegisterPlacement(string placementId, AreaId area, PlacementSurface surface)
    {
        PlacementRegistry.Register(new PlacementOption
        {
            placementId = placementId,
            area = area,
            surface = surface
        });
    }

    // ------------------------------------------------------------------
    // 批量跑
    // ------------------------------------------------------------------

    /// <summary>跑 runs 局并汇总。config 里应该已经是最终数值（调用方负责应用 tuning 覆盖）。</summary>
    public static SimulationReport Run(GameConfig config, SimulationOptions options)
    {
        if (config == null) throw new ArgumentNullException("config");
        if (options == null) options = new SimulationOptions();
        if (options.runs < 1) options.runs = 1;

        config.EnsureDefaults();
        InstallDemoPlacements();

        SimulationReport report = new SimulationReport();
        report.runs = options.runs;
        report.seedBase = options.seedBase;
        report.policy = options.policy;
        report.tuningApplied = options.tuning != null ? options.tuning.Describe() : new List<string>();
        report.highThreshold = config.highThreshold;

        if (options.ignoreMother) config.motherEnabled = false;
        report.motherEnabled = config.motherEnabled;

        for (int i = 0; i < options.runs; i++)
        {
            int seed = DeriveSeed(options.seedBase, i);
            bool wantTrace = options.traceIndex >= 0 ? options.traceIndex == i : i < options.traceGames;
            List<string> trace = wantTrace ? new List<string>() : null;

            SimGameResult game = RunOne(config, options, seed, trace);
            report.games.Add(game);

            if (trace != null)
            {
                report.trace.Add("—— 第 " + (i + 1) + " 局（种子 " + seed + "）——");
                report.trace.AddRange(trace);
            }
        }

        Summarize(report);
        return report;
    }

    /// <summary>跑单独一局，返回结局。</summary>
    public static EndingId RunSingle(GameConfig config, int seed)
    {
        SimulationOptions options = new SimulationOptions { runs = 1, seedBase = seed };
        SimulationReport report = Run(config, options);
        return report.games[0].ending;
    }

    private static void Summarize(SimulationReport report)
    {
        EndingId[] order = AllEndings;
        int[] counts = new int[order.Length];
        for (int i = 0; i < report.games.Count; i++) counts[(int)report.games[i].ending]++;

        float[] shares = new float[order.Length];
        int present = 0;
        EndingId maxEnding = order[0];
        EndingId minEnding = order[0];
        for (int i = 0; i < order.Length; i++)
        {
            shares[i] = (float)counts[i] / Mathf.Max(1, report.games.Count);
            if (counts[i] > 0) present++;
            if (counts[i] > counts[(int)maxEnding]) maxEnding = order[i];
            if (counts[i] < counts[(int)minEnding]) minEnding = order[i];
        }

        report.endingOrder = order;
        report.endingCounts = counts;
        report.endingShares = shares;
        report.endingsPresent = present;
        report.endingsMissing = order.Length - present;
        report.maxEnding = maxEnding;
        report.maxShare = shares[(int)maxEnding];
        report.minEnding = minEnding;
        report.minShare = shares[(int)minEnding];

        int doorOpen = 0;
        int warmBothHigh = 0;
        int unawareCandidates = 0;
        float warm = 0f, cold = 0f, girl = 0f, touches = 0f, force = 0f, warnings = 0f;
        float[] warmth = new float[4];
        float[] awareness = new float[4];
        for (int i = 0; i < report.games.Count; i++)
        {
            SimGameResult g = report.games[i];
            if (g.motherDoorOpen) doorOpen++;

            // 「整栋楼都不知道他已经死了」的三道门槛，逐局数一数卡在哪。
            bool neighborsWarmHigh = g.grandmaWarmth > report.highThreshold && g.uncleWarmth > report.highThreshold;
            if (neighborsWarmHigh)
            {
                warmBothHigh++;
                bool someoneAwarenessLow = g.grandmaAwareness <= report.highThreshold
                    || g.uncleAwareness <= report.highThreshold;
                if (someoneAwarenessLow && !g.motherDoorOpen) unawareCandidates++;
            }

            warm += g.warmTakeBacks;
            cold += g.coldTakeBacks;
            girl += g.girlMoves;
            touches += g.itemTouches;
            force += g.forceTouches;
            warnings += g.forceWarnings;
            warmth[(int)ResidentId.Grandma] += g.grandmaWarmth;
            warmth[(int)ResidentId.Uncle] += g.uncleWarmth;
            warmth[(int)ResidentId.Girl] += g.girlWarmth;
            warmth[(int)ResidentId.Mother] += g.motherWarmth;
            awareness[(int)ResidentId.Grandma] += g.grandmaAwareness;
            awareness[(int)ResidentId.Uncle] += g.uncleAwareness;
            awareness[(int)ResidentId.Mother] += g.motherAwareness;
        }

        float n = Mathf.Max(1, report.games.Count);
        report.motherDoorShare = doorOpen / n;
        report.neighborsWarmBothHigh = warmBothHigh;
        report.buildingUnawareCandidates = unawareCandidates;
        report.averageWarmTakeBacks = warm / n;
        report.averageColdTakeBacks = cold / n;
        report.averageGirlMoves = girl / n;
        report.averageItemTouches = touches / n;
        report.averageForceTouches = force / n;
        report.averageForceWarnings = warnings / n;
        report.averageWarmth = new float[4];
        report.averageAwareness = new float[4];
        for (int i = 0; i < 4; i++)
        {
            report.averageWarmth[i] = warmth[i] / n;
            report.averageAwareness[i] = awareness[i] / n;
        }
    }

    // ------------------------------------------------------------------
    // 单局
    // ------------------------------------------------------------------

    /// <summary>
    /// 第 n 局的种子：从 seedBase 派生而不是 seedBase + n，
    /// 避免 System.Random 在连续种子上产生相关性。
    /// </summary>
    public static int DeriveSeed(int seedBase, int index)
    {
        unchecked
        {
            uint x = (uint)(seedBase + index * 0x9E3779B9);
            x ^= x >> 16;
            x *= 0x7FEB352D;
            x ^= x >> 15;
            x *= 0x846CA68B;
            x ^= x >> 16;
            return (int)x;
        }
    }

    private static SimGameResult RunOne(GameConfig config, SimulationOptions options, int seed, List<string> trace)
    {
        PlacementRegistry.ClearOccupancy();

        RandomService random = new RandomService(seed);
        GameSession session = new GameSession();
        session.Reset(config, seed);

        StatSettlement stats = new StatSettlement(config, session);
        ItemSystem items = new ItemSystem(config, random, session, null, stats);
        NpcSystem npc = new NpcSystem(config, random, session, items, null, stats);
        items.InitializeFromConfig();
        session.isRunning = true;

        SimCounters counters = new SimCounters();
        Action<ItemTakenEvent> onTaken = delegate(ItemTakenEvent evt)
        {
            if (evt.hadTemperature) counters.warmTakeBacks++;
            else counters.coldTakeBacks++;
        };
        Action<ItemMovedEvent> onMoved = delegate(ItemMovedEvent evt) { counters.girlMoves++; };
        EventBus.Subscribe(onTaken);
        EventBus.Subscribe(onMoved);

        try
        {
            int guard = 0;
            while (session.round <= config.totalRounds)
            {
                if (++guard > 256)
                {
                    Debug.LogError("[模拟器] 轮次没有推进，已强制中断（seed=" + seed + "）。");
                    break;
                }

                // ① 场景状态：住户回家 / 上一轮定下的「谁不回家」生效。
                session.BeginRound(session.hasAbsentResident, session.absentResident);
                npc.BeginRound(session.hasAbsentResident, session.absentResident);

                if (IsRoam(options.policy)) session.playerArea = random.Pick(RoamAreas);

                SimCounters roundStart = counters.Snapshot();

                // ② 场上摆物品（和 GameFlow 的 SceneState 阶段一致：摆在玩家所在区域）。
                items.EnsureSceneItems(session.playerArea, config.sceneItemCount);

                // 玩家输入：一轮只能碰一次。
                PlayPlayerTurn(options, random, session, items, counters, trace);

                // ③ 物品结算：温度在触摸时就标好了，这一步在逻辑上没有额外动作。

                // ④ 住户行为。
                npc.ForceTakeOutNext = npc.CountUntouchedItems(session.playerArea) <= 1;
                RunNpcActions(npc, counters);
                npc.ResolveEndOfRound();

                // ⑤ 轮次 +1。
                if (trace != null) trace.Add(RoundSnapshot(session, counters, roundStart));
                session.round++;
            }
        }
        finally
        {
            EventBus.Unsubscribe(onTaken);
            EventBus.Unsubscribe(onMoved);
        }

        // ⑦ 母亲开门 + ⑧ 结局（顺序不能换，文档 5.5）。
        bool motherDoorOpen = stats.ResolveMotherDoorAtEnd();
        if (motherDoorOpen) items.CollectMotherItems();
        EndingResult ending = EndingRules.Resolve(session, config);

        ResidentData mother = session.GetResident(ResidentId.Mother);
        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        ResidentData uncle = session.GetResident(ResidentId.Uncle);
        ResidentData girl = session.GetResident(ResidentId.Girl);

        SimGameResult result = new SimGameResult
        {
            seed = seed,
            ending = ending.id,
            motherDoorOpen = motherDoorOpen,
            motherWarmth = mother != null ? mother.warmth : 0f,
            motherAwareness = mother != null ? mother.awareness : 0f,
            grandmaWarmth = grandma != null ? grandma.warmth : 0f,
            grandmaAwareness = grandma != null ? grandma.awareness : 0f,
            uncleWarmth = uncle != null ? uncle.warmth : 0f,
            uncleAwareness = uncle != null ? uncle.awareness : 0f,
            girlWarmth = girl != null ? girl.warmth : 0f,
            warmTakeBacks = counters.warmTakeBacks,
            coldTakeBacks = counters.coldTakeBacks,
            girlMoves = counters.girlMoves,
            itemTouches = counters.itemTouches,
            forceTouches = counters.forceTouches,
            forceWarnings = counters.forceWarnings,
            actions = counters.actions
        };

        if (trace != null)
        {
            trace.Add("结局：" + EndingRules.TitleOf(ending.id) + "（" + ending.reason + "）");
            trace.Add("最终（暖/知晓）：" + F(grandma) + "  " + F(uncle) + "  " + F(girl) + "  " + F(mother));
        }

        return result;
    }

    /// <summary>
    /// 玩家这一轮的动作：碰一件自己区域里还没有温度的东西；
    /// 区域被「区域持续」的住户占着时，按策略选择放弃或强行触碰。
    /// </summary>
    private static void PlayPlayerTurn(SimulationOptions options, RandomService random, GameSession session,
        ItemSystem items, SimCounters counters, List<string> trace)
    {
        AreaId area = session.playerArea;
        ResidentId blocker;
        ResidentData blocking = items.TryGetBlockedResident(area, out blocker) ? session.GetResident(blocker) : null;

        if (blocking != null)
        {
            if (!Insists(options.policy))
            {
                if (trace != null) trace.Add("玩家：区域被" + blocker.ToDisplayName() + "占着，这一轮没碰。");
                return;
            }

            // 一直按：整局只有 2 次警告，第 3 次强行生效（和 ItemSystem 的规则一致）。
            for (int attempt = 0; attempt < 4; attempt++)
            {
                ItemActionResult result = items.TouchResident(blocker);
                RecordTouch(result, counters);
                if (trace != null) trace.Add("玩家：" + DescribeTouch(result, null, blocker));
                if (result.ChangedState) return;
                if (result.status != ItemActionStatus.ForceTouchWarned) return;
                if (!items.TryGetBlockedResident(area, out blocker)) break;
            }
            return;
        }

        List<ItemData> candidates = new List<ItemData>();
        List<ItemData> inScene = items.GetSceneItems(area);
        for (int i = 0; i < inScene.Count; i++)
        {
            ItemData item = inScene[i];
            if (item.isReturned || item.hasTemperature) continue;
            candidates.Add(item);
        }

        while (candidates.Count > 0)
        {
            ItemData pick = random.Pick(candidates);
            candidates.Remove(pick);

            ItemActionResult result = items.Touch(pick.itemId);
            RecordTouch(result, counters);
            if (trace != null) trace.Add("玩家：" + DescribeTouch(result, pick, ResidentId.Girl));
            if (result.ChangedState) return;
            if (result.status == ItemActionStatus.ForceTouchWarned) return;
        }

        if (trace != null) trace.Add("玩家：本区域没有可碰的东西，这一轮没碰。");
    }

    private static void RunNpcActions(NpcSystem npc, SimCounters counters)
    {
        List<ResidentData> actors = npc.GetActors();
        for (int i = 0; i < actors.Count; i++)
        {
            ResidentData actor = actors[i];
            int count = npc.RollActionCount(actor);
            for (int order = 1; order <= count; order++)
            {
                NpcActionRecord record = npc.DecideNextAction(actor, order);
                NpcActionResult result = npc.Execute(record);
                counters.Count(record, result);
            }
        }
    }

    private static string RoundSnapshot(GameSession session, SimCounters counters, SimCounters before)
    {
        List<ItemData> inScene = session.GetItemsInArea(session.playerArea);
        int warm = 0;
        for (int i = 0; i < inScene.Count; i++) if (inScene[i].hasTemperature) warm++;

        return "第 " + session.round + " 轮：区域 " + session.playerArea.ToDisplayName()
            + "（场上 " + inScene.Count + " 件，有温度 " + warm + "）"
            + "  住户行动 " + (counters.actions - before.actions) + " 次（拿出 " + (counters.takeOut - before.takeOut)
            + " / 拿回 " + (counters.takeBack - before.takeBack) + " / 交流 " + (counters.talk - before.talk)
            + "→母亲 " + (counters.talkToMother - before.talkToMother)
            + " / 闲逛 " + (counters.wander - before.wander) + " / 移动 " + (counters.girlMove - before.girlMove) + "）"
            + "  暖/知晓 " + F(session.GetResident(ResidentId.Grandma)) + " " + F(session.GetResident(ResidentId.Uncle))
            + " " + F(session.GetResident(ResidentId.Girl)) + " " + F(session.GetResident(ResidentId.Mother));
    }

    private static string F(ResidentData resident)
    {
        if (resident == null) return "-";
        return resident.id.ToDisplayName()
            + resident.warmth.ToString("0", CultureInfo.InvariantCulture) + "/"
            + resident.awareness.ToString("0", CultureInfo.InvariantCulture);
    }

    private static string DescribeTouch(ItemActionResult result, ItemData item, ResidentId blocker)
    {
        string what = item != null ? item.itemId : ("住户 " + blocker.ToDisplayName());
        switch (result.status)
        {
            case ItemActionStatus.Success: return "触碰 " + what + "（有温度了）";
            case ItemActionStatus.ForceTouchWarned: return "强行触碰 " + what + " 只被警告（警告不消耗本轮触碰）";
            case ItemActionStatus.ForceTouchAccepted: return "强行触碰 " + what + " 生效（他进入惊吓）";
            case ItemActionStatus.AlreadyTouched: return "碰 " + what + " 被拒：已经触摸过了";
            case ItemActionStatus.TouchUsedUpThisRound: return "本轮已经碰过了";
            case ItemActionStatus.NotInScene: return "碰 " + what + " 被拒：不在场景里";
            default: return "碰 " + what + " 被拒：" + result.status;
        }
    }

    private static void RecordTouch(ItemActionResult result, SimCounters counters)
    {
        if (result.status == ItemActionStatus.Success) counters.itemTouches++;
        else if (result.status == ItemActionStatus.ForceTouchAccepted) counters.forceTouches++;
        else if (result.status == ItemActionStatus.ForceTouchWarned) counters.forceWarnings++;
    }

    private static bool IsRoam(SimPlayerPolicy policy)
    {
        return policy == SimPlayerPolicy.RoamAndWait || policy == SimPlayerPolicy.RoamAndInsist;
    }

    private static bool Insists(SimPlayerPolicy policy)
    {
        return policy == SimPlayerPolicy.StayAndInsist || policy == SimPlayerPolicy.RoamAndInsist;
    }

    private class SimCounters
    {
        public int itemTouches;
        public int forceTouches;
        public int forceWarnings;
        public int warmTakeBacks;
        public int coldTakeBacks;
        public int girlMoves;
        public int actions;
        public int takeOut;
        public int takeBack;
        public int talk;
        public int talkToMother;
        public int wander;
        public int girlMove;

        /// <summary>把当前计数拷一份，用来算“这一轮”的增量。</summary>
        public SimCounters Snapshot()
        {
            return (SimCounters)MemberwiseClone();
        }

        public void Count(NpcActionRecord record, NpcActionResult result)
        {
            actions++;
            switch (record.action)
            {
                case NpcActionType.Wander: wander++; break;
                case NpcActionType.TakeOutItem: takeOut++; break;
                case NpcActionType.TakeBackItem: takeBack++; break;
                case NpcActionType.Talk:
                    talk++;
                    if (record.target == ResidentId.Mother) talkToMother++;
                    break;
                case NpcActionType.MoveItem: girlMove++; break;
            }
        }
    }
}
