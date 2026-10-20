using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 物品系统。负责文档里「物品」相关的全部纯逻辑：
///
///   1. 物品池：拿出 / 拿回 / 收回后不再出现（文档「物品的流转」）
///   2. 放置规则：衣服只能挂、花盆可落地可叠、药包等只能叠、木箱只能落地（文档 2.6）
///   3. 温度：被玩家碰过 → 有温度；二次触碰不允许；小女孩移动会清零
///   4. 玩家触碰：普通物品 / 母亲物品 / 不可用区域的强行触碰（文档 4.4）
///   5. 事件：碰、被拿走、被移动都通过 EventBus 广播给数值 / 表现 / UI
///
/// 这个类不挂场景、不认 Prefab，只碰 GameSession 里的数据，所以可以直接写单测。
/// 表现层通过 ItemView 订阅事件刷新；场景放置点由 PlacementPoint 提供。
///
/// 谁在用：GameRoot 持有；GameFlow 在场景状态阶段调 EnsureSceneItems；
/// NpcSystem 调 TakeOut / Return / Move；GameManager + PlayerController 调 Touch / TouchResident。
/// </summary>
public class ItemSystem
{
    private readonly GameConfig config;
    private readonly RandomService random;
    private readonly GameSession session;
    private readonly GameFlow flow;
    private readonly StatSettlement settlement;

    /// <summary>本回合不可用的区域 → 占着这个区域的住户。</summary>
    private readonly Dictionary<AreaId, ResidentId> blockedAreas = new Dictionary<AreaId, ResidentId>();

    public ItemSystem(GameConfig config, RandomService random, GameSession session, GameFlow flow, StatSettlement settlement = null)
    {
        this.config = config;
        this.random = random;
        this.session = session;
        this.flow = flow;
        this.settlement = settlement != null ? settlement : new StatSettlement(config, session);
    }

    public GameSession Session { get { return session; } }

    // ------------------------------------------------------------------
    // 初始化
    // ------------------------------------------------------------------

    /// <summary>
    /// 按 GameConfig.items 建立本局的物品数据。要在 GameSession.Reset 之后调用。
    /// 会清空上一局的温度 / 收回状态，并清掉所有区域占用。
    /// </summary>
    public void InitializeFromConfig()
    {
        if (session == null) return;
        blockedAreas.Clear();
        PlacementRegistry.ClearOccupancy();

        if (config == null) return;
        config.EnsureDefaults();

        List<ItemConfig> definitions = config.items;
        if (definitions == null) return;

        for (int i = 0; i < definitions.Count; i++)
        {
            ItemConfig definition = definitions[i];
            if (definition == null || string.IsNullOrEmpty(definition.itemId)) continue;

            ItemData item = session.EnsureItem(definition.itemId, definition.owner, definition.type);
            item.owner = definition.owner;
            if (definition.type != ItemType.Unknown) item.type = definition.type;
            item.hasTemperature = false;
            item.isReturned = false;

            if (definition.startsInScene)
            {
                item.isInPool = false;
                item.position = ItemPosition.Scene;
                item.area = definition.startArea;
                item.placementId = definition.startPlacementId;
                PlacementRegistry.MarkOccupied(definition.startPlacementId);
            }
            else
            {
                item.isInPool = true;
                item.position = ItemPosition.OwnerHand;
                item.area = AreaId.None;
                item.placementId = null;
            }
        }
    }

    // ------------------------------------------------------------------
    // 查询
    // ------------------------------------------------------------------

    public bool HasItemInPool(ItemOwner owner)
    {
        return session != null && session.HasItemInPool(owner);
    }

    /// <summary>物品池里下一个还没放出来的物品；没有就返回 null。</summary>
    public ItemData FindNextPoolItem(ItemOwner owner)
    {
        if (session == null) return null;
        for (int i = 0; i < session.items.Count; i++)
        {
            ItemData item = session.items[i];
            if (item.owner == owner && item.isInPool && !item.isReturned) return item;
        }
        return null;
    }

