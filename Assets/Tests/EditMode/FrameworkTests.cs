using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class FrameworkTests
{
    private GameConfig config;

    [SetUp]
    public void SetUp()
    {
        EventBus.Clear();
        config = ScriptableObject.CreateInstance<GameConfig>();
        config.EnsureDefaults();
    }

    [TearDown]
    public void TearDown()
    {
        EventBus.Clear();
        if (config != null) Object.DestroyImmediate(config);
    }

    // ---------------- 配置 ----------------

    [Test]
    public void Config_Defaults_MatchDesign()
    {
        Assert.AreEqual(6, config.totalRounds);
        Assert.AreEqual(1, config.touchesPerRound);
        Assert.AreEqual(60f, config.highThreshold);
        Assert.AreEqual(30f, config.doorSeamWarmth);
        Assert.AreEqual(55f, config.doorHalfWarmth);
        Assert.AreEqual(4, config.residents.Count);
    }

    [Test]
    public void Config_ResidentInitialValues_MatchDesign()
    {
        Assert.AreEqual(26f, config.GetResidentConfig(ResidentId.Grandma).warmth);
        Assert.AreEqual(20f, config.GetResidentConfig(ResidentId.Grandma).awareness);
        Assert.AreEqual(26f, config.GetResidentConfig(ResidentId.Uncle).warmth);
        Assert.AreEqual(20f, config.GetResidentConfig(ResidentId.Uncle).awareness);
        Assert.AreEqual(30f, config.GetResidentConfig(ResidentId.Girl).warmth);
        Assert.IsFalse(config.GetResidentConfig(ResidentId.Girl).hasAwareness);
        Assert.AreEqual(30f, config.GetResidentConfig(ResidentId.Mother).warmth);
        Assert.AreEqual(30f, config.GetResidentConfig(ResidentId.Mother).awareness);
    }

    // ---------------- 随机 ----------------

    [Test]
    public void Random_SameSeed_ProducesSameSequence()
    {
        RandomService a = new RandomService(12345);
        RandomService b = new RandomService(12345);

        for (int i = 0; i < 32; i++)
        {
            Assert.AreEqual(a.Value(), b.Value(), 0.0000001f);
        }
    }

    [Test]
    public void Random_DifferentSeed_ProducesDifferentSequence()
    {
        RandomService a = new RandomService(1);
        RandomService b = new RandomService(2);
        bool different = false;
        for (int i = 0; i < 16 && !different; i++)
        {
            if (!Mathf.Approximately(a.Value(), b.Value())) different = true;
        }
        Assert.IsTrue(different);
    }

    [Test]
    public void Random_Chance_RespectsBounds()
    {
        RandomService random = new RandomService(7);
        for (int i = 0; i < 100; i++)
        {
            Assert.IsFalse(random.Chance(0f));
            Assert.IsTrue(random.Chance(1f));
        }
    }

    [Test]
    public void Random_WeightedIndex_PicksByWeight()
    {
        RandomService random = new RandomService(99);
        List<float> weights = new List<float> { 0f, 100f, 0f };
        for (int i = 0; i < 50; i++)
        {
            Assert.AreEqual(1, random.WeightedIndex(weights));
        }
        Assert.AreEqual(-1, random.WeightedIndex(new List<float> { 0f, 0f }));
    }

    // ---------------- 本局数据 ----------------

    [Test]
    public void Session_Reset_CreatesFourResidents()
    {
        GameSession session = new GameSession();
        session.Reset(config, 42);

        Assert.AreEqual(42, session.seed);
        Assert.AreEqual(1, session.round);
        Assert.AreEqual(4, session.residents.Count);
        Assert.AreEqual(26f, session.GetResident(ResidentId.Grandma).warmth);
        Assert.IsFalse(session.GetResident(ResidentId.Mother).canAct);
        Assert.AreEqual(0, session.items.Count);
    }

    [Test]
    public void Session_EnsureResident_IsIdempotent()
    {
        GameSession session = new GameSession();
        session.Reset(config, 1);
        ResidentData first = session.EnsureResident(ResidentId.Grandma, config);
        ResidentData second = session.EnsureResident(ResidentId.Grandma, config);
        Assert.AreSame(first, second);
        Assert.AreEqual(4, session.residents.Count);
    }

    [Test]
    public void Session_EnsureItem_IsIdempotent()
    {
        GameSession session = new GameSession();
        session.Reset(config, 1);
        ItemData first = session.EnsureItem("grandma_01", ItemOwner.Grandma, ItemType.Clothes);
        ItemData second = session.EnsureItem("grandma_01", ItemOwner.Grandma, ItemType.Clothes);
        Assert.AreSame(first, second);
        Assert.AreEqual(1, session.items.Count);
        Assert.AreEqual(ItemType.Clothes, first.type);
    }

    [Test]
    public void Session_AddWarmth_ClampsAndPublishesEvent()
    {
        GameSession session = new GameSession();
        session.Reset(config, 1);

        float observed = -1f;
        System.Action<WarmthChangedEvent> handler = evt => observed = evt.current;
        EventBus.Subscribe(handler);

        session.AddWarmth(ResidentId.Grandma, 1000f, config);

        Assert.AreEqual(config.maxStatValue, session.GetResident(ResidentId.Grandma).warmth);
        Assert.AreEqual(config.maxStatValue, observed);
        EventBus.Unsubscribe(handler);
    }

    [Test]
    public void Session_HasItemInPool_TracksState()
    {
        GameSession session = new GameSession();
        session.Reset(config, 1);
        ItemData item = session.EnsureItem("aunt_01", ItemOwner.Uncle, ItemType.FlowerPot);
        Assert.IsTrue(session.HasItemInPool(ItemOwner.Uncle));

        item.isInPool = false;
        item.position = ItemPosition.Scene;
        item.area = AreaId.Backyard;
        Assert.IsFalse(session.HasItemInPool(ItemOwner.Uncle));
        Assert.AreEqual(1, session.GetItemsInArea(AreaId.Backyard).Count);
    }

    [Test]
    public void Session_SnapshotRoundTrip_PreservesState()
    {
        GameSession session = new GameSession();
        session.Reset(config, 321);
        session.round = 4;
        session.forceTouchWarningsUsed = 1;
        session.AddWarmth(ResidentId.Grandma, 10f, config);
        session.EnsureItem("grandma_01", ItemOwner.Grandma, ItemType.Clothes);

        GameSessionSnapshot snapshot = session.CreateSnapshot();
        GameSession restored = new GameSession();
        restored.LoadSnapshot(snapshot);

        Assert.AreEqual(321, restored.seed);
        Assert.AreEqual(4, restored.round);
        Assert.AreEqual(1, restored.forceTouchWarningsUsed);
        Assert.AreEqual(
            session.GetResident(ResidentId.Grandma).warmth,
            restored.GetResident(ResidentId.Grandma).warmth);
        Assert.IsNotNull(restored.GetItem("grandma_01"));
    }

    // ---------------- 回合规则 ----------------

    [Test]
    public void TurnRules_AbsentChanceZero_NeverAbsent()
    {
        config.absentRoundChance = 0f;
        GameSession session = new GameSession();
        session.Reset(config, 1);

        RandomService random = new RandomService(5);
        ResidentId absent;
        for (int i = 0; i < 50; i++)
        {
            Assert.IsFalse(TurnRules.TryPickAbsentResident(random, config, session.residents, out absent));
        }
    }

    [Test]
    public void TurnRules_AbsentChanceOne_PicksActingResidentOnly()
    {
        config.absentRoundChance = 1f;
        GameSession session = new GameSession();
        session.Reset(config, 1);

        RandomService random = new RandomService(5);
        for (int i = 0; i < 50; i++)
        {
            ResidentId absent;
            Assert.IsTrue(TurnRules.TryPickAbsentResident(random, config, session.residents, out absent));
            Assert.AreNotEqual(ResidentId.Mother, absent);
        }
    }

    [Test]
    public void TurnRules_IsHighValue_UsesStrictGreaterThan()
    {
        Assert.IsFalse(TurnRules.IsHighValue(60f, config));
        Assert.IsTrue(TurnRules.IsHighValue(60.5f, config));
    }

    [Test]
    public void TurnRules_ShouldSomeoneStayOut_UsesAbsentChance()
    {
        config.absentRoundChance = 0f;
        Assert.IsFalse(TurnRules.ShouldSomeoneStayOut(new RandomService(1), config), "0% 应该全员在家。");

        config.absentRoundChance = 1f;
        Assert.IsTrue(TurnRules.ShouldSomeoneStayOut(new RandomService(1), config), "100% 应该有人不回家。");
    }

    [Test]
    public void TurnRules_TryPickWhoStaysOut_NeverPicksMother()
    {
        GameSession session = new GameSession();
        session.Reset(config, 1);
        RandomService random = new RandomService(3);

        for (int i = 0; i < 100; i++)
        {
            ResidentId absent;
            Assert.IsTrue(TurnRules.TryPickWhoStaysOut(random, session.residents, out absent));
            Assert.AreNotEqual(ResidentId.Mother, absent, "母亲不行动，不应该被选中。");
        }
    }

    // ---------------- 事件总线 ----------------

    [Test]
    public void EventBus_SubscribePublishUnsubscribe()
    {
        int count = 0;
        System.Action<RoundStartedEvent> handler = evt => count++;

        EventBus.Subscribe(handler);
        EventBus.Publish(new RoundStartedEvent { round = 1 });
        Assert.AreEqual(1, count);

        EventBus.Unsubscribe(handler);
        EventBus.Publish(new RoundStartedEvent { round = 2 });
        Assert.AreEqual(1, count);
    }

    // ---------------- 门的规则 ----------------

    [Test]
    public void DoorLight_OpenState_MatchesDesign()
    {
        GameObject go = new GameObject("DoorLightTest");
        DoorLightView door = go.AddComponent<DoorLightView>();

        Assert.AreEqual(DoorLightView.OpenState.Closed, door.ResolveOpenState(29.9f));
        Assert.AreEqual(DoorLightView.OpenState.Seam, door.ResolveOpenState(30f));
        Assert.AreEqual(DoorLightView.OpenState.Seam, door.ResolveOpenState(54.9f));
        Assert.AreEqual(DoorLightView.OpenState.HalfOpen, door.ResolveOpenState(55f));

        Object.DestroyImmediate(go);
    }

    [Test]
    public void DoorLight_Color_MatchesDesign()
    {
        GameObject go = new GameObject("DoorLightTest");
        DoorLightView door = go.AddComponent<DoorLightView>();

        Assert.AreEqual(door.whiteLight, door.ResolveLightColor(29f, true));
        Assert.AreEqual(door.paleYellowLight, door.ResolveLightColor(30f, true));
        Assert.AreEqual(door.paleYellowLight, door.ResolveLightColor(54f, true));
        Assert.AreEqual(door.orangeLight, door.ResolveLightColor(55f, true));
        Assert.AreEqual(door.whiteLight, door.ResolveLightColor(90f, false));

        Object.DestroyImmediate(go);
    }

    // ---------------- 相机取景 ----------------

    [Test]
    public void Camera_WholeScene_FillsScreen_AtDesignAspect()
    {
        GameObject go = new GameObject("CameraFramingTest");
        Camera camera = go.AddComponent<Camera>();
        SceneCameraFollow2D framing = go.AddComponent<SceneCameraFollow2D>();
        framing.sceneMinX = -9.6f;
        framing.sceneMaxX = 9.6f;
        framing.sceneMinY = -5.4f;
        framing.sceneMaxY = 5.4f;

        // 19.2 宽、10.8 高，16:9 下刚好整屏，所以正交尺寸应为 5.4。
        Assert.AreEqual(5.4f, framing.ComputeOrthographicSize(), 0.001f);

        framing.ApplyFraming();
        Assert.AreEqual(0f, go.transform.position.x, 0.001f);
        Assert.AreEqual(0f, go.transform.position.y, 0.001f);
        Assert.AreEqual(5.4f, camera.orthographicSize, 0.001f);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Camera_VisibleRect_AlwaysCoversWholeScene()
    {
        GameObject go = new GameObject("CameraCoverTest");
        Camera camera = go.AddComponent<Camera>();
        SceneCameraFollow2D framing = go.AddComponent<SceneCameraFollow2D>();
        framing.sceneMinX = -9.6f;
        framing.sceneMaxX = 9.6f;
        framing.sceneMinY = -5.4f;
        framing.sceneMaxY = 5.4f;

        float size = framing.ComputeOrthographicSize();
        float halfHeight = size;
        float halfWidth = size * SceneCameraFollow2D.DesignAspect;
        float centerX = (framing.sceneMinX + framing.sceneMaxX) * 0.5f;
        float centerY = (framing.sceneMinY + framing.sceneMaxY) * 0.5f;

        Assert.LessOrEqual(centerX - halfWidth, framing.sceneMinX + 0.001f);
        Assert.GreaterOrEqual(centerX + halfWidth, framing.sceneMaxX - 0.001f);
        Assert.LessOrEqual(centerY - halfHeight, framing.sceneMinY + 0.001f);
        Assert.GreaterOrEqual(centerY + halfHeight, framing.sceneMaxY - 0.001f);

        Object.DestroyImmediate(go);
    }
}
