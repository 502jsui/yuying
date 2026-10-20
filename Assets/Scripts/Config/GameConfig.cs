using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局数值配置。所有“文档 4.7 参数汇总”里的值集中在这里，
/// 策划改数值不需要碰代码；运行时如果没有配置资产，会自动用默认值兜底。
/// </summary>
[CreateAssetMenu(menuName = "余音/游戏配置", fileName = "GameConfig")]
public class GameConfig : ScriptableObject
{
    [Header("回合")]
    [Min(1)] public int totalRounds = 6;
    [Min(1)] public int touchesPerRound = 1;

    [Header("阈值")]
    public float highThreshold = 60f;
    public float maxStatValue = 100f;

    [Header("母亲")]
    [Tooltip("母亲是否参与本局。本次 Demo 关掉它：没人会去找她、她不会开门，结局只走「母亲低」的四档。")]
    public bool motherEnabled = true;
    public float motherInitialWarmth = 30f;
    public float motherInitialAwareness = 30f;
    public float motherOpenAwareness = 60f;
    public float motherOpenWarmth = 61f;
    public float motherOpenAwarenessValue = 63f;

    [Header("暖意 / 知晓度变化")]
    public float itemTakenWithTemperatureWarmth = 18f;
    public float itemTakenWithTemperatureAwareness = 15f;
    public float itemTakenWithoutTemperatureAwareness = 15f;
    public float talkWarmth = 4f;
    public float talkAwareness = 8f;
    public float talkMotherAwareness = 11f;
    public float girlMoveWarmth = 18f;

    [Header("强行触碰")]
    [Min(0)] public int forceTouchWarnCount = 2;
    public float forceTouchWarmthPenalty = 8f;
    public float forceTouchContagionPenalty = 5f;

    [Header("不回家")]
    [Range(0f, 1f)] public float absentRoundChance = 0.8f;

    [Header("住户行动权重（总和不必为 100）")]
    public float wanderWeight = 35f;
    public float talkWeight = 25f;
    public float takeOutWeight = 20f;
    public float takeBackWeight = 20f;
    [Tooltip("小女孩「移动东西」的权重（她没有自己的物品，也不交流）。")]
    public float girlMoveWeight = 20f;

    [Header("区域持续")]
    [Tooltip("不回家的住户会停在这些区域之一，该区域本回合不可用。")]
    public List<AreaId> stayOutAreas = new List<AreaId> { AreaId.FirstFloor, AreaId.Backyard };

    [Tooltip("每轮场上保持几件物品（不够就从物品池随机摆几件出来）。")]
    [Min(0)] public int sceneItemCount = 3;

    [Header("交流对象")]
    [Range(0f, 1f)] public float talkMotherChance = 0.2f;

    [Header("疑惑")]
    [Range(0f, 1f)] public float confusionTwoChance = 0.3f;
    [Min(1)] public int confusionMinCount = 1;
    [Min(1)] public int confusionMaxCount = 2;

    [Header("行动阶段")]
    [Tooltip("NPC 行动阶段连续这么久没有任何进展时，才强制进入下一阶段。")]
    [Min(0.1f)] public float npcPhaseTimeout = 30f;
    [Tooltip("每个阶段之间留多久（秒），给表现层跟上节奏；设 0 表示不额外等待。")]
    [Min(0f)] public float phaseStepDelay = 0.2f;
    [Tooltip("每个 NPC 行动之间间隔多久（秒），给表现层播动画的时间；设 0 表示逻辑上瞬间跑完。")]
    [Min(0f)] public float npcActionStepDelay = 0.35f;
    [Tooltip("住户走到目标放置点需要的时间（秒）。")]
    [Min(0.05f)] public float npcTravelDuration = 0.7f;
    [Tooltip("住户走位的速度（世界单位/秒），用来按距离算走位时长。")]
    [Min(0.1f)] public float npcTravelSpeed = 3.5f;
    [Tooltip("进入游戏场景后是否自动开局。接入主界面后改成 false。")]
    public bool autoStartOnSceneLoad = true;

    [Header("住户初始值")]
    public List<ResidentConfig> residents = new List<ResidentConfig>();

    [Header("物品（每局开始时按这张表建物品池）")]
    [Tooltip("startsInScene = true 的物品一开局就在场景里，其余在物品池里等 NPC 拿出来。")]
    public List<ItemConfig> items = new List<ItemConfig>();

    [Header("音频（可留空）")]
    public AudioClip rainLoop;
    public AudioClip musicLoop;

    [Tooltip("进游戏是否自动循环播放 musicLoop。如果有别的 BGM 控制器接管，把它关掉。")]
    public bool autoPlayMusicOnStart = true;

    private void Reset()
    {
        ApplyDefaultResidents();
        ApplyDefaultItems();
    }

    /// <summary>从 Resources 读取配置；没有资产时返回一份默认配置，保证任何场景都能跑。</summary>
    public static GameConfig Load()
    {
        GameConfig config = Resources.Load<GameConfig>("GameConfig");
        if (config == null)
        {
            config = CreateInstance<GameConfig>();
            config.name = "GameConfig (Runtime Default)";
            Debug.LogWarning("未找到 Resources/GameConfig.asset，本次运行使用代码默认数值。可在菜单 余音/创建默认配置 生成资产。");
        }

        config.EnsureDefaults();
        return config;
    }

