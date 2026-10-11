using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 住户行为 AI + 状态机的纯逻辑单测（对应文档五、3.2）。
/// 不依赖场景，直接构造 GameConfig + GameSession + ItemSystem + NpcSystem。
/// </summary>
public class NpcSystemTests
{
    private GameConfig config;
    private GameSession session;
    private RandomService random;
    private ItemSystem items;
    private NpcSystem npc;

    [SetUp]
    public void SetUp()
    {
        EventBus.Clear();
        PlacementRegistry.Clear();

        config = ScriptableObject.CreateInstance<GameConfig>();
        config.EnsureDefaults();

        RegisterPlacements();

        session = new GameSession();
        session.Reset(config, 12345);

        random = new RandomService(12345);
        items = new ItemSystem(config, random, session, null);
        items.InitializeFromConfig();

        npc = new NpcSystem(config, random, session, items, null);
    }

    [TearDown]
    public void TearDown()
    {
        EventBus.Clear();
        PlacementRegistry.Clear();
        if (config != null) Object.DestroyImmediate(config);
    }

    private void RegisterPlacements()
    {
        Register("first_rack", AreaId.FirstFloor, PlacementSurface.HangingRack);
        Register("first_ground_a", AreaId.FirstFloor, PlacementSurface.Ground);
        Register("first_ground_b", AreaId.FirstFloor, PlacementSurface.Ground);
        Register("first_on_item", AreaId.FirstFloor, PlacementSurface.OnItem);
        Register("backyard_ground_a", AreaId.Backyard, PlacementSurface.Ground);
        Register("backyard_on_item", AreaId.Backyard, PlacementSurface.OnItem);
    }

    private void Register(string placementId, AreaId area, PlacementSurface surface)
    {
        PlacementRegistry.Register(new PlacementOption
        {
            placementId = placementId,
            area = area,
            surface = surface
        });
    }

    private static NpcActionRecord Record(ResidentId actor, NpcActionType action, int order)
    {
        return new NpcActionRecord { actor = actor, action = action, order = order };
    }

    // ---------------- 参数 ----------------

    [Test]
    public void Config_ActionWeights_MatchDesign()
    {
        Assert.AreEqual(35f, config.wanderWeight);
        Assert.AreEqual(25f, config.talkWeight);
        Assert.AreEqual(20f, config.takeOutWeight);
        Assert.AreEqual(20f, config.takeBackWeight);
        Assert.AreEqual(20f, config.girlMoveWeight);
    }

    [Test]
    public void Config_ActionCounts_MatchDesign()
    {
        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        ResidentData aunt = session.GetResident(ResidentId.Uncle);
        ResidentData girl = session.GetResident(ResidentId.Girl);
        ResidentData mother = session.GetResident(ResidentId.Mother);

        Assert.AreEqual(2, grandma.actionMin);
        Assert.AreEqual(3, grandma.actionMax);
        Assert.AreEqual(2, aunt.actionMin);
        Assert.AreEqual(3, aunt.actionMax);
        Assert.AreEqual(1, girl.actionMin);
        Assert.AreEqual(2, girl.actionMax);
        Assert.IsFalse(mother.canAct);
    }

    [Test]
    public void RollActionCount_RespectsRanges()
    {
        for (int i = 0; i < 300; i++)
        {
            int grandma = npc.RollActionCount(session.GetResident(ResidentId.Grandma));
            Assert.GreaterOrEqual(grandma, 2);
            Assert.LessOrEqual(grandma, 3);

            int girl = npc.RollActionCount(session.GetResident(ResidentId.Girl));
            Assert.GreaterOrEqual(girl, 1);
            Assert.LessOrEqual(girl, 2);
        }

        Assert.AreEqual(0, npc.RollActionCount(session.GetResident(ResidentId.Mother)));
    }

    [Test]
    public void Mother_NeverActs()
    {
        List<ResidentData> actors = npc.GetActors();
        for (int i = 0; i < actors.Count; i++)
        {
            Assert.AreNotEqual(ResidentId.Mother, actors[i].id);
        }

        ResidentData mother = session.GetResident(ResidentId.Mother);
        Assert.IsFalse(npc.IsActionAvailable(NpcActionType.Talk, mother));
        Assert.IsFalse(npc.IsActionAvailable(NpcActionType.TakeOutItem, mother));
        Assert.AreEqual(0, npc.RollActionCount(mother));
    }

