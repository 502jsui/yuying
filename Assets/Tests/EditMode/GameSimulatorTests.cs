using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 命令行模拟器（程序分工 7.4）的自测：能跑完、能复现、分布数据自洽。
/// 真正的 500 局验收在 YuyinSimulatorTool 里跑，单测只跑小样本，避免拖慢 Test Runner。
/// </summary>
public class GameSimulatorTests
{
    private GameConfig config;

    [SetUp]
    public void SetUp()
    {
        EventBus.Clear();
        PlacementRegistry.Clear();

        config = ScriptableObject.CreateInstance<GameConfig>();
        config.EnsureDefaults();
    }

    [TearDown]
    public void TearDown()
    {
        EventBus.Clear();
        PlacementRegistry.Clear();
        if (config != null) Object.DestroyImmediate(config);
    }

    [Test]
    public void Simulator_SameSeedBase_ProducesSameEndings()
    {
        SimulationOptions first = new SimulationOptions { runs = 30, seedBase = 777 };
        SimulationReport a = GameSimulator.Run(config, first);
        SimulationReport b = GameSimulator.Run(config, first);

        Assert.AreEqual(a.games.Count, b.games.Count);
        for (int i = 0; i < a.games.Count; i++)
        {
            Assert.AreEqual(a.games[i].seed, b.games[i].seed);
            Assert.AreEqual(a.games[i].ending, b.games[i].ending, "第 " + i + " 局的结局应该可复现");
        }
    }

    [Test]
    public void Simulator_DifferentSeedBase_SeedsDiffer()
    {
        Assert.AreNotEqual(GameSimulator.DeriveSeed(1, 0), GameSimulator.DeriveSeed(2, 0));
        Assert.AreNotEqual(GameSimulator.DeriveSeed(1, 0), GameSimulator.DeriveSeed(1, 1));
    }

    [Test]
    public void Simulator_DefaultPolicy_IsFullGame()
    {
        Assert.AreEqual(SimPlayerPolicy.FullGameAndInsist, new SimulationOptions().policy);
    }

    [Test]
    public void Simulator_UsesCanonicalPlacementLayout()
    {
        GameSimulator.InstallCanonicalPlacements();

        List<PlacementOption> actual = PlacementRegistry.GetAll();
        Assert.AreEqual(ScenePlacementLayout.Count, actual.Count);

        for (int i = 0; i < ScenePlacementLayout.Count; i++)
        {
            ScenePlacementSpec expected = ScenePlacementLayout.Get(i);
            PlacementOption option;
            Assert.IsTrue(PlacementRegistry.TryGet(expected.id, out option), "模拟器缺少放置点 " + expected.id);
            Assert.AreEqual(expected.area, option.area);
            Assert.AreEqual(expected.surface, option.surface);
        }
    }

    [Test]
    public void Simulator_ReportCountsAreConsistent()
    {
        SimulationReport report = GameSimulator.Run(config, new SimulationOptions { runs = 120 });

        Assert.AreEqual(3, GameSimulator.AllEndings.Length);
        Assert.AreEqual(120, report.games.Count);
        Assert.AreEqual(120, report.runs);
        Assert.AreEqual(GameSimulator.AllEndings.Length, report.endingCounts.Length);

        int total = 0;
        float shareTotal = 0f;
        for (int i = 0; i < report.endingCounts.Length; i++)
        {
            Assert.GreaterOrEqual(report.endingCounts[i], 0);
            total += report.endingCounts[i];
            shareTotal += report.endingShares[i];
        }

        Assert.AreEqual(120, total, "每个结局的次数加起来应该等于总局数");
        Assert.AreEqual(1f, shareTotal, 0.001f);
        Assert.GreaterOrEqual(report.motherDoorShare, 0f);
        Assert.LessOrEqual(report.motherDoorShare, 1f);
        int maxCount = 0;
        for (int i = 0; i < report.games.Count; i++)
        {
            if (report.games[i].ending == report.maxEnding) maxCount++;
        }
        Assert.AreEqual(maxCount, report.endingCounts[(int)report.maxEnding]);
        Assert.AreEqual(maxCount, Mathf.RoundToInt(report.maxShare * report.games.Count));
        Assert.GreaterOrEqual(report.maxShare, report.minShare);
        Assert.AreEqual(
            report.endingCounts[(int)EndingId.RainInTheCorridor],
            report.buildingUnawareCandidates,
            "BE 诊断计数必须和最终结局计数一致");
    }

