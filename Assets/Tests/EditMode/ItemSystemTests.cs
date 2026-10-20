using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 物品系统的纯逻辑单测：物品池、放置规则、温度、拿回、小女孩移动、母亲物品、强行触碰。
/// 全部不依赖场景，直接构造 GameConfig + GameSession + ItemSystem。
/// </summary>
public class ItemSystemTests
{
    private GameConfig config;
    private GameSession session;
    private ItemSystem items;

    [SetUp]
    public void SetUp()
    {
        EventBus.Clear();
        PlacementRegistry.Clear();
        config = ScriptableObject.CreateInstance<GameConfig>();
        config.EnsureDefaults();

        session = new GameSession();
        session.Reset(config, 12345);

        items = new ItemSystem(config, new RandomService(12345), session, null);
        items.InitializeFromConfig();
    }

    [TearDown]
    public void TearDown()
    {
        EventBus.Clear();
        PlacementRegistry.Clear();
        if (config != null) Object.DestroyImmediate(config);
    }

    // ---------------- 配置 ----------------

    [Test]
    public void Config_DefaultItems_MatchDesign()
    {
        Assert.AreEqual(9, config.items.Count);

        int demoItems = 0;
        for (int i = 0; i < config.items.Count; i++)
        {
            if (config.items[i].owner != ItemOwner.Mother) demoItems++;
        }
        // 本次 Demo 共 6 件（老婆婆 3 + 大叔 3），母亲 3 件预留。
        Assert.AreEqual(6, demoItems);
        Assert.AreEqual(ItemType.Clothes, config.GetItemConfig("grandma_01").type);
        Assert.AreEqual(ItemType.FlowerPot, config.GetItemConfig("aunt_01").type);
    }

    [Test]
    public void InitializeFromConfig_ClothesStartOnBackyardRack()
    {
        Assert.AreEqual(9, session.items.Count);

        ItemData clothes = items.GetItem("grandma_01");
        Assert.IsTrue(clothes.IsInScene);
        Assert.AreEqual(AreaId.Backyard, clothes.area);
        Assert.AreEqual("backyard_rack_a", clothes.placementId);

        ItemData pot = items.GetItem("aunt_01");
        Assert.IsTrue(pot.IsInScene);

        ItemData poolItem = items.GetItem("grandma_02");
        Assert.IsFalse(poolItem.IsInScene);
        Assert.IsTrue(poolItem.isInPool);
    }

    // ---------------- 放置规则（文档 2.6） ----------------

    [Test]
    public void PlacementRules_MatchDesign()
    {
        // 衣服只能晾衣架，不能落地。
        Assert.IsTrue(ItemPlacementRules.CanPlace(ItemType.Clothes, PlacementSurface.HangingRack));
        Assert.IsFalse(ItemPlacementRules.CanPlace(ItemType.Clothes, PlacementSurface.Ground));

        // 花盆：地上 / 别的物品上。
        Assert.IsTrue(ItemPlacementRules.CanPlace(ItemType.FlowerPot, PlacementSurface.Ground));
        Assert.IsTrue(ItemPlacementRules.CanPlace(ItemType.FlowerPot, PlacementSurface.OnItem));
        Assert.IsFalse(ItemPlacementRules.CanPlace(ItemType.FlowerPot, PlacementSurface.HangingRack));

        // 药包 / 工具包 / 木雕 / 日记：同花盆但不能落地。
        Assert.IsFalse(ItemPlacementRules.CanPlace(ItemType.MedicineBag, PlacementSurface.Ground));
        Assert.IsTrue(ItemPlacementRules.CanPlace(ItemType.MedicineBag, PlacementSurface.OnItem));
        Assert.IsFalse(ItemPlacementRules.CanPlace(ItemType.ToolBag, PlacementSurface.Ground));
        Assert.IsTrue(ItemPlacementRules.CanPlace(ItemType.ToolBag, PlacementSurface.OnItem));
        Assert.IsFalse(ItemPlacementRules.CanPlace(ItemType.WoodCarving, PlacementSurface.Ground));
        Assert.IsTrue(ItemPlacementRules.CanPlace(ItemType.WoodCarving, PlacementSurface.OnItem));
        Assert.IsFalse(ItemPlacementRules.CanPlace(ItemType.Diary, PlacementSurface.Ground));
        Assert.IsTrue(ItemPlacementRules.CanPlace(ItemType.Diary, PlacementSurface.OnItem));

        // 木箱：只能落地。
        Assert.IsTrue(ItemPlacementRules.CanPlace(ItemType.WoodenBox, PlacementSurface.Ground));
        Assert.IsFalse(ItemPlacementRules.CanPlace(ItemType.WoodenBox, PlacementSurface.OnItem));
    }