    // ---------------- 权重与前提条件 ----------------

    [Test]
    public void AvailableActions_NormalAdult_UsesDesignWeights()
    {
        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        Dictionary<NpcActionType, float> actions = npc.GetAvailableActions(grandma);

        Assert.IsTrue(actions.ContainsKey(NpcActionType.Wander));
        Assert.AreEqual(35f, actions[NpcActionType.Wander]);
        Assert.AreEqual(25f, actions[NpcActionType.Talk]);
        Assert.AreEqual(20f, actions[NpcActionType.TakeOutItem]);
        Assert.AreEqual(20f, actions[NpcActionType.TakeBackItem]);
        Assert.IsFalse(actions.ContainsKey(NpcActionType.MoveItem));
    }

    [Test]
    public void AvailableActions_Girl_OnlyWanderAndMove()
    {
        ResidentData girl = session.GetResident(ResidentId.Girl);
        Dictionary<NpcActionType, float> actions = npc.GetAvailableActions(girl);

        Assert.AreEqual(2, actions.Count);
        Assert.IsTrue(actions.ContainsKey(NpcActionType.Wander));
        Assert.AreEqual(35f, actions[NpcActionType.Wander]);
        Assert.IsTrue(actions.ContainsKey(NpcActionType.MoveItem));
        Assert.AreEqual(20f, actions[NpcActionType.MoveItem]);
        Assert.IsFalse(actions.ContainsKey(NpcActionType.Talk));
        Assert.IsFalse(actions.ContainsKey(NpcActionType.TakeOutItem));
        Assert.IsFalse(actions.ContainsKey(NpcActionType.TakeBackItem));
    }

    [Test]
    public void Girl_NeverPicksTalk()
    {
        ResidentData girl = session.GetResident(ResidentId.Girl);
        for (int i = 0; i < 500; i++)
        {
            NpcActionRecord record = npc.DecideNextAction(girl, 1);
            Assert.AreNotEqual(NpcActionType.Talk, record.action);
            Assert.AreNotEqual(NpcActionType.TakeOutItem, record.action);
            Assert.AreNotEqual(NpcActionType.TakeBackItem, record.action);
        }
    }

    [Test]
    public void TakeOut_UnavailableWhenPoolEmpty()
    {
        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        Assert.IsTrue(npc.IsActionAvailable(NpcActionType.TakeOutItem, grandma));

        items.TakeOut("grandma_02", AreaId.FirstFloor, "first_ground_b", PlacementSurface.Ground);
        items.TakeOut("grandma_03", AreaId.FirstFloor, "first_on_item", PlacementSurface.OnItem);

        Assert.IsFalse(npc.IsActionAvailable(NpcActionType.TakeOutItem, grandma));
    }

    [Test]
    public void TakeBack_UnavailableWithoutOwnItemInScene()
    {
        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        Assert.IsTrue(npc.IsActionAvailable(NpcActionType.TakeBackItem, grandma));

        items.Return("grandma_01", ResidentId.Grandma);

        Assert.IsFalse(npc.IsActionAvailable(NpcActionType.TakeBackItem, grandma));
    }

    // ---------------- 交流 ----------------

    [Test]
    public void TalkTarget_MotherAboutTwentyPercent()
    {
        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        const int total = 4000;
        int mother = 0;

        for (int i = 0; i < total; i++)
        {
            if (npc.PickTalkTarget(grandma) == ResidentId.Mother) mother++;
        }

        float ratio = (float)mother / total;
        Assert.That(ratio, Is.InRange(0.16f, 0.24f));
    }

    [Test]
    public void TalkTarget_NeverSelf()
    {
        for (int i = 0; i < 200; i++)
        {
            Assert.AreNotEqual(ResidentId.Grandma, npc.PickTalkTarget(session.GetResident(ResidentId.Grandma)));
            Assert.AreNotEqual(ResidentId.Uncle, npc.PickTalkTarget(session.GetResident(ResidentId.Uncle)));
        }
    }

