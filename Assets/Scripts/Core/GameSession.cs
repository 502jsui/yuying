using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一局游戏的运行时数据。它不挂在任何场景上，切场景不会丢，
/// 由 GameRoot 持有、由 GameFlow 驱动。
/// </summary>
[Serializable]
public class GameSession
{
    public int seed;
    public int round = 1;
    public bool isRunning;
    public bool isGameOver;

    public AreaId playerArea = AreaId.FirstFloor;

    public List<ResidentData> residents = new List<ResidentData>();
    public List<ItemData> items = new List<ItemData>();

    /// <summary>本局到目前为止用掉的“强行触碰警告”次数，整局只有 2 次。</summary>
    public int forceTouchWarningsUsed;

    /// <summary>本轮玩家已经触碰的物品数。</summary>
    public int touchesThisRound;

    /// <summary>本轮不回家的住户（ResidentId.Girl 表示“没人不回家”时要靠 hasAbsentResident 判断）。</summary>
    public ResidentId absentResident;
    public bool hasAbsentResident;

<<<<<<< HEAD
    // ---- 文案 / 教学：整局只触发一次的标记 ----
    public bool openingDone;
    public bool tutorialFirstMoveDone;
    public bool tutorialFirstTouchDone;
    public bool tutorialFirstOutDone;
    public bool tutorialFirstTakenDone;
    public bool tutorialFirstSettleDone;

=======
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
    public event Action<ResidentId, float, float> WarmthChanged;
    public event Action<ResidentId, float, float> AwarenessChanged;
    public event Action<ResidentId, NpcState, NpcState> NpcStateChanged;

    public void Reset(GameConfig config, int sessionSeed)
    {
        config.EnsureDefaults();
        seed = sessionSeed;
        round = 1;
        isRunning = false;
        isGameOver = false;
        playerArea = AreaId.FirstFloor;
        forceTouchWarningsUsed = 0;
        touchesThisRound = 0;
        absentResident = ResidentId.Girl;
        hasAbsentResident = false;
<<<<<<< HEAD
        openingDone = false;
        tutorialFirstMoveDone = false;
        tutorialFirstTouchDone = false;
        tutorialFirstOutDone = false;
        tutorialFirstTakenDone = false;
        tutorialFirstSettleDone = false;
=======
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0

        residents = new List<ResidentData>();
        for (int i = 0; i < config.residents.Count; i++)
        {
            ResidentConfig definition = config.residents[i];
            if (definition == null) continue;
            ResidentData data = new ResidentData(definition.id, definition.warmth, definition.awareness, definition.hasAwareness);
            data.actionMin = definition.actionMin;
            data.actionMax = definition.actionMax;
            data.homeArea = definition.homeArea;
            data.currentArea = definition.homeArea;
            data.canAct = definition.id != ResidentId.Mother;
            residents.Add(data);
        }

        items = new List<ItemData>();
    }

    public ResidentData GetResident(ResidentId id)
    {
        for (int i = 0; i < residents.Count; i++)
        {
            if (residents[i].id == id) return residents[i];
        }
        return null;
    }

    public ResidentData EnsureResident(ResidentId id, GameConfig config)
    {
        ResidentData existing = GetResident(id);
        if (existing != null) return existing;

        ResidentConfig definition = config.GetResidentConfig(id);
        ResidentData created = new ResidentData(id, definition.warmth, definition.awareness, definition.hasAwareness);
        created.actionMin = definition.actionMin;
        created.actionMax = definition.actionMax;
        created.homeArea = definition.homeArea;
        created.currentArea = definition.homeArea;
        created.canAct = id != ResidentId.Mother;
        residents.Add(created);
        return created;
    }