    /// <summary>补齐缺失/非法的数据，避免资产被别人改坏后运行时崩溃。</summary>
    public void EnsureDefaults()
    {
        totalRounds = Mathf.Max(1, totalRounds);
        touchesPerRound = Mathf.Max(1, touchesPerRound);
        maxStatValue = Mathf.Max(1f, maxStatValue);
        confusionMinCount = Mathf.Max(1, confusionMinCount);
        confusionMaxCount = Mathf.Max(confusionMinCount, confusionMaxCount);
        if (npcPhaseTimeout <= 0f) npcPhaseTimeout = 30f;

        if (residents == null || residents.Count == 0) ApplyDefaultResidents();
        if (items == null || items.Count == 0) ApplyDefaultItems();
        if (stayOutAreas == null || stayOutAreas.Count == 0)
        {
            stayOutAreas = new List<AreaId> { AreaId.FirstFloor, AreaId.Backyard };
        }
    }

    public void ApplyDefaultResidents()
    {
        residents = new List<ResidentConfig>
        {
            new ResidentConfig(ResidentId.Grandma, 26f, 20f, true, 2, 3),
            new ResidentConfig(ResidentId.Uncle, 26f, 20f, true, 2, 3),
            new ResidentConfig(ResidentId.Girl, 30f, 0f, false, 1, 2, AreaId.SecondFloor),
            new ResidentConfig(ResidentId.Mother, motherInitialWarmth, motherInitialAwareness, true, 0, 0, AreaId.SecondFloor)
        };
    }

    public ResidentConfig GetResidentConfig(ResidentId id)
    {
        EnsureDefaults();
        for (int i = 0; i < residents.Count; i++)
        {
            if (residents[i] != null && residents[i].id == id) return residents[i];
        }

        switch (id)
        {
            case ResidentId.Grandma: return new ResidentConfig(ResidentId.Grandma, 26f, 20f, true, 2, 3);
            case ResidentId.Uncle: return new ResidentConfig(ResidentId.Uncle, 26f, 20f, true, 2, 3);
            case ResidentId.Girl: return new ResidentConfig(ResidentId.Girl, 30f, 0f, false, 1, 2, AreaId.SecondFloor);
            default: return new ResidentConfig(ResidentId.Mother, motherInitialWarmth, motherInitialAwareness, true, 0, 0);
        }
    }

    /// <summary>
    /// 文档 2.5 的物品安排：老婆婆 3 件、大叔 3 件，母亲 3 件本次不做但先留表。
    /// 一楼没有晾衣架，老婆婆衣服从后院衣架开局；大叔的花盆从一楼开局。
    /// </summary>
    public void ApplyDefaultItems()
    {
        items = new List<ItemConfig>
        {
            new ItemConfig("grandma_01", ItemOwner.Grandma, ItemType.Clothes, true, AreaId.Backyard, "backyard_rack_a"),
            new ItemConfig("grandma_02", ItemOwner.Grandma, ItemType.FlowerPot, false, AreaId.None, null),
            new ItemConfig("grandma_03", ItemOwner.Grandma, ItemType.MedicineBag, false, AreaId.None, null),

            new ItemConfig("aunt_01", ItemOwner.Uncle, ItemType.FlowerPot, true, AreaId.FirstFloor, "first_ground_a"),
            new ItemConfig("aunt_02", ItemOwner.Uncle, ItemType.WoodCarving, false, AreaId.None, null),
            new ItemConfig("aunt_03", ItemOwner.Uncle, ItemType.ToolBag, false, AreaId.None, null),

            new ItemConfig("mother_01", ItemOwner.Mother, ItemType.Clothes, true, AreaId.SecondFloor, "second_rack"),
            new ItemConfig("mother_02", ItemOwner.Mother, ItemType.WoodenBox, true, AreaId.SecondFloor, "second_ground"),
            new ItemConfig("mother_03", ItemOwner.Mother, ItemType.Diary, true, AreaId.SecondFloor, "second_on_item")
        };
    }

    public ItemConfig GetItemConfig(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        EnsureItemDefaults();
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].itemId == itemId) return items[i];
        }
        return null;
    }

    private void EnsureItemDefaults()
    {
        if (items == null || items.Count == 0) ApplyDefaultItems();
    }
}

/// <summary>
/// 一件物品的静态配置。物品的运行时状态（温度 / 位置 / 是否收回）在 ItemData 里。
/// </summary>
[Serializable]
public class ItemConfig
{
    public string itemId;
    public ItemOwner owner;
    public ItemType type = ItemType.Unknown;

    [Tooltip("true：开局就在场景里；false：先放在物品池。")]
    public bool startsInScene;

    public AreaId startArea = AreaId.None;
    public string startPlacementId;

    public ItemConfig()
    {
    }

    public ItemConfig(string itemId, ItemOwner owner, ItemType type, bool startsInScene, AreaId startArea, string startPlacementId)
    {
        this.itemId = itemId;
        this.owner = owner;
        this.type = type;
        this.startsInScene = startsInScene;
        this.startArea = startArea;
        this.startPlacementId = startPlacementId;
    }
}

[Serializable]
public class ResidentConfig
{
    public ResidentId id;
    public float warmth;
    public float awareness;
    public bool hasAwareness;
    public int actionMin;
    public int actionMax;
    public AreaId homeArea = AreaId.FirstFloor;

    public ResidentConfig(ResidentId id, float warmth, float awareness, bool hasAwareness, int actionMin, int actionMax, AreaId homeArea = AreaId.FirstFloor)
    {
        this.id = id;
        this.warmth = warmth;
        this.awareness = awareness;
        this.hasAwareness = hasAwareness;
        this.actionMin = actionMin;
        this.actionMax = actionMax;
        this.homeArea = homeArea;
    }
}