    [Test]
    public void Talk_ToOtherAdult_AddsWarmthAndAwareness()
    {
        ResidentData aunt = session.GetResident(ResidentId.Uncle);
        float warmth = aunt.warmth;
        float awareness = aunt.awareness;

        NpcActionRecord record = Record(ResidentId.Grandma, NpcActionType.Talk, 1);
        record.target = ResidentId.Uncle;
        NpcActionResult result = npc.Execute(record);

        Assert.IsTrue(result.executed);
        Assert.AreEqual(warmth + config.talkWarmth, aunt.warmth, 0.001f);
        Assert.AreEqual(awareness + config.talkAwareness, aunt.awareness, 0.001f);
    }

    [Test]
    public void Talk_ToMother_AddsAwarenessOnly()
    {
        ResidentData mother = session.GetResident(ResidentId.Mother);
        Assert.AreEqual(30f, mother.warmth, 0.001f);
        Assert.AreEqual(30f, mother.awareness, 0.001f);

        for (int i = 1; i <= 3; i++)
        {
            NpcActionRecord record = Record(ResidentId.Grandma, NpcActionType.Talk, i);
            record.target = ResidentId.Mother;
            Assert.IsTrue(npc.Execute(record).executed);
        }

        // 3 次交流：30 → 41 → 52 → 63；暖意整局固定 30（开门在第 6 轮结束后才判）。
        Assert.AreEqual(63f, mother.awareness, 0.001f);
        Assert.AreEqual(30f, mother.warmth, 0.001f);
    }

    // ---------------- 物品动作 ----------------

    [Test]
    public void TakeOut_MovesItemFromPoolToScene()
    {
        NpcActionRecord record = Record(ResidentId.Grandma, NpcActionType.TakeOutItem, 1);
        record.itemId = "grandma_02";
        record.area = AreaId.FirstFloor;
        record.placementId = "first_ground_b";
        record.surface = PlacementSurface.Ground;

        Assert.IsTrue(npc.Execute(record).executed);

        ItemData item = items.GetItem("grandma_02");
        Assert.IsTrue(item.IsInScene);
        Assert.IsFalse(item.isInPool);
        Assert.AreEqual("first_ground_b", item.placementId);
        Assert.IsTrue(PlacementRegistry.IsOccupied("first_ground_b"));
    }

    [Test]
    public void TakeBack_ReturnsOwnItem_AndAddsWarmthWhenHot()
    {
        ItemData item = items.GetItem("aunt_01");
        item.hasTemperature = true;

        ResidentData aunt = session.GetResident(ResidentId.Uncle);
        float warmth = aunt.warmth;
        float awareness = aunt.awareness;

        NpcActionRecord record = Record(ResidentId.Uncle, NpcActionType.TakeBackItem, 1);
        record.itemId = "aunt_01";

        Assert.IsTrue(npc.Execute(record).executed);
        Assert.IsFalse(item.isReturned, "异常情况.pdf：收回后回物品池，可以再被拿出来。");
        Assert.IsTrue(item.isInPool);
        Assert.AreEqual(warmth + config.itemTakenWithTemperatureWarmth, aunt.warmth, 0.001f);
        Assert.AreEqual(awareness + config.itemTakenWithTemperatureAwareness, aunt.awareness, 0.001f);
    }

    [Test]
    public void Move_ByGirl_ClearsTemperatureAndAddsWarmth()
    {
        ItemData item = items.GetItem("aunt_01");
        item.hasTemperature = true;

        ResidentData girl = session.GetResident(ResidentId.Girl);
        float warmth = girl.warmth;

        NpcActionRecord record = Record(ResidentId.Girl, NpcActionType.MoveItem, 1);
        record.itemId = "aunt_01";
        record.area = AreaId.FirstFloor;
        record.placementId = "first_on_item";
        record.surface = PlacementSurface.OnItem;

        Assert.IsTrue(npc.Execute(record).executed);
        Assert.IsFalse(item.hasTemperature);
        Assert.AreEqual(warmth + config.girlMoveWarmth, girl.warmth, 0.001f);
        Assert.AreEqual("first_on_item", item.placementId);
    }

    [Test]
    public void ForceTakeOut_WhenOnlyOneUntouchedItemLeft()
    {
        // 场上只剩 grandma_01 没被碰过。
        items.GetItem("aunt_01").hasTemperature = true;
        Assert.AreEqual(1, npc.CountUntouchedItems(AreaId.FirstFloor));

        npc.ForceTakeOutNext = true;
        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        NpcActionRecord record = npc.DecideNextAction(grandma, 1);

        Assert.AreEqual(NpcActionType.TakeOutItem, record.action, "场上快没东西可碰时必须有一个住户拿出物品。");
        Assert.IsFalse(npc.ForceTakeOutNext, "强制标记只触发一次。");
    }