    public ItemData GetItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].itemId == itemId) return items[i];
        }
        return null;
    }

    public ItemData EnsureItem(string itemId, ItemOwner owner, ItemType type)
    {
        ItemData existing = GetItem(itemId);
        if (existing != null)
        {
            if (type != ItemType.Unknown) existing.type = type;
            return existing;
        }

        ItemData created = new ItemData(itemId, owner);
        created.type = type;
        items.Add(created);
        return created;
    }

    public List<ItemData> GetItemsOwnedBy(ItemOwner owner)
    {
        List<ItemData> result = new List<ItemData>();
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].owner == owner) result.Add(items[i]);
        }
        return result;
    }

    public List<ItemData> GetItemsInArea(AreaId area)
    {
        List<ItemData> result = new List<ItemData>();
        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            if (item.IsInScene && item.area == area) result.Add(item);
        }
        return result;
    }

    /// <summary>是否还有没被拿出来的物品（决定“拿出东西”行动能不能发生）。</summary>
    public bool HasItemInPool(ItemOwner owner)
    {
        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            if (item.owner == owner && item.isInPool && !item.isReturned) return true;
        }
        return false;
    }

    public float AddWarmth(ResidentId id, float delta, GameConfig config)
    {
        ResidentData resident = GetResident(id);
        if (resident == null) return 0f;

        float previous = resident.warmth;
        float max = config != null ? config.maxStatValue : 100f;
        resident.warmth = Mathf.Clamp(resident.warmth + delta, 0f, max);
        if (!Mathf.Approximately(previous, resident.warmth))
        {
            if (WarmthChanged != null) WarmthChanged(id, previous, resident.warmth);
            EventBus.Publish(new WarmthChangedEvent { id = id, previous = previous, current = resident.warmth });
        }
        return resident.warmth;
    }

    public float AddAwareness(ResidentId id, float delta, GameConfig config)
    {
        ResidentData resident = GetResident(id);
        if (resident == null) return 0f;

        float previous = resident.awareness;
        float max = config != null ? config.maxStatValue : 100f;
        resident.awareness = Mathf.Clamp(resident.awareness + delta, 0f, max);
        if (!Mathf.Approximately(previous, resident.awareness))
        {
            if (AwarenessChanged != null) AwarenessChanged(id, previous, resident.awareness);
            EventBus.Publish(new AwarenessChangedEvent { id = id, previous = previous, current = resident.awareness });
        }
        return resident.awareness;
    }

    public void SetNpcState(ResidentId id, NpcState state)
    {
        ResidentData resident = GetResident(id);
        if (resident == null || resident.state == state) return;

        NpcState previous = resident.state;
        resident.state = state;
        if (NpcStateChanged != null) NpcStateChanged(id, previous, state);
        EventBus.Publish(new NpcStateChangedEvent { id = id, previous = previous, current = state });
    }

    /// <summary>把每轮的临时字段清掉，并重置“玩家本轮已触碰次数”。</summary>
    public void BeginRound(bool absent, ResidentId absentId)
    {
        touchesThisRound = 0;
        hasAbsentResident = absent;
        absentResident = absentId;
        for (int i = 0; i < residents.Count; i++)
        {
            residents[i].BeginRound(true);
        }
    }

    /// <summary>
    /// 行动结束后定下本轮「谁不回家」，留给下一轮的玩家阶段用
    /// （判定顺序：先判有没有人不回家，再判是谁）。
    /// </summary>
    public void SetAbsentDecision(bool absent, ResidentId absentId)
    {
        hasAbsentResident = absent;
        absentResident = absentId;
    }

    public GameSessionSnapshot CreateSnapshot()
    {
        GameSessionSnapshot snapshot = new GameSessionSnapshot();
        snapshot.seed = seed;
        snapshot.round = round;
        snapshot.playerArea = (int)playerArea;
        snapshot.forceTouchWarningsUsed = forceTouchWarningsUsed;
        snapshot.residents = residents;
        snapshot.items = items;
        return snapshot;
    }

    public void LoadSnapshot(GameSessionSnapshot snapshot)
    {
        if (snapshot == null) return;
        seed = snapshot.seed;
        round = snapshot.round;
        playerArea = (AreaId)snapshot.playerArea;
        forceTouchWarningsUsed = snapshot.forceTouchWarningsUsed;
        residents = snapshot.residents ?? new List<ResidentData>();
        items = snapshot.items ?? new List<ItemData>();
    }
}

/// <summary>
/// 可 JSON 序列化的快照，用于调试存档 / 回放。
/// </summary>
[Serializable]
public class GameSessionSnapshot
{
    public int seed;
    public int round;
    public int playerArea;
    public int forceTouchWarningsUsed;
    public List<ResidentData> residents;
    public List<ItemData> items;
}