    public ItemData GetItem(string itemId)
    {
        return session != null ? session.GetItem(itemId) : null;
    }

    public List<ItemData> GetSceneItems(AreaId area)
    {
        return session != null ? session.GetItemsInArea(area) : new List<ItemData>();
    }

    // ------------------------------------------------------------------
    // 物品池流转（NPC 行动用）
    // ------------------------------------------------------------------

    /// <summary>
    /// 把指定的物品从池里拿出来放到场景。placementSurface 不传成 None 就会按 2.6 校验；
    /// 还没有放置点时传 PlacementSurface.Any 先放出来。
    /// </summary>
    public ItemActionResult TakeOut(string itemId, AreaId area, string placementId, PlacementSurface placementSurface)
    {
        ItemData item = GetItem(itemId);
        if (item == null)
            return ItemActionResult.Fail(itemId, ItemActionStatus.ItemNotFound, "找不到这件物品。");
        if (item.isReturned)
            return ItemActionResult.Fail(itemId, ItemActionStatus.AlreadyReturned, "这件物品已经被拿回去了。");
        if (!item.isInPool)
            return ItemActionResult.Fail(itemId, ItemActionStatus.Rejected, "这件物品已经拿出来了。");

        if (!ItemPlacementRules.CanPlace(item.type, placementSurface))
        {
            return FailPlacement(itemId, item.type);
        }

        PlaceInScene(item, area, placementId);
        EventBus.Publish(new ItemAppearedEvent
        {
            itemId = item.itemId,
            area = area,
            placementId = placementId
        });
        return ItemActionResult.Ok(itemId, ItemActionStatus.Success, "物品已经放出来了。");
    }

    /// <summary>
    /// 母亲开门：**一次性收走她所有东西**（文档 5.3）。收走后不会再出现。
    /// </summary>
    public int CollectMotherItems()
    {
        if (session == null) return 0;

        int collected = 0;
        for (int i = 0; i < session.items.Count; i++)
        {
            ItemData item = session.items[i];
            if (item.owner != ItemOwner.Mother || item.isReturned) continue;

            string placementId = item.placementId;
            item.hasTemperature = false;
            item.position = ItemPosition.OwnerHand;
            item.area = AreaId.None;
            item.placementId = null;
            item.isInPool = false;
            item.isReturned = true;
            PlacementRegistry.Release(placementId);

            EventBus.Publish(new ItemTakenEvent
            {
                itemId = item.itemId,
                by = ResidentId.Mother,
                hadTemperature = false
            });
            collected++;
        }

        return collected;
    }

    /// <summary>
    /// 保证某个区域场上有 targetCount 件物品：不够就从物品池**随机**补几件，
    /// 放到该区域随机的空闲放置点上（母亲物品本次不参与）。
    /// </summary>
    public int EnsureSceneItems(AreaId area, int targetCount)
    {
        if (session == null || area == AreaId.None || targetCount <= 0) return 0;
        if (area == AreaId.SecondFloor) return 0; // 二楼只放母亲物品，不做普通物品随机补货

        int current = GetSceneItems(area).Count;
        int placed = 0;

        while (current < targetCount)
        {
            ItemData item = PickRandomPoolItem();
            if (item == null) break;

            PlacementOption placement;
            if (!TryPickRandomPlacement(item, area, out placement)) break;

            ItemActionResult result = TakeOut(item.itemId, placement.area, placement.placementId, placement.surface);
            if (!result.ChangedState) break;

            current++;
            placed++;
        }

        return placed;
    }

    private ItemData PickRandomPoolItem()
    {
        List<ItemData> candidates = new List<ItemData>();
        for (int i = 0; i < session.items.Count; i++)
        {
            ItemData item = session.items[i];
            if (!item.isInPool || item.isReturned) continue;
            if (item.owner == ItemOwner.Mother) continue; // 母亲机制本次不做
            candidates.Add(item);
        }

        if (candidates.Count == 0) return null;
        return random.Pick(candidates);
    }