    // ---------------- 状态机 ----------------

    [Test]
    public void RoundStart_AbsentResident_BecomesStayOut_AndBlocksArea()
    {
        npc.BeginRound(true, ResidentId.Grandma);

        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        Assert.AreEqual(NpcState.StayOut, grandma.state);
        Assert.AreNotEqual(AreaId.None, grandma.stayOutArea);
        Assert.IsTrue(items.IsAreaBlocked(grandma.stayOutArea));

        ResidentId blocker;
        Assert.IsTrue(items.TryGetBlockedResident(grandma.stayOutArea, out blocker));
        Assert.AreEqual(ResidentId.Grandma, blocker);
    }

    [Test]
    public void RoundStart_NoAbsent_EveryoneNormalAndNoBlockedArea()
    {
        npc.BeginRound(true, ResidentId.Grandma);
        AreaId blocked = session.GetResident(ResidentId.Grandma).stayOutArea;

        npc.BeginRound(false, ResidentId.Girl);

        Assert.AreEqual(NpcState.Normal, session.GetResident(ResidentId.Grandma).state);
        Assert.IsFalse(items.IsAreaBlocked(blocked));
    }

    [Test]
    public void Confused_ForcesMotherKnock_ThenReturnsToNormal()
    {
        session.SetNpcState(ResidentId.Grandma, NpcState.Confused);
        ResidentData grandma = session.GetResident(ResidentId.Grandma);

        NpcActionRecord record = npc.DecideNextAction(grandma, 1);
        Assert.AreEqual(NpcActionType.Talk, record.action);
        Assert.AreEqual(ResidentId.Mother, record.target);
        Assert.IsTrue(record.motherKnock);

        float awareness = session.GetResident(ResidentId.Mother).awareness;
        Assert.IsTrue(npc.Execute(record).executed);
        Assert.AreEqual(awareness + config.talkMotherAwareness, session.GetResident(ResidentId.Mother).awareness, 0.001f);
        Assert.AreEqual(NpcState.Normal, grandma.state);
    }

    [Test]
    public void Startled_ForcesContagionTalk_ThenReturnsToNormal()
    {
        session.SetNpcState(ResidentId.Grandma, NpcState.Startled);
        session.GetResident(ResidentId.Grandma).wasForceTouched = true;
        ResidentData grandma = session.GetResident(ResidentId.Grandma);

        NpcActionRecord record = npc.DecideNextAction(grandma, 1);
        Assert.AreEqual(NpcActionType.Talk, record.action);
        Assert.IsTrue(record.contagion);

        // 固定成「找另一个大人」——母亲暖意固定 30，不参与传染。
        record.target = ResidentId.Uncle;
        ResidentData target = session.GetResident(record.target);
        float warmth = target.warmth;
        Assert.IsTrue(npc.Execute(record).executed);

        Assert.AreEqual(warmth - config.forceTouchContagionPenalty, target.warmth, 0.001f);
        Assert.AreEqual(NpcState.Normal, grandma.state);
        Assert.IsFalse(grandma.wasForceTouched);
    }

    [Test]
    public void Startled_Girl_DoesNotTalk()
    {
        // 程序分工 5.10：小女孩不交流。被强碰进入惊吓后，也不能凭空多出一次「交流母亲」。
        session.SetNpcState(ResidentId.Girl, NpcState.Startled);
        ResidentData girl = session.GetResident(ResidentId.Girl);
        float motherAwareness = session.GetResident(ResidentId.Mother).awareness;

        NpcActionRecord record = npc.DecideNextAction(girl, 1);
        Assert.AreNotEqual(NpcActionType.Talk, record.action, "小女孩不能被强制交流");

        npc.Execute(record);
        Assert.AreEqual(motherAwareness, session.GetResident(ResidentId.Mother).awareness, 0.001f,
            "小女孩的任何行动都不该改变母亲的知晓度");
        Assert.AreEqual(NpcState.Normal, girl.state, "惊吓状态在行动后清除");
    }