    [Test]
    public void TakeOut_BlockedByPlacementRule()
    {
        // grandma_02 是花盆，不能放晾衣架。
        ItemActionResult blocked = items.TakeOut("grandma_02", AreaId.FirstFloor, "rack", PlacementSurface.HangingRack);
        Assert.AreEqual(ItemActionStatus.PlacementBlocked, blocked.status);
        Assert.IsTrue(items.GetItem("grandma_02").isInPool);

        ItemActionResult ok = items.TakeOut("grandma_02", AreaId.FirstFloor, "ground_a", PlacementSurface.Ground);
        Assert.IsTrue(ok.IsSuccess);
        Assert.IsTrue(items.GetItem("grandma_02").IsInScene);
        Assert.IsFalse(items.GetItem("grandma_02").isInPool);
    }

    [Test]
    public void TakeOut_PublishesItemAppearedEvent()
    {
        ItemAppearedEvent observed = default(ItemAppearedEvent);
        bool fired = false;
        System.Action<ItemAppearedEvent> handler = evt =>
        {
            fired = true;
            observed = evt;
        };
        EventBus.Subscribe(handler);

        items.TakeOut("grandma_02", AreaId.Backyard, "backyard_ground_a", PlacementSurface.Ground);

        Assert.IsTrue(fired);
        Assert.AreEqual("grandma_02", observed.itemId);
        Assert.AreEqual(AreaId.Backyard, observed.area);
        Assert.AreEqual("backyard_ground_a", observed.placementId);

        EventBus.Unsubscribe(handler);
    }

    [Test]
    public void CollectMotherItems_RemovesAllOfThem()
    {
        int collected = items.CollectMotherItems();

        Assert.AreEqual(3, collected, "母亲有 3 件物品。");
        Assert.IsTrue(items.GetItem("mother_01").isReturned);
        Assert.IsTrue(items.GetItem("mother_02").isReturned);
        Assert.IsTrue(items.GetItem("mother_03").isReturned);
        Assert.IsFalse(items.GetItem("mother_01").isInPool);
    }

    // ---------------- 物品池流转 ----------------

    [Test]
    public void FindNextPoolItem_ReturnsSuccessiveItems_UntilPoolEmpty()
    {
        ItemData first = items.FindNextPoolItem(ItemOwner.Grandma);
        Assert.AreEqual("grandma_02", first.itemId);
        Assert.IsTrue(items.TakeOut(first.itemId, AreaId.FirstFloor, "g", PlacementSurface.Ground).IsSuccess);

        ItemData second = items.FindNextPoolItem(ItemOwner.Grandma);
        Assert.AreEqual("grandma_03", second.itemId);
        Assert.IsTrue(items.TakeOut(second.itemId, AreaId.FirstFloor, "o", PlacementSurface.OnItem).IsSuccess);

        Assert.IsFalse(items.HasItemInPool(ItemOwner.Grandma));
        Assert.IsNull(items.FindNextPoolItem(ItemOwner.Grandma));
    }

    [Test]
    public void EnsureSceneItems_FillsUpToTarget_WithRandomPoolItems()
    {
        PlacementRegistry.Register(new PlacementOption { placementId = "t_ground_a", area = AreaId.FirstFloor, surface = PlacementSurface.Ground });
        PlacementRegistry.Register(new PlacementOption { placementId = "t_ground_b", area = AreaId.FirstFloor, surface = PlacementSurface.Ground });
        PlacementRegistry.Register(new PlacementOption { placementId = "t_on_item", area = AreaId.FirstFloor, surface = PlacementSurface.OnItem });

        int before = items.GetSceneItems(AreaId.FirstFloor).Count;
        Assert.AreEqual(1, before, "一楼开局只有大叔的花盆。");

        int placed = items.EnsureSceneItems(AreaId.FirstFloor, before + 2);

        Assert.AreEqual(2, placed, "应该随机补 2 件。");
        Assert.AreEqual(before + 2, items.GetSceneItems(AreaId.FirstFloor).Count);

        // 已经够了就不再多摆。
        Assert.AreEqual(0, items.EnsureSceneItems(AreaId.FirstFloor, before + 2));
    }