    private bool TryPickRandomPlacement(ItemData item, AreaId area, out PlacementOption chosen)
    {
        chosen = default(PlacementOption);

        List<PlacementOption> all = PlacementRegistry.GetInArea(area);
        List<PlacementOption> matches = new List<PlacementOption>();
        for (int i = 0; i < all.Count; i++)
        {
            PlacementOption option = all[i];
            if (!option.Accepts(item)) continue;
            if (PlacementRegistry.IsOccupied(option.placementId)) continue;
            matches.Add(option);
        }

        if (matches.Count == 0) return false;
        chosen = random.Pick(matches);
        return true;
    }

    /// <summary>
    /// 住户「拿回东西」。有温度 → 暖意 +18 / 知晓 +15；没温度 → 只加知晓 +15。
    /// 拿回后物品回到物品池，之后仍可被再次拿出。
    /// </summary>
    public ItemActionResult Return(string itemId, ResidentId by)
    {
        ItemData item = GetItem(itemId);
        if (item == null)
            return ItemActionResult.Fail(itemId, ItemActionStatus.ItemNotFound, "找不到这件物品。");
        if (item.isReturned)
            return ItemActionResult.Fail(itemId, ItemActionStatus.AlreadyReturned, "这件物品已经被拿回去了。");
        if (!item.IsInScene)
            return ItemActionResult.Fail(itemId, ItemActionStatus.NotInScene, "这件物品现在不在场景里。");
        if (item.owner == ItemOwner.Mother || item.owner == ItemOwner.None)
            return ItemActionResult.Fail(itemId, ItemActionStatus.Rejected, "这件东西不会有人来收。");
        if (item.owner.ToResidentId() != by)
            return ItemActionResult.Fail(itemId, ItemActionStatus.Rejected, "这不是" + by.ToDisplayName() + "的东西。");

        string returnedFrom = item.placementId;
        bool hadTemperature = item.hasTemperature;
        item.hasTemperature = false;
        item.position = ItemPosition.OwnerHand;
        item.area = AreaId.None;
        item.placementId = null;
        // 异常情况.pdf：取消「物品不会被第二次拿出」的限制 —— 收回后回到物品池，可以再被拿出来。
        item.isInPool = true;
        item.isReturned = false;
        PlacementRegistry.Release(returnedFrom);

        // 数值结算统一走 StatSettlement。
        settlement.ApplyItemTaken(by, hadTemperature);

        EventBus.Publish(new ItemTakenEvent
        {
            itemId = item.itemId,
            by = by,
            hadTemperature = hadTemperature
        });

        return ItemActionResult.Ok(itemId, ItemActionStatus.Success,
            hadTemperature ? "他拿回了还带着温度的东西。" : "他拿回了东西，但没感觉到温度。");
    }

    /// <summary>
    /// 小女孩「移动东西」：暖意 +18，并把物品的温度清零。物品留在场景里，只是换位置。
    /// </summary>
    public ItemActionResult Move(string itemId, ResidentId by, AreaId area, string placementId, PlacementSurface placementSurface)
    {
        ItemData item = GetItem(itemId);
        if (item == null)
            return ItemActionResult.Fail(itemId, ItemActionStatus.ItemNotFound, "找不到这件物品。");
        if (!item.IsInScene || item.isReturned)
            return ItemActionResult.Fail(itemId, ItemActionStatus.NotInScene, "这件物品现在不在场景里。");
        if (by != ResidentId.Girl)
            return ItemActionResult.Fail(itemId, ItemActionStatus.Rejected, "只有小女孩会去移动别人的东西。");
        if (!ItemPlacementRules.CanPlace(item.type, placementSurface))
        {
            return FailPlacement(itemId, item.type);
        }

        string previousPlacement = item.placementId;
        bool hadTemperature = item.hasTemperature;
        item.area = area;
        item.placementId = placementId;
        PlacementRegistry.Release(previousPlacement);
        PlacementRegistry.MarkOccupied(placementId);

        // 小女孩移动：暖意 +18 并清温度，统一由结算系统处理。
        settlement.ApplyGirlMovedItem(item);
        EventBus.Publish(new ItemMovedEvent
        {
            itemId = item.itemId,
            by = by,
            hadTemperature = hadTemperature
        });

        return ItemActionResult.Ok(itemId, ItemActionStatus.Success, "小女孩把东西挪走了。");
    }

