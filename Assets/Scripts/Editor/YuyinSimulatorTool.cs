using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 结局分布模拟器的命令行 / 菜单入口（程序分工 7.4，七天计划 第 3、4、6 天）。
///
/// 菜单：Tools → 余音 → 结局分布模拟
/// 命令行：
///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;项目路径&gt;" ^
///     -executeMethod YuyinSimulatorTool.RunFromCommandLine -simRuns 500 -simOut "Docs/Simulation/结局分布.md"
///
/// 参数（都可省略）：
///   -simRuns 500              跑多少局
///   -simSeed 1                起始种子
///   -simPolicy full           full / stay / insist / roam / roamInsist
///   -simOut &lt;path&gt;            把报告写成 markdown
///   -simCsv &lt;path&gt;            把分布写成 csv
///   -simTraceGames 1          打印前几局的逐轮明细
///   -simTrace 0               只打印第 n 局（从 0 开始）的逐轮明细
///   -simMaxShare 0.6          “最大结局占比”上限
///   -simMinShare 0.15         “最小结局占比”下限
///   -simNoMinShare            跳过 “最小占比” 这一条
///   -simAllowMissing          允许有结局出现 0 次
///   -simIgnoreMother          Demo 口径：母亲不参与本局（没人找她、她不开门）
///   数值覆盖：-simWander -simTalk -simTakeOut -simTakeBack -simGirlMove
///             -simTalkMother -simAbsent
///             -simWarmTake -simAwareTake -simAwareTakeCold
///             -simWarmTalk -simAwareTalk -simAwareTalkMother -simGirlWarm
///             -simInitAwareness -simInitAwarenessMother（初始知晓度）
///             -simTouches（每轮玩家能碰几次）
/// </summary>
public static class YuyinSimulatorTool
{
    private const string ReportFolder = "Docs/Simulation";
    private const string DefaultReportName = "结局分布.md";
    private const string DefaultCsvName = "结局分布.csv";

    [MenuItem("Tools/余音/结局分布模拟")]
    public static void RunFromMenu()
    {
        SimulationOptions options = new SimulationOptions();
        SimulationAcceptance acceptance = new SimulationAcceptance();
        Run(options, acceptance, ReportFolder + "/" + DefaultReportName, ReportFolder + "/" + DefaultCsvName);
    }

    [MenuItem("Tools/余音/结局分布模拟（单局明细）")]
    public static void RunTraceFromMenu()
    {
        SimulationOptions options = new SimulationOptions { runs = 1, traceGames = 1 };
        SimulationAcceptance acceptance = new SimulationAcceptance { checkMinShare = false };
        Run(options, acceptance, null, null);
    }

    /// <summary>Unity -batchmode -executeMethod YuyinSimulatorTool.RunFromCommandLine</summary>
    public static void RunFromCommandLine()
    {
        Dictionary<string, string> args = ParseArguments(Environment.GetCommandLineArgs());
        SimulationOptions options = new SimulationOptions();
        SimulationAcceptance acceptance = new SimulationAcceptance();

        options.runs = GetInt(args, "-simRuns", options.runs);
        options.seedBase = GetInt(args, "-simSeed", options.seedBase);
        options.traceGames = GetInt(args, "-simTraceGames", options.traceGames);
        options.traceIndex = GetInt(args, "-simTrace", options.traceIndex);
        options.policy = ParsePolicy(GetString(args, "-simPolicy", "full"));
        ReadTuning(args, options.tuning);

        acceptance.maxShare = GetFloat(args, "-simMaxShare", acceptance.maxShare);
        acceptance.minShare = GetFloat(args, "-simMinShare", acceptance.minShare);
        if (args.ContainsKey("-simNoMinShare")) acceptance.checkMinShare = false;
        if (args.ContainsKey("-simAllowMissing")) acceptance.requireAllEndings = false;
        if (args.ContainsKey("-simIgnoreMother")) options.ignoreMother = true;

        string report = GetString(args, "-simOut", null);
        string csv = GetString(args, "-simCsv", null);

        int code = Run(options, acceptance, report, csv);
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }

