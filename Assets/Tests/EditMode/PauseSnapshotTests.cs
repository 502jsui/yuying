using NUnit.Framework;

/// <summary>暂停现场（截图 + 回来的场景 + 站位）的纯逻辑单测。</summary>
public class PauseSnapshotTests
{
    [TearDown]
    public void TearDown()
    {
        PauseSnapshot.Clear();
    }

    [Test]
    public void SceneNameOf_MapsAreas()
    {
        Assert.AreEqual("FirstFloor", PauseSnapshot.SceneNameOf(AreaId.FirstFloor));
        Assert.AreEqual("SecondFloor", PauseSnapshot.SceneNameOf(AreaId.SecondFloor));
        Assert.AreEqual("Backyard", PauseSnapshot.SceneNameOf(AreaId.Backyard));
        Assert.AreEqual("FirstFloor", PauseSnapshot.SceneNameOf(AreaId.None), "没记区域时兜底一楼");
    }

    [Test]
    public void Store_KeepsReturnScene_AndConsumesPlayerXOnlyOnce()
    {
        PauseSnapshot.Store(null, AreaId.Backyard, -3.25f);

        Assert.IsTrue(PauseSnapshot.HasSnapshot);
        Assert.AreEqual(AreaId.Backyard, PauseSnapshot.ReturnArea);
        Assert.AreEqual("Backyard", PauseSnapshot.ReturnSceneName);

        float x;
        Assert.IsTrue(PauseSnapshot.ConsumePlayerX(out x));
        Assert.AreEqual(-3.25f, x, 0.0001f);

        Assert.IsFalse(PauseSnapshot.ConsumePlayerX(out x), "站位只能被消费一次");
        Assert.IsTrue(PauseSnapshot.HasSnapshot, "场景信息还要留着（恢复时才用）");
    }

    [Test]
    public void Store_WithUnknownArea_FallsBackToFirstFloor()
    {
        PauseSnapshot.Store(null, AreaId.None, 0f);
        Assert.AreEqual(AreaId.FirstFloor, PauseSnapshot.ReturnArea);
    }

    [Test]
    public void Clear_ForgetsEverything()
    {
        PauseSnapshot.Store(null, AreaId.Backyard, 1.5f);
        PauseSnapshot.Clear();

        Assert.IsFalse(PauseSnapshot.HasSnapshot);
        Assert.AreEqual(AreaId.None, PauseSnapshot.ReturnArea);

        float x;
        Assert.IsFalse(PauseSnapshot.ConsumePlayerX(out x));
    }
}