    // ------------------------------------------------------------------
    // 玩家触碰
    // ------------------------------------------------------------------

    /// <summary>
    /// 玩家触碰一件物品。返回的结果带提示文案，UI 可以直接显示。
    /// 只有真正产生效果的触碰才会消耗「本轮碰一次」。
    /// </summary>
    public ItemActionResult Touch(string itemId)
    {
        ItemActionResult result = EvaluateTouch(itemId);
        if (!string.IsNullOrEmpty(result.message))
        {
            EventBus.Publish(new ItemPromptEvent
            {
                itemId = itemId,
                message = result.message,
                status = result.status
            });
        }
        return result;
    }

    private ItemActionResult EvaluateTouch(string itemId)
    {
        if (session == null)
            return ItemActionResult.Fail(itemId, ItemActionStatus.ItemNotFound, "本局数据还没准备好。");

        if (flow != null && !flow.CanPlayerInteract)
            return ItemActionResult.Fail(itemId, ItemActionStatus.NotPlayerInputPhase, "现在还不能碰东西。");

        ItemData item = session.GetItem(itemId);
        if (item == null)
            return ItemActionResult.Fail(itemId, ItemActionStatus.ItemNotFound, "找不到这件物品。");
        if (item.isReturned)
            return ItemActionResult.Fail(itemId, ItemActionStatus.AlreadyReturned, "这件东西已经被拿回去了。");
        if (!item.IsInScene)
            return ItemActionResult.Fail(itemId, ItemActionStatus.NotInScene, "这件东西现在不在场景里。");
        if (item.hasTemperature)
            return ItemActionResult.Fail(itemId, ItemActionStatus.AlreadyTouched, "你已经触摸过了。");

        // 先让「已经是暖的 / 不在场景」给出更具体的提示，再提示本轮次数用完。
        if (config != null && session.touchesThisRound >= config.touchesPerRound)
            return ItemActionResult.Fail(itemId, ItemActionStatus.TouchUsedUpThisRound, "这一轮你已经碰过了。");

        // 区域被「区域持续」的住户占着：前两次只警告，第 3 次起强行生效。
        ResidentId blockingResident;
        if (TryGetBlockedResident(item.area, out blockingResident))
        {
            if (config != null && session.forceTouchWarningsUsed < config.forceTouchWarnCount)
            {
                session.forceTouchWarningsUsed++;
                EventBus.Publish(new ForceTouchWarnedEvent
                {
                    itemId = item.itemId,
                    blockingResident = blockingResident,
                    warningsRemaining = Mathf.Max(0, config.forceTouchWarnCount - session.forceTouchWarningsUsed)
                });
                return ItemActionResult.Fail(itemId, ItemActionStatus.ForceTouchWarned, "他还在那边，你现在碰不到。");
            }

            item.hasTemperature = true;
            ResidentData blocking = session.GetResident(blockingResident);
            if (blocking != null)
            {
                blocking.wasForceTouched = true;
                settlement.ApplyForceTouch(blockingResident);
            }
            session.SetNpcState(blockingResident, NpcState.Startled);
            // 被强碰之后他不再占着这个区域，区域恢复可用。
            ClearAreaBlocked(item.area);
            ConsumePlayerTouch(item);
            return ItemActionResult.Ok(itemId, ItemActionStatus.ForceTouchAccepted, "你还是碰到了，他被吓了一跳。");
        }

        item.hasTemperature = true;

        // 母亲的物品：本人不会来收，碰一下会让随机 1~2 位住户进入「疑惑」。
        // （母亲不参与本局时，不产生疑惑，避免"她不在场却在影响别人"。）
        if (item.owner == ItemOwner.Mother && (config == null || config.motherEnabled))
        {
            ApplyConfusion();
        }

        ConsumePlayerTouch(item);
        return ItemActionResult.Ok(itemId, ItemActionStatus.Success, "物品散发出了微光。");
    }