    private static int Run(SimulationOptions options, SimulationAcceptance acceptance, string reportPath, string csvPath)
    {
        GameConfig source = GameConfig.Load();
        if (source == null)
        {
            Debug.LogError("[模拟器] 没有拿到 GameConfig，无法模拟。");
            return 2;
        }

        // 在副本上跑，避免把工程资产改掉。
        GameConfig config = UnityEngine.Object.Instantiate(source);

        // 模拟要覆盖整个放置点表，跑完还原，免得影响打开着的场景。
        List<KeyValuePair<PlacementOption, bool>> savedPlacements = SnapshotPlacements();

        try
        {
            if (options.tuning != null) options.tuning.ApplyTo(config);
            config.EnsureDefaults();

            SimulationReport result = GameSimulator.Run(config, options);
            string text = result.ToText(acceptance, config);

            Debug.Log(text);
            if (Application.isBatchMode) Console.WriteLine(text);

            if (!string.IsNullOrEmpty(reportPath)) WriteTextFile(reportPath, text);
            if (!string.IsNullOrEmpty(csvPath)) WriteTextFile(csvPath, BuildCsv(result));

            List<string> lines = result.AcceptanceLines(acceptance);
            bool pass = lines.Count > 0 && lines[0].Contains("通过") && !lines[0].Contains("不通过");
            return pass ? 0 : 1;
        }
        catch (Exception exception)
        {
            Debug.LogError("[模拟器] 跑挂了：" + exception);
            Console.WriteLine("[模拟器] 跑挂了：" + exception);
            return 2;
        }
        finally
        {
            RestorePlacements(savedPlacements);
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    private static string BuildCsv(SimulationReport report)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("结局,次数,占比");
        for (int i = 0; i < report.endingOrder.Length; i++)
        {
            EndingId id = report.endingOrder[i];
            csv.AppendLine(EndingRules.TitleOf(id) + ","
                + report.endingCounts[(int)id].ToString(CultureInfo.InvariantCulture) + ","
                + (report.endingShares[(int)id] * 100f).ToString("0.00", CultureInfo.InvariantCulture) + "%");
        }
        csv.AppendLine("合计," + report.runs.ToString(CultureInfo.InvariantCulture) + ",100.00%");
        return csv.ToString();
    }

    private static void WriteTextFile(string path, string text)
    {
        string full = Path.GetFullPath(path);
        string folder = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);
        File.WriteAllText(full, text, new UTF8Encoding(false));
        Debug.Log("[模拟器] 已写入：" + full);
    }

    // ------------------------------------------------------------------
    // 放置点：跑模拟前临时铺一套，跑完还原
    // ------------------------------------------------------------------

    private static List<KeyValuePair<PlacementOption, bool>> SnapshotPlacements()
    {
        List<KeyValuePair<PlacementOption, bool>> snapshot = new List<KeyValuePair<PlacementOption, bool>>();
        List<PlacementOption> all = PlacementRegistry.GetAll();
        for (int i = 0; i < all.Count; i++)
        {
            snapshot.Add(new KeyValuePair<PlacementOption, bool>(all[i], PlacementRegistry.IsOccupied(all[i].placementId)));
        }
        return snapshot;
    }

    private static void RestorePlacements(List<KeyValuePair<PlacementOption, bool>> snapshot)
    {
        PlacementRegistry.Clear();
        for (int i = 0; i < snapshot.Count; i++)
        {
            PlacementRegistry.Register(snapshot[i].Key);
            if (snapshot[i].Value) PlacementRegistry.MarkOccupied(snapshot[i].Key.placementId);
        }
    }