    [Test]
    public void Return_WithTemperature_AddsWarmthAndAwareness_AndRetires()
    {
        ItemData item = items.GetItem("aunt_01");
        item.hasTemperature = true;

        ResidentData aunt = session.GetResident(ResidentId.Uncle);
        float warmthBefore = aunt.warmth;
        float awarenessBefore = aunt.awareness;

        bool takenEvent = false;
        System.Action<ItemTakenEvent> handler = evt =>
        {
            if (evt.itemId == "aunt_01") takenEvent = evt.hadTemperature;
        };
        EventBus.Subscribe(handler);

        ItemActionResult result = items.Return("aunt_01", ResidentId.Uncle);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(takenEvent);
        Assert.AreEqual(warmthBefore + config.itemTakenWithTemperatureWarmth, aunt.warmth, 0.001f);
        Assert.AreEqual(awarenessBefore + config.itemTakenWithTemperatureAwareness, aunt.awareness, 0.001f);
        // 异常情况.pdf：取消「物品不会被第二次拿出」，收回后回到物品池。
        Assert.IsFalse(item.isReturned);
        Assert.IsTrue(item.isInPool);
        Assert.IsFalse(item.IsInScene);

        EventBus.Unsubscribe(handler);
    }

    [Test]
    public void ReturnedItem_CanBeTakenOutAgain()
    {
        items.Return("aunt_01", ResidentId.Uncle);

        ItemActionResult again = items.TakeOut("aunt_01", AreaId.FirstFloor, "first_ground_b", PlacementSurface.Ground);

        Assert.IsTrue(again.IsSuccess, "收回的东西应该可以再次被拿出来。");
        Assert.IsTrue(items.GetItem("aunt_01").IsInScene);
    }

    [Test]
    public void Return_WithoutTemperature_OnlyAddsAwareness()
    {
        ItemData item = items.GetItem("aunt_01");
        item.hasTemperature = false;

        ResidentData aunt = session.GetResident(ResidentId.Uncle);
        float warmthBefore = aunt.warmth;
        float awarenessBefore = aunt.awareness;

        items.Return("aunt_01", ResidentId.Uncle);

        Assert.AreEqual(warmthBefore, aunt.warmth, 0.001f);
        Assert.AreEqual(awarenessBefore + config.itemTakenWithoutTemperatureAwareness, aunt.awareness, 0.001f);
    }

    [Test]
    public void Return_OnlyOwnerCanTakeBack_AndMotherItemsStay()
    {
        // 大叔的东西不能让老婆婆来收。
        ItemActionResult wrongOwner = items.Return("aunt_01", ResidentId.Grandma);
        Assert.AreEqual(ItemActionStatus.Rejected, wrongOwner.status);

        // 母亲的东西不会有人来收。
        items.TakeOut("mother_01", AreaId.FirstFloor, "r", PlacementSurface.HangingRack);
        ItemActionResult motherItem = items.Return("mother_01", ResidentId.Mother);
        Assert.AreEqual(ItemActionStatus.Rejected, motherItem.status);
    }

    // ---------------- 温度 / 玩家触碰 ----------------

    [Test]
    public void Touch_NormalItem_GivesTemperatureAndConsumesRound()
    {
        ItemData item = items.GetItem("grandma_01");
        Assert.IsFalse(item.hasTemperature);

        bool touched = false;
        System.Action<ItemTouchedEvent> handler = evt =>
        {
            if (evt.itemId == "grandma_01") touched = true;
        };
        EventBus.Subscribe(handler);

        ItemActionResult result = items.Touch("grandma_01");

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(item.hasTemperature);
        Assert.AreEqual(1, session.touchesThisRound);
        Assert.IsTrue(touched);

        EventBus.Unsubscribe(handler);
    }

    [Test]
    public void Touch_SecondTime_SaysAlreadyTouched()
    {
        items.Touch("grandma_01");

        ItemActionResult second = items.Touch("grandma_01");
        Assert.AreEqual(ItemActionStatus.AlreadyTouched, second.status);
        Assert.AreEqual(1, session.touchesThisRound);
    }

    [Test]
    public void Touch_OnlyOncePerRound()
    {
        items.Touch("grandma_01");

        ItemActionResult other = items.Touch("aunt_01");
        Assert.AreEqual(ItemActionStatus.TouchUsedUpThisRound, other.status);
        Assert.IsFalse(items.GetItem("aunt_01").hasTemperature);
        Assert.AreEqual(1, session.touchesThisRound);
    }

    [Test]
    public void Touch_PoolItem_IsRejected()
    {
        ItemActionResult result = items.Touch("grandma_02");
        Assert.AreEqual(ItemActionStatus.NotInScene, result.status);
        Assert.AreEqual(0, session.touchesThisRound);
    }

