using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 结局判定（8 档）的纯逻辑单测：高 = 数值 &gt; 60，低 = 数值 ≤ 60。
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

    /// <summary>mother = 母亲；others = 老婆婆与大叔（同一档）；girl = 小女孩暖意。</summary>
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
    public void Farewell_MotherOpen_AndOthersHighHigh()
    {
        Arrange(80f, 80f, 80f, 80f, 50f);
        Assert.AreEqual(EndingId.Farewell, Resolve());
    }

    [Test]
    public void MutualConcealment_MotherOpen_OthersKnowButCold()
    {
        Arrange(80f, 80f, 40f, 80f, 50f);
        Assert.AreEqual(EndingId.MutualConcealment, Resolve());
    }

    [Test]
    public void OnlyMotherKnows_MotherOpen_OthersLowLow()
    {
        Arrange(80f, 80f, 40f, 40f, 50f);
        Assert.AreEqual(EndingId.OnlyMotherKnows, Resolve());
    }

    [Test]
    public void NobodySpoke_MotherKnowsButCold_OthersHighHigh()
    {
        Arrange(40f, 80f, 80f, 80f, 50f);
        Assert.AreEqual(EndingId.NobodySpoke, Resolve());
    }

    [Test]
    public void BuildingKnowsSilentToMother_MotherLow_OthersHighHigh()
    {
        Arrange(40f, 40f, 80f, 80f, 50f);
        Assert.AreEqual(EndingId.BuildingKnowsSilentToMother, Resolve());
    }

    [Test]
    public void NobodySpoke_OthersKnowButCold()
    {
        Arrange(40f, 40f, 40f, 80f, 50f);
        Assert.AreEqual(EndingId.NobodySpoke, Resolve());
    }

    [Test]
    public void WholeBuildingUnaware_OthersWarmButUnaware()
    {
        Arrange(40f, 40f, 80f, 40f, 50f);
        Assert.AreEqual(EndingId.WholeBuildingUnaware, Resolve());
    }

    [Test]
    public void RainNeverStopped_AllLow()
    {
        Arrange(40f, 40f, 40f, 40f, 40f);
        Assert.AreEqual(EndingId.RainNeverStopped, Resolve());
    }

    [Test]
    public void OnlyGirlRemembers_AllLowButGirlHigh()
    {
        Arrange(40f, 40f, 40f, 40f, 80f);
        Assert.AreEqual(EndingId.OnlyGirlRemembers, Resolve());
    }

    [Test]
    public void Threshold_60IsLow_61IsHigh()
    {
        // 60 不算高 → 全部低档 → 雨没停
        Arrange(60f, 60f, 60f, 60f, 60f);
        Assert.AreEqual(EndingId.RainNeverStopped, Resolve());

        // 61 算高 → 母亲开门 + 邻居都高 → 告别
        Arrange(61f, 61f, 61f, 61f, 61f);
        Assert.AreEqual(EndingId.Farewell, Resolve());
    }

    [Test]
    public void MissingSession_FallsBackWithoutThrowing()
    {
        Assert.AreEqual(EndingId.RainNeverStopped, EndingRules.ResolveId(null, config));
    }

    [Test]
    public void Titles_MatchDesign()
    {
        Assert.AreEqual("告别", EndingRules.TitleOf(EndingId.Farewell));
        Assert.AreEqual("互相隐瞒", EndingRules.TitleOf(EndingId.MutualConcealment));
        Assert.AreEqual("只有母亲一个人知道", EndingRules.TitleOf(EndingId.OnlyMotherKnows));
        Assert.AreEqual("谁也没开口", EndingRules.TitleOf(EndingId.NobodySpoke));
        Assert.AreEqual("全楼都知道了，却没人敢告诉母亲", EndingRules.TitleOf(EndingId.BuildingKnowsSilentToMother));
        Assert.AreEqual("整栋楼都不知道他已经死了", EndingRules.TitleOf(EndingId.WholeBuildingUnaware));
        Assert.AreEqual("雨没停", EndingRules.TitleOf(EndingId.RainNeverStopped));
        Assert.AreEqual("只有小女孩还记着他", EndingRules.TitleOf(EndingId.OnlyGirlRemembers));
    }
}
