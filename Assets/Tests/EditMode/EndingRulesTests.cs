using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 八档旧结局合并为三档后的纯逻辑单测。
/// 高 = 数值 &gt; 60，低 = 数值 ≤ 60。
/// </summary>
public class EndingRulesTests
{
    private GameConfig config;
    private GameSession session;

    [SetUp]
    public void SetUp()
    {
        EventBus.Clear();
        config = ScriptableObject.CreateInstance<GameConfig>();
        config.EnsureDefaults();

        session = new GameSession();
        session.Reset(config, 12345);
    }

    [TearDown]
    public void TearDown()
    {
        EventBus.Clear();
        if (config != null) Object.DestroyImmediate(config);
    }

    private void Arrange(float motherWarmth, float motherAwareness, float othersWarmth, float othersAwareness, float girlWarmth)
    {
        Set(ResidentId.Mother, motherWarmth, motherAwareness);
        Set(ResidentId.Grandma, othersWarmth, othersAwareness);
        Set(ResidentId.Uncle, othersWarmth, othersAwareness);
        Set(ResidentId.Girl, girlWarmth, 0f);
    }

    private void Set(ResidentId id, float warmth, float awareness)
    {
        ResidentData resident = session.GetResident(id);
        resident.warmth = warmth;
        resident.awareness = awareness;
    }

    private EndingId Resolve()
    {
        return EndingRules.ResolveId(session, config);
    }

    [Test]
    public void Farewell_BecomesBeforeSnowStops()
    {
        Arrange(80f, 80f, 80f, 80f, 50f);
        Assert.AreEqual(EndingId.BeforeSnowStops, Resolve());
    }

    [Test]
    public void PreviouslyMutualConcealment_BecomesWhenSheWasLittle()
    {
        Arrange(80f, 80f, 40f, 80f, 50f);
        Assert.AreEqual(EndingId.WhenSheWasLittle, Resolve());
    }

    [Test]
    public void PreviouslyOnlyMotherKnows_BecomesWhenSheWasLittle()
    {
        Arrange(80f, 80f, 40f, 40f, 50f);
        Assert.AreEqual(EndingId.WhenSheWasLittle, Resolve());
    }

    [Test]
    public void PreviouslyNobodySpoke_BecomesWhenSheWasLittle()
    {
        Arrange(40f, 80f, 80f, 80f, 50f);
        Assert.AreEqual(EndingId.WhenSheWasLittle, Resolve());
    }

    [Test]
    public void PreviouslyBuildingKnowsSilentToMother_BecomesWhenSheWasLittle()
    {
        Arrange(40f, 40f, 80f, 80f, 50f);
        Assert.AreEqual(EndingId.WhenSheWasLittle, Resolve());
    }

    [Test]
    public void PreviouslyNobodySpokeCold_BecomesWhenSheWasLittle()
    {
        Arrange(40f, 40f, 40f, 80f, 50f);
        Assert.AreEqual(EndingId.WhenSheWasLittle, Resolve());
    }

    [Test]
    public void PreviouslyWholeBuildingUnaware_BecomesRainInTheCorridor()
    {
        Arrange(40f, 40f, 80f, 40f, 50f);
        Assert.AreEqual(EndingId.RainInTheCorridor, Resolve());
    }

    [Test]
    public void PreviouslyRainNeverStopped_BecomesWhenSheWasLittle()
    {
        Arrange(40f, 40f, 40f, 40f, 40f);
        Assert.AreEqual(EndingId.WhenSheWasLittle, Resolve());
    }

    [Test]
    public void PreviouslyOnlyGirlRemembers_BecomesWhenSheWasLittle()
    {
        Arrange(40f, 40f, 40f, 40f, 80f);
        Assert.AreEqual(EndingId.WhenSheWasLittle, Resolve());
    }

    [Test]
    public void Threshold_60IsLow_61IsHigh()
    {
        Arrange(60f, 60f, 60f, 60f, 60f);
        Assert.AreEqual(EndingId.WhenSheWasLittle, Resolve());

        Arrange(61f, 61f, 61f, 61f, 61f);
        Assert.AreEqual(EndingId.BeforeSnowStops, Resolve());
    }

    [Test]
    public void RainInTheCorridor_IgnoresMotherWarmthAndGirlWarmth()
    {
        Arrange(0f, 40f, 80f, 40f, 100f);
        Assert.AreEqual(EndingId.RainInTheCorridor, Resolve());
    }

    [Test]
    public void MissingSession_FallsBackToRainInTheCorridor()
    {
        Assert.AreEqual(EndingId.RainInTheCorridor, EndingRules.ResolveId(null, config));
    }

    [Test]
    public void Titles_MatchPDF()
    {
        Assert.AreEqual("楼道里的雨", EndingRules.TitleOf(EndingId.RainInTheCorridor));
        Assert.AreEqual("她小时候", EndingRules.TitleOf(EndingId.WhenSheWasLittle));
        Assert.AreEqual("雪停之前", EndingRules.TitleOf(EndingId.BeforeSnowStops));
    }
}