    [Test]
    public void Touch_PublishesPrompt_OnReject()
    {
        items.Touch("grandma_01");

        string message = null;
        System.Action<ItemPromptEvent> handler = evt => message = evt.message;
        EventBus.Subscribe(handler);

        items.Touch("grandma_01");

        Assert.IsFalse(string.IsNullOrEmpty(message));
        EventBus.Unsubscribe(handler);
    }

    // ---------------- 母亲物品 ----------------

    [Test]
    public void Touch_MotherItem_ConfusesOneOrTwoResidents()
    {
        items.TakeOut("mother_01", AreaId.FirstFloor, "r", PlacementSurface.HangingRack);
        ItemActionResult result = items.Touch("mother_01");

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(items.GetItem("mother_01").hasTemperature);

        int confused = 0;
        for (int i = 0; i < session.residents.Count; i++)
        {
            if (session.residents[i].state == NpcState.Confused) confused++;
        }
        Assert.GreaterOrEqual(confused, config.confusionMinCount);
        Assert.LessOrEqual(confused, config.confusionMaxCount);
    }

    // ---------------- 小女孩移动 ----------------

    [Test]
    public void Move_ByGirl_ClearsTemperature_AndAddsWarmth()
    {
        items.TakeOut("grandma_02", AreaId.FirstFloor, "ground_a", PlacementSurface.Ground);
        ItemData item = items.GetItem("grandma_02");
        item.hasTemperature = true;

        ResidentData girl = session.GetResident(ResidentId.Girl);
        float warmthBefore = girl.warmth;

        ItemActionResult result = items.Move("grandma_02", ResidentId.Girl, AreaId.Backyard, "ground_b", PlacementSurface.Ground);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(item.hasTemperature);
        Assert.AreEqual(warmthBefore + config.girlMoveWarmth, girl.warmth, 0.001f);
        Assert.IsTrue(item.IsInScene);
        Assert.AreEqual(AreaId.Backyard, item.area);
        Assert.AreEqual("ground_b", item.placementId);
    }

    [Test]
    public void Move_OnlyGirlCanDo()
    {
        ItemActionResult result = items.Move("aunt_01", ResidentId.Uncle, AreaId.FirstFloor, "g", PlacementSurface.Ground);
        Assert.AreEqual(ItemActionStatus.Rejected, result.status);
    }

    // ---------------- 强行触碰（文档 4.4） ----------------

    [Test]
    public void ForceTouch_TwoWarnings_ThenAccepted()
    {
        // 区域占用现在会校验"人真的在那儿、真的在门外"，所以先把他摆好
        session.GetResident(ResidentId.Uncle).currentArea = AreaId.FirstFloor;
        session.SetNpcState(ResidentId.Uncle, NpcState.StayOut);
        items.SetAreaBlocked(AreaId.FirstFloor, ResidentId.Uncle);
        ItemData item = items.GetItem("aunt_01");

        ItemActionResult warning1 = items.Touch("aunt_01");
        Assert.AreEqual(ItemActionStatus.ForceTouchWarned, warning1.status);
        Assert.AreEqual(1, session.forceTouchWarningsUsed);
        Assert.IsFalse(item.hasTemperature);
        Assert.AreEqual(0, session.touchesThisRound);

        ItemActionResult warning2 = items.Touch("aunt_01");
        Assert.AreEqual(ItemActionStatus.ForceTouchWarned, warning2.status);
        Assert.AreEqual(2, session.forceTouchWarningsUsed);
        Assert.IsFalse(item.hasTemperature);

        ResidentData aunt = session.GetResident(ResidentId.Uncle);
        float warmthBefore = aunt.warmth;

        ItemActionResult forced = items.Touch("aunt_01");
        Assert.AreEqual(ItemActionStatus.ForceTouchAccepted, forced.status);
        Assert.IsTrue(item.hasTemperature);
        Assert.AreEqual(1, session.touchesThisRound);
        Assert.AreEqual(NpcState.Startled, aunt.state);
        Assert.IsTrue(aunt.wasForceTouched);
        Assert.AreEqual(warmthBefore - config.forceTouchWarmthPenalty, aunt.warmth, 0.001f);
    }