    [Test]
    public void Confused_Girl_DoesNotTalk()
    {
        session.SetNpcState(ResidentId.Girl, NpcState.Confused);
        ResidentData girl = session.GetResident(ResidentId.Girl);

        NpcActionRecord record = npc.DecideNextAction(girl, 1);
        Assert.AreNotEqual(NpcActionType.Talk, record.action, "小女孩不会被强制去敲门");

        npc.Execute(record);
        Assert.AreEqual(NpcState.Normal, girl.state, "疑惑状态在行动后清除");
    }

    [Test]
    public void MotherDisabled_NobodyTalksToHer_AndDoorStaysShut()
    {
        config.motherEnabled = false;
        ResidentData mother = session.GetResident(ResidentId.Mother);
        float awareness = mother.awareness;

        for (int i = 0; i < 300; i++)
        {
            NpcActionRecord record = npc.DecideNextAction(session.GetResident(ResidentId.Grandma), 1);
            if (record.action != NpcActionType.Talk) continue;
            Assert.AreNotEqual(ResidentId.Mother, record.target, "母亲不参与时不该有人去找她");
        }

        Assert.IsFalse(new StatSettlement(config, session).ResolveMotherDoorAtEnd(),
            "母亲不参与时不应该开门");
        Assert.AreEqual(awareness, mother.awareness, 0.001f);
    }

    [Test]
    public void Execution_ReturnsToNormalAfterOuting()
    {
        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        NpcActionRecord record = Record(ResidentId.Grandma, NpcActionType.Wander, 1);

        Assert.IsTrue(npc.Execute(record).executed);
        Assert.AreEqual(NpcState.Normal, grandma.state);
        Assert.AreEqual(1, grandma.actionsTakenThisRound);
    }

    [Test]
    public void StayOut_KeepsAreaBlockedAfterActing()
    {
        npc.BeginRound(true, ResidentId.Grandma);
        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        AreaId blocked = grandma.stayOutArea;

        npc.Execute(Record(ResidentId.Grandma, NpcActionType.Wander, 1));

        Assert.AreEqual(NpcState.StayOut, grandma.state);
        Assert.IsTrue(items.IsAreaBlocked(blocked));
    }

    [Test]
    public void StayOut_MovingToAnotherArea_MovesTheBlockWithHim()
    {
        npc.BeginRound(true, ResidentId.Grandma);
        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        AreaId blocked = grandma.stayOutArea;
        AreaId other = blocked == AreaId.FirstFloor ? AreaId.Backyard : AreaId.FirstFloor;

        npc.MoveResidentToArea(ResidentId.Grandma, other, null);

        Assert.AreEqual(other, grandma.currentArea);
        Assert.AreEqual(other, grandma.stayOutArea, "stayOutArea 要跟着人更新");
        Assert.IsFalse(items.IsAreaBlocked(blocked), "旧区域应该立刻解除占用");
        Assert.IsTrue(items.IsAreaBlocked(other), "新区域才应该被占住");
    }

    // ---------------- 跨区域移动 ----------------

    [Test]
    public void CurrentArea_StartsAtHomeArea()
    {
        Assert.AreEqual(AreaId.FirstFloor, session.GetResident(ResidentId.Grandma).currentArea);
        Assert.AreEqual(AreaId.FirstFloor, session.GetResident(ResidentId.Uncle).currentArea);
        Assert.AreEqual(AreaId.FirstFloor, session.GetResident(ResidentId.Girl).currentArea);
    }

    [Test]
    public void BeginRound_AbsentResident_MovesToStayOutArea()
    {
        npc.BeginRound(true, ResidentId.Grandma);

        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        Assert.AreEqual(grandma.stayOutArea, grandma.currentArea);
    }

    [Test]
    public void BeginRound_NextRound_MovesResidentBackHome()
    {
        npc.BeginRound(true, ResidentId.Grandma);

        // 手动把他挪到后院，模拟他停在外面（stayOutArea 随机挑到一楼时也能稳定测）。
        npc.MoveResidentToArea(ResidentId.Grandma, AreaId.Backyard);
        Assert.AreEqual(AreaId.Backyard, session.GetResident(ResidentId.Grandma).currentArea);

        npc.BeginRound(false, ResidentId.Girl);

        Assert.AreEqual(AreaId.FirstFloor, session.GetResident(ResidentId.Grandma).currentArea, "下一轮应该自行回家。");
    }

