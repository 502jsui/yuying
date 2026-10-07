using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一次行动的执行结果。
/// </summary>
public struct NpcActionResult
{
    public bool executed;
    public NpcActionRecord record;
    public string message;
}

/// <summary>一轮结束时「回家 / 不回家」的判定结果。</summary>
public struct NpcRoundEndResult
{
    public bool hasAbsentResident;
    public ResidentId absentResident;
}

/// <summary>
/// 住户行为 AI + 状态机（文档五、住户行为 / 3.2 NPC 状态）。
///
/// 行动权重：闲逛 35 / 交流 25 / 拿出 20 / 拿回 20。
/// 行动次数：老婆婆、大叔 2~3；小女孩 1~2；母亲不动。
/// 交流对象：20% 找母亲，80% 找另一位大人（母亲只能被动交流，小女孩不交流）。
/// 小女孩：不交流、没有自己的东西，只会闲逛或移动别人的东西。
///
/// 这个类不挂场景，只操作 GameSession / ItemSystem，方便直接写单测。
/// 演出节奏由 NpcPhaseDriver 负责。
/// </summary>
public class NpcSystem
{
    private readonly GameConfig config;
    private readonly RandomService random;
    private readonly GameSession session;
    private readonly ItemSystem items;
    private readonly GameFlow flow;
    private readonly StatSettlement settlement;

    public NpcSystem(GameConfig config, RandomService random, GameSession session, ItemSystem items, GameFlow flow, StatSettlement settlement = null)
    {
        this.config = config;
        this.random = random;
        this.session = session;
        this.items = items;
        this.flow = flow;
        this.settlement = settlement != null ? settlement : new StatSettlement(config, session);
    }

    public GameSession Session { get { return session; } }

    /// <summary>异常情况.pdf：场上没被碰过的物品 ≤1 件时，这一轮强制有一个住户拿出物品。</summary>
    public bool ForceTakeOutNext { get; set; }

    /// <summary>该区域场上有几件还没被碰过的物品。</summary>
    public int CountUntouchedItems(AreaId area)
    {
        if (items == null) return 0;

        List<ItemData> inScene = items.GetSceneItems(area);
        int count = 0;
        for (int i = 0; i < inScene.Count; i++)
        {
            if (!inScene[i].hasTemperature) count++;
        }
        return count;
    }

    // ------------------------------------------------------------------
    // ① 场景状态：决定谁不回家、哪个区域不可用
    // ------------------------------------------------------------------

    /// <summary>
    /// 把「谁不回家」的决定落到状态和区域占用上：
    /// 不回家的进入「区域持续」并停在他当时所在的区域（该区域本回合不可用）；
    /// 其他人回「正常」并回家。每轮开始和行动结束都会调。
    /// 疑惑 / 惊吓不跨轮保留。
    /// </summary>
    public void BeginRound(bool hasAbsentResident, ResidentId absentResident)
    {
        if (items != null) items.ClearAllBlockedAreas();

        for (int i = 0; i < session.residents.Count; i++)
        {
            ResidentData resident = session.residents[i];
            if (resident == null) continue;

            resident.wasForceTouched = false;
            resident.actionsTakenThisRound = 0;
            resident.isOut = false;
            resident.isHome = true;

            bool absent = hasAbsentResident && resident.canAct && resident.id == absentResident;
            if (absent)
            {
                // 停在他当时所在的区域；没有就退回随机区域。
                AreaId area = resident.stayOutArea != AreaId.None
                    ? resident.stayOutArea
                    : (resident.currentArea != AreaId.None ? resident.currentArea : PickStayOutArea());

                resident.stayOutArea = area;
                resident.currentArea = area;
                resident.isHome = false;
                resident.isOut = true;
                session.SetNpcState(resident.id, NpcStateRules.OnRoundStart(true));
                if (items != null && area != AreaId.None) items.SetAreaBlocked(area, resident.id);
            }
            else
            {
                resident.stayOutArea = AreaId.None;
                session.SetNpcState(resident.id, NpcStateRules.OnRoundStart(false));
                // 下一轮自行回家。
                MoveResidentToArea(resident.id, resident.homeArea);
            }

            // 位置 / 是否在外变了，通知本场景的 View 刷新（在门里 ←→ 在门外）。
            EventBus.Publish(new NpcMovedEvent
            {
                id = resident.id,
                from = resident.currentArea,
                to = resident.currentArea,
                placementId = null
            });
        }
    }