    /// <summary>消耗「本轮碰一次」，并广播 ItemTouchedEvent。</summary>
    private void ConsumePlayerTouch(ItemData item)
    {
        if (flow != null && flow.RegisterPlayerTouch(item.itemId, item.owner, false)) return;

        // 没有 GameFlow（例如单测）时自己记账，保证纯逻辑也能跑。
        session.touchesThisRound++;
        EventBus.Publish(new ItemTouchedEvent
        {
            itemId = item.itemId,
            owner = item.owner,
            hadTemperature = false
        });
    }

    /// <summary>
    /// 玩家触碰「区域持续」（不回家）的住户本人：前 2 次只警告，第 3 次起生效
    /// （他进入惊吓、暖意 −8，并解除区域占用）。不在本场景 / 在门里的住户碰不到。
    /// </summary>
    public ItemActionResult TouchResident(ResidentId id)
    {
        ItemActionResult result = EvaluateTouchResident(id);
        if (!string.IsNullOrEmpty(result.message))
        {
            EventBus.Publish(new ItemPromptEvent
            {
                itemId = null,
                message = result.message,
                status = result.status
            });
        }
        return result;
    }

    private ItemActionResult EvaluateTouchResident(ResidentId id)
    {
        if (session == null)
            return ItemActionResult.Fail(null, ItemActionStatus.ItemNotFound, "本局数据还没准备好。");

        if (flow != null && !flow.CanPlayerInteract)
            return ItemActionResult.Fail(null, ItemActionStatus.NotPlayerInputPhase, "现在还不能碰东西。");

        ResidentData resident = session.GetResident(id);
        if (resident == null)
            return ItemActionResult.Fail(null, ItemActionStatus.ItemNotFound, "找不到这个人。");

        // 只有「区域持续」（不回家、停在外面）的住户可以碰。
        if (resident.state != NpcState.StayOut || !resident.IsOutside)
            return ItemActionResult.Fail(null, ItemActionStatus.Rejected, "他在门里面，碰不到。");

        // 不在玩家所在场景的不能碰。
        if (session.playerArea != AreaId.None && resident.currentArea != session.playerArea)
            return ItemActionResult.Fail(null, ItemActionStatus.Rejected, "他不在这里。");

        if (config != null && session.touchesThisRound >= config.touchesPerRound)
            return ItemActionResult.Fail(null, ItemActionStatus.TouchUsedUpThisRound, "这一轮你已经碰过了。");

        // 前两次只警告，不消耗本轮触碰。
        if (config != null && session.forceTouchWarningsUsed < config.forceTouchWarnCount)
        {
            session.forceTouchWarningsUsed++;
            EventBus.Publish(new ForceTouchWarnedEvent
            {
                itemId = null,
                blockingResident = id,
                warningsRemaining = Mathf.Max(0, config.forceTouchWarnCount - session.forceTouchWarningsUsed)
            });
            return ItemActionResult.Fail(null, ItemActionStatus.ForceTouchWarned, "他还在那边，你现在碰不到。");
        }

        // 生效：惊吓 + 暖意 −8，并解除他占着的区域。
        resident.wasForceTouched = true;
        settlement.ApplyForceTouch(id);
        session.SetNpcState(id, NpcState.Startled);

        AreaId blockedArea = resident.stayOutArea;
        if (blockedArea != AreaId.None) ClearAreaBlocked(blockedArea);
        resident.stayOutArea = AreaId.None;

        ConsumePlayerTouchResident(resident);
        return ItemActionResult.Ok(null, ItemActionStatus.ForceTouchAccepted, "你碰到他了，他被吓了一跳。");
    }