    [Test]
    public void TakeOut_MovesActorToPlacementArea_AndPublishesMovedEvent()
    {
        NpcMovedEvent observed = default(NpcMovedEvent);
        bool fired = false;
        System.Action<NpcMovedEvent> handler = evt =>
        {
            if (evt.id == ResidentId.Grandma) { fired = true; observed = evt; }
        };
        EventBus.Subscribe(handler);

        NpcActionRecord record = Record(ResidentId.Grandma, NpcActionType.TakeOutItem, 1);
        record.itemId = "grandma_02";
        record.area = AreaId.Backyard;
        record.placementId = "backyard_ground_a";
        record.surface = PlacementSurface.Ground;

        Assert.AreEqual(AreaId.Backyard, npc.ResolveActionArea(record));
        Assert.IsTrue(npc.Execute(record).executed);

        Assert.IsTrue(fired);
        Assert.AreEqual(AreaId.Backyard, observed.to);
        Assert.AreEqual(AreaId.Backyard, session.GetResident(ResidentId.Grandma).currentArea);

        EventBus.Unsubscribe(handler);
    }

    [Test]
    public void MoveItem_MovesGirlToDestinationArea()
    {
        NpcActionRecord record = Record(ResidentId.Girl, NpcActionType.MoveItem, 1);
        record.itemId = "aunt_01";
        record.area = AreaId.Backyard;
        record.placementId = "backyard_ground_a";
        record.surface = PlacementSurface.Ground;

        Assert.AreEqual(AreaId.Backyard, npc.ResolveActionArea(record));
        Assert.IsTrue(npc.Execute(record).executed);

        Assert.AreEqual(AreaId.Backyard, session.GetResident(ResidentId.Girl).currentArea);
        Assert.AreEqual(AreaId.Backyard, items.GetItem("aunt_01").area);
    }

    [Test]
    public void TakeBack_MovesActorToItemArea()
    {
        // 把大叔的花盆挪到后院，再让他走到后院收回去。
        ItemData item = items.GetItem("aunt_01");
        item.area = AreaId.Backyard;
        item.placementId = "backyard_ground_a";

        NpcActionRecord record = Record(ResidentId.Uncle, NpcActionType.TakeBackItem, 1);
        record.itemId = "aunt_01";

        Assert.AreEqual(AreaId.Backyard, npc.ResolveActionArea(record));
        Assert.IsTrue(npc.Execute(record).executed);

        Assert.AreEqual(AreaId.Backyard, session.GetResident(ResidentId.Uncle).currentArea);
    }

    [Test]
    public void Girl_PlacementStaysInHerArea()
    {
        // 小女孩只在本区域内移动物品（不跨区域）。
        ItemData item = items.GetItem("aunt_01");
        ResidentData girl = session.GetResident(ResidentId.Girl);

        for (int i = 0; i < 100; i++)
        {
            PlacementOption option;
            Assert.IsTrue(npc.TryPickPlacement(item, girl, out option));
            Assert.AreEqual(AreaId.FirstFloor, option.area, "小女孩只在本区域换位置。");
        }
    }

    [Test]
    public void Girl_MoveUnavailable_WhenNoItemsInHerArea()
    {
        ResidentData girl = session.GetResident(ResidentId.Girl);

        items.Return("grandma_01", ResidentId.Grandma);

        ItemData auntItem = items.GetItem("aunt_01");
        auntItem.area = AreaId.Backyard;
        auntItem.placementId = "backyard_ground_a";

        Assert.IsFalse(npc.IsActionAvailable(NpcActionType.MoveItem, girl), "本区域没有物品，就不能移动。");
    }

    [Test]
    public void Resident_IsOutside_OnlyWhenOut()
    {
        ResidentData grandma = session.GetResident(ResidentId.Grandma);

        npc.BeginRound(false, ResidentId.Girl);
        Assert.IsFalse(grandma.IsOutside, "在家 = 在门里面（不可见）。");

        npc.BeginRound(true, ResidentId.Grandma);
        Assert.IsTrue(grandma.IsOutside, "区域持续 = 在外面（可见）。");
    }

    // ---------------- 行动结束后的「回家 / 不回家」判定 ----------------