    /// <summary>
    /// 住户行动**结束之后**调用：判定回家还是不回家。
    /// 顺序：① 先判有没有人不回家（80% / 20%）② 再判是谁；
    /// 不回家的进入「区域持续」，停在他当时所在的区域。
    /// </summary>
    public NpcRoundEndResult ResolveEndOfRound()
    {
        ResidentId absent = default(ResidentId);
        bool hasAbsent = TurnRules.TryPickAbsentResident(random, config, session.residents, out absent);

        // 先把「停在哪」定下来：就在他这一刻所在的区域。
        if (hasAbsent)
        {
            ResidentData resident = session.GetResident(absent);
            if (resident != null)
            {
                resident.stayOutArea = resident.currentArea != AreaId.None
                    ? resident.currentArea
                    : PickStayOutArea();
            }
        }

        session.SetAbsentDecision(hasAbsent, absent);
        BeginRound(hasAbsent, absent);

        return new NpcRoundEndResult
        {
            hasAbsentResident = hasAbsent,
            absentResident = absent
        };
    }

    /// <summary>
    /// 把住户挪到某个区域（跨场景 / 传送）。会广播 NpcMovedEvent，
    /// 场景里那份对应的 ResidentView 会自己显示 / 隐藏。
    /// </summary>
    public void MoveResidentToArea(ResidentId id, AreaId area, string placementId = null)
    {
        ResidentData resident = session.GetResident(id);
        if (resident == null || area == AreaId.None) return;
        if (resident.currentArea == area) return;

        AreaId from = resident.currentArea;
        resident.currentArea = area;
        EventBus.Publish(new NpcMovedEvent
        {
            id = id,
            from = from,
            to = area,
            placementId = placementId
        });
    }

    /// <summary>这条行动发生在哪个区域（在执行之前解析，拿回东西要先读到物品当时的位置）。</summary>
    public AreaId ResolveActionArea(NpcActionRecord record)
    {
        switch (record.action)
        {
            case NpcActionType.TakeOutItem:
            case NpcActionType.MoveItem:
                return record.area;

            case NpcActionType.TakeBackItem:
                ItemData item = session.GetItem(record.itemId);
                return item != null ? item.area : AreaId.None;

            default:
                return AreaId.None;
        }
    }

    private AreaId PickStayOutArea()
    {
        if (config != null && config.stayOutAreas != null && config.stayOutAreas.Count > 0)
        {
            return random.Pick(config.stayOutAreas);
        }
        return AreaId.FirstFloor;
    }

    // ------------------------------------------------------------------
    // ② 行动：决定 + 执行
    // ------------------------------------------------------------------

    /// <summary>本轮这个住户要做几个行动。</summary>
    public int RollActionCount(ResidentData resident)
    {
        if (resident == null || !resident.canAct) return 0;
        return random.RangeInclusive(resident.actionMin, resident.actionMax);
    }

    /// <summary>本轮能行动的住户（母亲不动）。</summary>
    public List<ResidentData> GetActors()
    {
        List<ResidentData> actors = new List<ResidentData>();
        for (int i = 0; i < session.residents.Count; i++)
        {
            ResidentData resident = session.residents[i];
            if (resident != null && resident.canAct) actors.Add(resident);
        }
        return actors;
    }

    /// <summary>
    /// 决定该住户的下一个行动。会按当前状态处理「疑惑 / 惊吓」的强制行动，
    /// 否则按权重从满足前提条件的行动里随机挑一个。
    /// </summary>
    public NpcActionRecord DecideNextAction(ResidentData resident, int order)
    {
        if (resident == null || !resident.canAct) return NpcActionRecord.None(ResidentId.Mother);

        // 场上快没东西可碰了 → 这一轮第一个行动的住户直接拿出东西。
        if (ForceTakeOutNext)
        {
            ForceTakeOutNext = false;
            if (IsActionAvailable(NpcActionType.TakeOutItem, resident))
            {
                return BuildRecord(resident, NpcActionType.TakeOutItem, order);
            }
        }

        // 疑惑：下次行动必去敲母亲的门
        if (resident.state == NpcState.Confused)
        {
            return new NpcActionRecord
            {
                actor = resident.id,
                action = NpcActionType.Talk,
                target = ResidentId.Mother,
                motherKnock = true,
                order = order
            };
        }

        // 惊吓：下次行动必为交流，且让对方暖意减少
        if (resident.state == NpcState.Startled)
        {
            return new NpcActionRecord
            {
                actor = resident.id,
                action = NpcActionType.Talk,
                target = PickTalkTarget(resident),
                contagion = true,
                order = order
            };
        }

        List<NpcActionType> candidates = new List<NpcActionType>();
        List<float> weights = new List<float>();
        foreach (KeyValuePair<NpcActionType, float> pair in GetAvailableActions(resident))
        {
            candidates.Add(pair.Key);
            weights.Add(pair.Value);
        }

        if (candidates.Count == 0) return NpcActionRecord.None(resident.id);

        NpcActionType picked = random.PickWeighted(candidates, weights);
        return BuildRecord(resident, picked, order);
    }