    private void ConsumePlayerTouchResident(ResidentData resident)
    {
        string key = "npc:" + resident.id;
        if (flow != null && flow.RegisterPlayerTouch(key, ItemOwner.None, false)) return;

        session.touchesThisRound++;
        EventBus.Publish(new ItemTouchedEvent
        {
            itemId = key,
            owner = ItemOwner.None,
            hadTemperature = false
        });
    }

    /// <summary>碰母亲的物品 → 随机给 1~2 位能行动的住户「疑惑」。</summary>
    private void ApplyConfusion()
    {
        if (random == null || config == null) return;

        List<ResidentId> candidates = new List<ResidentId>
        {
            ResidentId.Grandma,
            ResidentId.Uncle,
            ResidentId.Girl
        };

        int count = random.Chance(config.confusionTwoChance)
            ? config.confusionMaxCount
            : config.confusionMinCount;
        count = Mathf.Clamp(count, 1, candidates.Count);

        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int index = random.Range(0, candidates.Count);
            ResidentId picked = candidates[index];
            candidates.RemoveAt(index);
            session.SetNpcState(picked, NpcState.Confused);
        }
    }

    // ------------------------------------------------------------------
    // 区域可用性（由 NPC 状态机写入）
    // ------------------------------------------------------------------

    /// <summary>标记某个区域被住户占着（对应「区域持续」状态）。</summary>
    public void SetAreaBlocked(AreaId area, ResidentId resident)
    {
        if (area == AreaId.None) return;
        blockedAreas[area] = resident;
    }

    public void ClearAreaBlocked(AreaId area)
    {
        blockedAreas.Remove(area);
    }

    public void ClearAllBlockedAreas()
    {
        blockedAreas.Clear();
    }

    public bool IsAreaBlocked(AreaId area)
    {
        ResidentId resident;
        return TryGetBlockedResident(area, out resident);
    }

    /// <summary>
    /// 这个区域是不是"真的"被占着：不光要登记过，
    /// 还要**那个人此刻真的在这个区域、而且真的在门外**（看得见）。
    /// 住户在 NPC 阶段会跨区域行动，登记不跟着走的话，
    /// 就会留下"看着没人、碰东西却被拦"的幽灵占用 —— 这里做校验并顺手自愈。
    /// </summary>
    public bool TryGetBlockedResident(AreaId area, out ResidentId resident)
    {
        resident = default(ResidentId);
        if (area == AreaId.None) return false;

        ResidentId id;
        if (!blockedAreas.TryGetValue(area, out id)) return false;

        ResidentData data = session != null ? session.GetResident(id) : null;
        if (data == null || !data.IsOutside || data.currentArea != area)
        {
            // 人已经不在这儿了：把这条脏记录删掉
            blockedAreas.Remove(area);
            return false;
        }

        resident = id;
        return true;
    }

    // ------------------------------------------------------------------
    // 内部
    // ------------------------------------------------------------------

    /// <summary>
    /// 放置不合规：除了返回失败，还发一条提示事件 ——
    /// 这样 NPC「放不下东西」时玩家也能看到对应台词（以前只有玩家触碰会发提示，NPC 的失败传不到提示层）。
    /// </summary>
    private ItemActionResult FailPlacement(string itemId, ItemType type)
    {
        ItemActionResult result = ItemActionResult.Fail(itemId, ItemActionStatus.PlacementBlocked,
            "「" + type.ToDisplayName() + "」不能放在这里，只能放在" +
            ItemPlacementRules.DescribeAllowed(type) + "。");

        EventBus.Publish(new ItemPromptEvent
        {
            itemId = itemId,
            message = result.message,
            status = result.status
        });

        return result;
    }

    private void PlaceInScene(ItemData item, AreaId area, string placementId)
    {
        item.isInPool = false;
        item.isReturned = false;
        item.position = ItemPosition.Scene;
        item.area = area;
        item.placementId = placementId;
        item.hasTemperature = false;
        PlacementRegistry.MarkOccupied(placementId);
    }
}