    [Test]
    public void EndOfRound_ChanceZero_EveryoneGoesHome()
    {
        config.absentRoundChance = 0f;

        npc.BeginRound(true, ResidentId.Grandma); // 先制造一个「区域持续」
        Assert.AreEqual(NpcState.StayOut, session.GetResident(ResidentId.Grandma).state);
        Assert.IsTrue(items.IsAreaBlocked(AreaId.FirstFloor));

        NpcRoundEndResult result = npc.ResolveEndOfRound();

        Assert.IsFalse(result.hasAbsentResident, "0% 应该全员回家。");
        Assert.AreEqual(NpcState.Normal, session.GetResident(ResidentId.Grandma).state);
        Assert.AreEqual(AreaId.FirstFloor, session.GetResident(ResidentId.Grandma).currentArea);
        Assert.IsFalse(items.IsAreaBlocked(AreaId.FirstFloor));
    }

    [Test]
    public void EndOfRound_ChanceOne_SomeoneStaysOutWhereTheyAre()
    {
        config.absentRoundChance = 1f;

        // 大叔行动结束时人在后院。
        npc.MoveResidentToArea(ResidentId.Uncle, AreaId.Backyard);

        NpcRoundEndResult result = npc.ResolveEndOfRound();

        Assert.IsTrue(result.hasAbsentResident);
        Assert.AreNotEqual(ResidentId.Mother, result.absentResident, "母亲不行动，不会被留下。");

        ResidentData absent = session.GetResident(result.absentResident);
        Assert.AreEqual(NpcState.StayOut, absent.state);
        Assert.AreEqual(absent.stayOutArea, absent.currentArea, "不回家的人停在他当时所在的区域。");
        Assert.IsTrue(items.IsAreaBlocked(absent.stayOutArea));
    }

    [Test]
    public void EndOfRound_DecisionCarriesIntoNextRound()
    {
        config.absentRoundChance = 1f;
        NpcRoundEndResult result = npc.ResolveEndOfRound();
        AreaId blocked = session.GetResident(result.absentResident).stayOutArea;

        // 下一轮开始：沿用上一轮定下的「谁不回家」。
        npc.BeginRound(session.hasAbsentResident, session.absentResident);

        Assert.AreEqual(NpcState.StayOut, session.GetResident(result.absentResident).state);
        Assert.IsTrue(items.IsAreaBlocked(blocked));
    }

    // ---------------- 碰「区域持续」的住户本人 ----------------

    [Test]
    public void TouchResident_TwoWarnings_ThenStartled()
    {
        npc.BeginRound(true, ResidentId.Uncle);
        session.playerArea = AreaId.FirstFloor;

        ResidentData uncle = session.GetResident(ResidentId.Uncle);
        Assert.AreEqual(NpcState.StayOut, uncle.state);
        AreaId blocked = uncle.stayOutArea;

        // 前两次只警告，不生效。
        Assert.AreEqual(ItemActionStatus.ForceTouchWarned, items.TouchResident(ResidentId.Uncle).status);
        Assert.AreEqual(ItemActionStatus.ForceTouchWarned, items.TouchResident(ResidentId.Uncle).status);
        Assert.AreEqual(NpcState.StayOut, uncle.state);

        // 第三次生效：惊吓 + 暖意 −8 + 解除区域占用。
        float warmth = uncle.warmth;
        ItemActionResult third = items.TouchResident(ResidentId.Uncle);

        Assert.AreEqual(ItemActionStatus.ForceTouchAccepted, third.status);
        Assert.AreEqual(NpcState.Startled, uncle.state);
        Assert.AreEqual(warmth - config.forceTouchWarmthPenalty, uncle.warmth, 0.001f);
        Assert.IsFalse(items.IsAreaBlocked(blocked), "被强碰之后他不再占着这个区域。");
    }

    [Test]
    public void TouchResident_RejectedWhenNotStayOut()
    {
        npc.BeginRound(false, ResidentId.Girl); // 全员在家
        session.playerArea = AreaId.FirstFloor;

        ItemActionResult result = items.TouchResident(ResidentId.Uncle);

        Assert.AreEqual(ItemActionStatus.Rejected, result.status, "在门里面的住户碰不到。");
    }

    [Test]
    public void TouchResident_RejectedWhenInAnotherArea()
    {
        npc.BeginRound(true, ResidentId.Uncle);
        npc.MoveResidentToArea(ResidentId.Uncle, AreaId.Backyard);
        session.playerArea = AreaId.FirstFloor;

        ItemActionResult result = items.TouchResident(ResidentId.Uncle);

        Assert.AreEqual(ItemActionStatus.Rejected, result.status, "不在当前场景的住户碰不到。");
    }
}