    /// <summary>
    /// 当前满足前提条件的行动 → 权重。闲逛 35 / 交流 25 / 拿出 20 / 拿回 20；
    /// 小女孩的「移动东西」用 girlMoveWeight（默认 20），她没有交流、拿出、拿回。
    /// </summary>
    public Dictionary<NpcActionType, float> GetAvailableActions(ResidentData resident)
    {
        Dictionary<NpcActionType, float> result = new Dictionary<NpcActionType, float>();
        AddAction(result, NpcActionType.Wander, config.wanderWeight, resident);
        AddAction(result, NpcActionType.Talk, config.talkWeight, resident);
        AddAction(result, NpcActionType.TakeOutItem, config.takeOutWeight, resident);
        AddAction(result, NpcActionType.TakeBackItem, config.takeBackWeight, resident);
        AddAction(result, NpcActionType.MoveItem, config.girlMoveWeight, resident);
        return result;
    }

    private void AddAction(Dictionary<NpcActionType, float> result, NpcActionType action, float weight, ResidentData resident)
    {
        if (weight <= 0f) return;
        if (!IsActionAvailable(action, resident)) return;
        result[action] = weight;
    }

    /// <summary>各行动的前提条件。</summary>
    public bool IsActionAvailable(NpcActionType action, ResidentData resident)
    {
        if (resident == null || !resident.canAct) return false;

        switch (action)
        {
            case NpcActionType.Wander:
                return true;

            case NpcActionType.Talk:
                return CanTalk(resident);

            case NpcActionType.TakeOutItem:
                return HasPlacementForPool(resident);

            case NpcActionType.TakeBackItem:
                return FindOwnSceneItem(resident) != null;

            case NpcActionType.MoveItem:
                return CanMoveItem(resident);

            default:
                return false;
        }
    }

    /// <summary>只有老婆婆、大叔能主动交流；小女孩不交流，母亲不动。</summary>
    public bool CanTalk(ResidentData resident)
    {
        if (resident == null || !resident.canAct) return false;
        if (resident.id == ResidentId.Mother || resident.id == ResidentId.Girl) return false;
        return PickTalkTarget(resident) != ResidentId.Mother || session.GetResident(ResidentId.Mother) != null;
    }

    private NpcActionRecord BuildRecord(ResidentData resident, NpcActionType action, int order)
    {
        NpcActionRecord record = new NpcActionRecord();
        record.actor = resident.id;
        record.action = action;
        record.order = order;

        switch (action)
        {
            case NpcActionType.Talk:
                record.target = PickTalkTarget(resident);
                break;

            case NpcActionType.TakeOutItem:
                ResolveTakeOut(resident, ref record);
                break;

            case NpcActionType.TakeBackItem:
                ResolveTakeBack(resident, ref record);
                break;

            case NpcActionType.MoveItem:
                ResolveMove(resident, ref record);
                break;
        }

        return record;
    }

    private void ResolveTakeOut(ResidentData resident, ref NpcActionRecord record)
    {
        ItemData item = FindNextPoolItem(resident);
        if (item == null) { record.action = NpcActionType.Wander; return; }

        PlacementOption placement;
        if (!TryPickPlacement(item, resident, out placement)) { record.action = NpcActionType.Wander; return; }

        record.itemId = item.itemId;
        record.area = placement.area;
        record.placementId = placement.placementId;
        record.surface = placement.surface;
    }

    private void ResolveTakeBack(ResidentData resident, ref NpcActionRecord record)
    {
        ItemData item = FindOwnSceneItem(resident);
        if (item == null) { record.action = NpcActionType.Wander; return; }
        record.itemId = item.itemId;
    }