    [Test]
    public void ForceTouch_WarningsResetEachRound()
    {
        ResidentData uncle = session.GetResident(ResidentId.Uncle);

        // 第 1 轮：两次警告都用掉
        uncle.currentArea = AreaId.FirstFloor;
        session.SetNpcState(ResidentId.Uncle, NpcState.StayOut);
        items.SetAreaBlocked(AreaId.FirstFloor, ResidentId.Uncle);

        Assert.AreEqual(ItemActionStatus.ForceTouchWarned, items.Touch("aunt_01").status);
        Assert.AreEqual(ItemActionStatus.ForceTouchWarned, items.Touch("aunt_01").status);
        Assert.AreEqual(2, session.forceTouchWarningsUsed);

        // 新的一轮开始：警告额度要回到 0，节奏重新从「警告 1」算起
        session.BeginRound(true, ResidentId.Uncle);
        Assert.AreEqual(0, session.forceTouchWarningsUsed, "每轮开始都要重置强碰警告次数");

        // BeginRound 会把住户复位，这里按「他还占着一楼」重新摆好
        uncle.currentArea = AreaId.FirstFloor;
        session.SetNpcState(ResidentId.Uncle, NpcState.StayOut);
        items.SetAreaBlocked(AreaId.FirstFloor, ResidentId.Uncle);

        ItemActionResult warning = items.Touch("aunt_01");
        Assert.AreEqual(ItemActionStatus.ForceTouchWarned, warning.status, "新一轮仍然先给警告，不该直接强碰生效");
        Assert.AreEqual(1, session.forceTouchWarningsUsed);
    }

    // ---------------- 区域占用 ----------------

    [Test]
    public void AreaBlock_TracksAndClears()
    {
        Assert.IsFalse(items.IsAreaBlocked(AreaId.Backyard));

        session.GetResident(ResidentId.Girl).currentArea = AreaId.Backyard;
        session.SetNpcState(ResidentId.Girl, NpcState.StayOut);
        items.SetAreaBlocked(AreaId.Backyard, ResidentId.Girl);
        Assert.IsTrue(items.IsAreaBlocked(AreaId.Backyard));

        ResidentId resident;
        Assert.IsTrue(items.TryGetBlockedResident(AreaId.Backyard, out resident));
        Assert.AreEqual(ResidentId.Girl, resident);

        items.ClearAreaBlocked(AreaId.Backyard);
        Assert.IsFalse(items.IsAreaBlocked(AreaId.Backyard));
    }

    [Test]
    public void AreaBlock_IsIgnoredWhenResidentMovedAway()
    {
        session.SetNpcState(ResidentId.Grandma, NpcState.StayOut);
        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        grandma.currentArea = AreaId.FirstFloor;
        items.SetAreaBlocked(AreaId.FirstFloor, ResidentId.Grandma);
        Assert.IsTrue(items.IsAreaBlocked(AreaId.FirstFloor));

        // 她搬到后院：旧区域要立刻恢复可用（这就是"看着没人却被拦"的修法）
        grandma.currentArea = AreaId.Backyard;

        Assert.IsFalse(items.IsAreaBlocked(AreaId.FirstFloor), "人走了，旧区域不该再被占着");
        ResidentId resident;
        Assert.IsFalse(items.TryGetBlockedResident(AreaId.FirstFloor, out resident));
    }

    [Test]
    public void AreaBlock_IsIgnoredWhenResidentInside()
    {
        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        grandma.currentArea = AreaId.FirstFloor;
        grandma.isOut = false;
        session.SetNpcState(ResidentId.Grandma, NpcState.Normal);

        items.SetAreaBlocked(AreaId.FirstFloor, ResidentId.Grandma);

        Assert.IsFalse(items.IsAreaBlocked(AreaId.FirstFloor), "人在门里（看不见）就不算占着区域");
    }

    [Test]
    public void TakeOut_WithoutValidSurface_PublishesPlacementBlockedPrompt()
    {
        ItemPromptEvent captured = default(ItemPromptEvent);
        bool got = false;
        System.Action<ItemPromptEvent> handler = delegate (ItemPromptEvent evt)
        {
            captured = evt;
            got = true;
        };

        EventBus.Subscribe(handler);
        try
        {
            ItemActionResult result = items.TakeOut("grandma_02", AreaId.FirstFloor, null, PlacementSurface.None);

            Assert.AreEqual(ItemActionStatus.PlacementBlocked, result.status);
            Assert.IsTrue(got, "NPC 放不下东西时也要发 ItemPromptEvent，文案才播得出来");
            Assert.AreEqual(ItemActionStatus.PlacementBlocked, captured.status);
            Assert.AreEqual("grandma_02", captured.itemId);
        }
        finally
        {
            EventBus.Unsubscribe(handler);
        }
    }
}