    [Test]
    public void Simulator_EveryEndingIdIsOneOfTheKnownOnes()
    {
        SimulationReport report = GameSimulator.Run(config, new SimulationOptions { runs = 60 });

        for (int i = 0; i < report.games.Count; i++)
        {
            EndingId id = report.games[i].ending;
            Assert.IsTrue(System.Enum.IsDefined(typeof(EndingId), id), "出现了没见过的结局：" + id);
        }
    }

    [Test]
    public void Simulator_AllPlayerPolicies_RunWithoutExceptions()
    {
        SimPlayerPolicy[] policies =
        {
            SimPlayerPolicy.StayAndWait,
            SimPlayerPolicy.StayAndInsist,
            SimPlayerPolicy.RoamAndWait,
            SimPlayerPolicy.RoamAndInsist,
            SimPlayerPolicy.FullGameAndInsist
        };

        for (int i = 0; i < policies.Length; i++)
        {
            SimulationReport report = GameSimulator.Run(config, new SimulationOptions
            {
                runs = 20,
                seedBase = 100 + i,
                policy = policies[i]
            });
            Assert.AreEqual(20, report.games.Count, policies[i] + " 没跑完");
        }
    }

    [Test]
    public void Simulator_FullGamePolicy_CoversSecondFloorMotherItemsAndConfusion()
    {
        SimulationReport report = GameSimulator.Run(config, new SimulationOptions
        {
            runs = 20,
            seedBase = 4321,
            policy = SimPlayerPolicy.FullGameAndInsist
        });

        int motherTouches = 0;
        int confusions = 0;
        for (int i = 0; i < report.games.Count; i++)
        {
            motherTouches += report.games[i].motherItemTouches;
            confusions += report.games[i].confusions;
        }

        Assert.Greater(motherTouches, 0, "完整局策略应上二楼并触碰母亲物品");
        Assert.Greater(confusions, 0, "触碰母亲物品后应触发疑惑");
        Assert.Greater(report.averageMotherItemTouches, 0f);
        Assert.Greater(report.averageConfusions, 0f);
    }

    [Test]
    public void Simulator_Trace_ContainsRoundsAndEnding()
    {
        SimulationReport report = GameSimulator.Run(config, new SimulationOptions
        {
            runs = 2,
            traceGames = 1
        });

        Assert.IsNotEmpty(report.trace);
        string text = string.Join("\n", report.trace.ToArray());
        StringAssert.Contains("第 1 轮", text);
        StringAssert.Contains("结局：", text);
    }

    [Test]
    public void Simulator_AcceptanceLines_ReportFailureWhenEndingMissing()
    {
        SimulationReport report = GameSimulator.Run(config, new SimulationOptions { runs = 1 });
        SimulationAcceptance acceptance = new SimulationAcceptance
        {
            requireAllEndings = true,
            checkMinShare = false,
            maxShare = 1f
        };

        List<string> lines = report.AcceptanceLines(acceptance);
        Assert.IsNotEmpty(lines);
        StringAssert.Contains("不通过", lines[0], "只跑 1 局不可能出现 3 个结局，应该判失败");
    }

    [Test]
    public void Simulator_IgnoreMother_MotherNeverOpensTheDoor()
    {
        SimulationReport report = GameSimulator.Run(config, new SimulationOptions
        {
            runs = 40,
            ignoreMother = true
        });

        Assert.IsFalse(report.motherEnabled);
        Assert.AreEqual(0f, report.motherDoorShare, 0.0001f, "母亲不参与时她不该开门");
        Assert.AreEqual(0, report.endingCounts[(int)EndingId.BeforeSnowStops]);

        for (int i = 0; i < report.games.Count; i++)
        {
            Assert.AreEqual(30f, report.games[i].motherAwareness, 0.001f,
                "母亲不参与时她的知晓度不该变化");
        }
    }

    [Test]
    public void Simulator_Tuning_AppliesOnlyFilledFields()
    {
        SimulationTuning tuning = new SimulationTuning { talkMotherChance = 0.55f };
        tuning.ApplyTo(config);

        Assert.AreEqual(0.55f, config.talkMotherChance, 0.0001f);
        Assert.AreEqual(35f, config.wanderWeight, "没写的字段不该被改");
    }
}