    private void ResolveMove(ResidentData resident, ref NpcActionRecord record)
    {
        ItemData item = FindMoveTarget(resident);
        if (item == null) { record.action = NpcActionType.Wander; return; }

        PlacementOption placement;
        if (!TryPickPlacement(item, resident, out placement)) { record.action = NpcActionType.Wander; return; }

        record.itemId = item.itemId;
        record.area = placement.area;
        record.placementId = placement.placementId;
        record.surface = placement.surface;
    }

    /// <summary>执行一条行动，返回是否真的生效。</summary>
    public NpcActionResult Execute(NpcActionRecord record)
    {
        ResidentData actor = session.GetResident(record.actor);
        if (actor == null)
        {
            return new NpcActionResult { executed = false, record = record, message = "找不到住户。" };
        }

        NpcState before = actor.state;
        bool transientOuting = before == NpcState.Normal || before == NpcState.GoingOut;
        if (before == NpcState.Normal) session.SetNpcState(actor.id, NpcState.GoingOut);

        AreaId actionArea = ResolveActionArea(record);
        bool executed = false;
        string message = "";

        switch (record.action)
        {
            case NpcActionType.Wander:
                executed = true;
                message = "闲逛。";
                break;

            case NpcActionType.Talk:
                executed = ExecuteTalk(actor, record);
                message = executed ? "交流。" : "交流失败。";
                break;

            case NpcActionType.TakeOutItem:
                executed = ExecuteTakeOut(record);
                message = executed ? "拿出东西。" : "拿出东西失败。";
                break;

            case NpcActionType.TakeBackItem:
                executed = ExecuteTakeBack(actor, record);
                message = executed ? "拿回东西。" : "拿回东西失败。";
                break;

            case NpcActionType.MoveItem:
                executed = ExecuteMove(record);
                message = executed ? "移动东西。" : "移动东西失败。";
                break;
        }

        // 状态收尾：出门行动回家；疑惑 / 惊吓被这次行动消耗掉；区域持续保持到下一轮。
        NpcState after;
        if (before == NpcState.StayOut) after = NpcState.StayOut;
        else if (NpcStateRules.IsForcedAction(before)) after = NpcStateRules.AfterForcedAction(before);
        else if (transientOuting) after = NpcStateRules.OnReturnHome(NpcState.GoingOut);
        else after = before;
        session.SetNpcState(actor.id, after);

        if (after == NpcState.Normal)
        {
            actor.wasForceTouched = false;
            actor.isOut = false;
            actor.isHome = true;
            actor.stayOutArea = AreaId.None;
        }

        // 做完这件事之后他人在哪个区域（跨场景移动）。
        if (executed) MoveResidentToArea(actor.id, actionArea, record.placementId);

        actor.actionsTakenThisRound++;
        return new NpcActionResult { executed = executed, record = record, message = message };
    }

    // ------------------------------------------------------------------
    // 各行动的效果
    // ------------------------------------------------------------------

    private bool ExecuteTalk(ResidentData actor, NpcActionRecord record)
    {
        ResidentData target = session.GetResident(record.target);
        if (target == null) return false;

        // 数值结算统一走 StatSettlement：
        // 普通 +4/+8、母亲 +0/+11、强碰传染 −5（知晓照常）。
        // 被强碰者自己这次不加暖意 / 知晓（发起者本来就不加）。
        settlement.ApplyTalk(record.target, record.contagion);
        return true;
    }

    /// <summary>母亲知晓度 &gt; 60 → 暖意跳到 61、知晓度 63（文档七）。</summary>
    public bool ApplyMotherDoor()
    {
        return settlement.ResolveMotherDoorAtEnd();
    }

    private bool ExecuteTakeOut(NpcActionRecord record)
    {
        if (items == null) return false;
        ItemActionResult result = items.TakeOut(record.itemId, record.area, record.placementId, record.surface);
        return result.ChangedState;
    }

    private bool ExecuteTakeBack(ResidentData actor, NpcActionRecord record)
    {
        if (items == null) return false;
        ItemActionResult result = items.Return(record.itemId, actor.id);
        return result.IsSuccess;
    }

    private bool ExecuteMove(NpcActionRecord record)
    {
        if (items == null) return false;
        ItemActionResult result = items.Move(record.itemId, ResidentId.Girl, record.area, record.placementId, record.surface);
        return result.IsSuccess;
    }

    // ------------------------------------------------------------------
    // 挑选目标
    // ------------------------------------------------------------------