    // ------------------------------------------------------------------
    // 参数
    // ------------------------------------------------------------------

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (args == null) return result;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.IsNullOrEmpty(arg) || arg[0] != '-') continue;

            string key = arg;
            string value = "1";
            if (i + 1 < args.Length && (string.IsNullOrEmpty(args[i + 1]) || args[i + 1][0] != '-'))
            {
                value = args[i + 1];
                i++;
            }
            result[key] = value;
        }
        return result;
    }

    private static string GetString(Dictionary<string, string> args, string key, string fallback)
    {
        string value;
        return args.TryGetValue(key, out value) && !string.IsNullOrEmpty(value) ? value : fallback;
    }

    private static int GetInt(Dictionary<string, string> args, string key, int fallback)
    {
        string value;
        int parsed;
        if (args.TryGetValue(key, out value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            return parsed;
        return fallback;
    }

    private static float GetFloat(Dictionary<string, string> args, string key, float fallback)
    {
        string value;
        float parsed;
        if (args.TryGetValue(key, out value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            return parsed;
        return fallback;
    }

    private static SimPlayerPolicy ParsePolicy(string value)
    {
        if (string.IsNullOrEmpty(value)) return SimPlayerPolicy.StayAndInsist;
        switch (value.Trim().ToLowerInvariant())
        {
            case "full": case "fullgame": return SimPlayerPolicy.FullGameAndInsist;
            case "stay": return SimPlayerPolicy.StayAndWait;
            case "insist": case "stayinsist": return SimPlayerPolicy.StayAndInsist;
            case "roam": return SimPlayerPolicy.RoamAndWait;
            case "roaminsist": return SimPlayerPolicy.RoamAndInsist;
            default: return SimPlayerPolicy.FullGameAndInsist;
        }
    }

    private static void ReadTuning(Dictionary<string, string> args, SimulationTuning tuning)
    {
        if (tuning == null) return;
        tuning.wanderWeight = GetFloatOrNull(args, "-simWander", tuning.wanderWeight);
        tuning.talkWeight = GetFloatOrNull(args, "-simTalk", tuning.talkWeight);
        tuning.takeOutWeight = GetFloatOrNull(args, "-simTakeOut", tuning.takeOutWeight);
        tuning.takeBackWeight = GetFloatOrNull(args, "-simTakeBack", tuning.takeBackWeight);
        tuning.girlMoveWeight = GetFloatOrNull(args, "-simGirlMove", tuning.girlMoveWeight);
        tuning.talkMotherChance = GetFloatOrNull(args, "-simTalkMother", tuning.talkMotherChance);
        tuning.absentRoundChance = GetFloatOrNull(args, "-simAbsent", tuning.absentRoundChance);
        tuning.itemTakenWithTemperatureWarmth = GetFloatOrNull(args, "-simWarmTake", tuning.itemTakenWithTemperatureWarmth);
        tuning.itemTakenWithTemperatureAwareness = GetFloatOrNull(args, "-simAwareTake", tuning.itemTakenWithTemperatureAwareness);
        tuning.itemTakenWithoutTemperatureAwareness = GetFloatOrNull(args, "-simAwareTakeCold", tuning.itemTakenWithoutTemperatureAwareness);
        tuning.talkWarmth = GetFloatOrNull(args, "-simWarmTalk", tuning.talkWarmth);
        tuning.talkAwareness = GetFloatOrNull(args, "-simAwareTalk", tuning.talkAwareness);
        tuning.talkMotherAwareness = GetFloatOrNull(args, "-simAwareTalkMother", tuning.talkMotherAwareness);
        tuning.girlMoveWarmth = GetFloatOrNull(args, "-simGirlWarm", tuning.girlMoveWarmth);
        tuning.initialAwareness = GetFloatOrNull(args, "-simInitAwareness", tuning.initialAwareness);
        tuning.motherInitialAwareness = GetFloatOrNull(args, "-simInitAwarenessMother", tuning.motherInitialAwareness);
        tuning.touchesPerRound = GetIntOrNull(args, "-simTouches", tuning.touchesPerRound);
    }

    private static float? GetFloatOrNull(Dictionary<string, string> args, string key, float? fallback)
    {
        string value;
        float parsed;
        if (args.TryGetValue(key, out value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            return parsed;
        return fallback;
    }

    private static int? GetIntOrNull(Dictionary<string, string> args, string key, int? fallback)
    {
        string value;
        int parsed;
        if (args.TryGetValue(key, out value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            return parsed;
        return fallback;
    }
}