    /// <summary>交流对象：20% 找母亲，80% 找另一位大人。</summary>
    public ResidentId PickTalkTarget(ResidentData actor)
    {
        if (actor == null) return ResidentId.Mother;
        if (random.Chance(config.talkMotherChance)) return ResidentId.Mother;

        if (actor.id == ResidentId.Grandma) return ResidentId.Uncle;
        if (actor.id == ResidentId.Uncle) return ResidentId.Grandma;
        return ResidentId.Mother;
    }

    private ItemData FindNextPoolItem(ResidentData resident)
    {
        if (items == null) return null;
        return items.FindNextPoolItem(resident.id.ToItemOwner());
    }

    private ItemData FindOwnSceneItem(ResidentData resident)
    {
        ItemOwner owner = resident.id.ToItemOwner();
        List<ItemData> candidates = new List<ItemData>();
        for (int i = 0; i < session.items.Count; i++)
        {
            ItemData item = session.items[i];
            if (item.owner == owner && item.IsInScene && !item.isReturned) candidates.Add(item);
        }

        // 「不一定是有温度的那件」：随机挑一件自己的东西来收。
        if (candidates.Count == 0) return null;
        return random.Pick(candidates);
    }

    /// <summary>小女孩只移动**本区域**里的物品（不跨区域）。</summary>
    private ItemData FindMoveTarget(ResidentData resident)
    {
        List<ItemData> candidates = new List<ItemData>();
        for (int i = 0; i < session.items.Count; i++)
        {
            ItemData item = session.items[i];
            if (!item.IsInScene || item.isReturned) continue;
            if (resident != null && item.area != resident.currentArea) continue;
            candidates.Add(item);
        }
        if (candidates.Count == 0) return null;
        return random.Pick(candidates);
    }

    private bool CanMoveItem(ResidentData resident)
    {
        if (resident == null || resident.id != ResidentId.Girl) return false;
        ItemData item = FindMoveTarget(resident);
        if (item == null) return false;

        PlacementOption placement;
        return TryPickPlacement(item, resident, out placement);
    }

    private bool HasPlacementForPool(ResidentData resident)
    {
        if (items == null) return false;
        ItemOwner owner = resident.id.ToItemOwner();
        List<ItemData> pool = new List<ItemData>();
        for (int i = 0; i < session.items.Count; i++)
        {
            ItemData item = session.items[i];
            if (item.owner == owner && item.isInPool && !item.isReturned) pool.Add(item);
        }
        if (pool.Count == 0) return false;

        for (int i = 0; i < pool.Count; i++)
        {
            PlacementOption placement;
            if (TryPickPlacement(pool[i], resident, out placement)) return true;
        }
        return false;
    }

    /// <summary>
    /// 挑放置点：优先主人所在区域 + 空闲的点；其次任意区域空闲点；
    /// 最后允许叠放（花盆可以放在别的物品上）。
    /// </summary>
    public bool TryPickPlacement(ItemData item, ResidentData actor, out PlacementOption chosen)
    {
        chosen = default(PlacementOption);
        if (item == null) return false;

        List<PlacementOption> all = PlacementRegistry.GetAll();
        if (all.Count == 0) return false;

        // 小女孩「不定向移动」：不限区域，随机挑一个能放的点；其他住户优先自己家。
        // 优先放在他/她**此刻所在的区域**（小女孩只在本区域内移动物品）。
        AreaId preferred = actor != null && actor.currentArea != AreaId.None
            ? actor.currentArea
            : AreaId.None;
        PlacementOption fallback = default(PlacementOption);
        bool hasFallback = false;

        for (int pass = 0; pass < 3; pass++)
        {
            List<PlacementOption> matches = new List<PlacementOption>();
            for (int i = 0; i < all.Count; i++)
            {
                PlacementOption option = all[i];
                if (!option.Accepts(item)) continue;

                bool inPreferred = preferred == AreaId.None || option.area == preferred;
                bool free = !PlacementRegistry.IsOccupied(option.placementId);

                if (pass == 0 && inPreferred && free) matches.Add(option);
                else if (pass == 1 && free) matches.Add(option);
                else if (pass == 2) { fallback = option; hasFallback = true; }
            }

            if (matches.Count > 0)
            {
                chosen = random.Pick(matches);
                return true;
            }
        }

        if (hasFallback)
        {
            chosen = fallback;
            return true;
        }
        return false;
    }
}
