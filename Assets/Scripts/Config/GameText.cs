using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 文案表：把交付的所有台词集中在一个资产里（Assets/Resources/GameText.asset）。
/// 文案同学只改资产，不用动代码；资产缺失时代码里的默认值兜底。
/// </summary>
[CreateAssetMenu(menuName = "余音/文案表", fileName = "GameText")]
public class GameText : ScriptableObject
{
    [Serializable]
    public class ItemMonologue
    {
        public string itemId;
        [TextArea] public string text;
    }

    [Header("开场：醒来时的独白（按顺序播）")]
    [TextArea] public string[] openingMonologue = new string[0];

    [Header("玩法教学 A：开场")]
    [TextArea] public string tutorialA1;
    [TextArea] public string tutorialA2;
    [TextArea] public string tutorialA3;

    [Header("玩法教学 B：第一次触碰")]
    [TextArea] public string tutorialB1;
    [TextArea] public string tutorialB2;

    [Header("玩法教学 C：首次结算")]
    [TextArea] public string tutorialC1;
    [TextArea] public string tutorialC2;
    [TextArea] public string tutorialC3;

    [Header("玩法教学 D：兜底（卡住时）")]
    [TextArea] public string tutorialD1;
    [TextArea] public string tutorialD2;
    [TextArea] public string tutorialD3;

    [Header("警告词：强行触碰（第 1、2 次）")]
    [TextArea] public string[] forceTouchWarnings = new string[0];

    [Header("警告词：强行触碰第 3 次起（普通物品，随机取一条）")]
    [TextArea] public string[] forceTouchAccepted = new string[0];

    [Header("警告词：强行触碰重物（木箱类，优先取这条）")]
    [TextArea] public string forceTouchHeavy;

    [Header("警告词：其它")]
    [TextArea] public string alreadyTouched;
    [TextArea] public string placementBlockedClothes;
    [TextArea] public string placementBlockedMedicineBag;
    [TextArea] public string placementBlockedWoodenBox;

    [Header("物品独白（按 itemId）")]
    public List<ItemMonologue> itemMonologues = new List<ItemMonologue>();

    // ------------------------------------------------------------------
    // 读取
    // ------------------------------------------------------------------

    public static GameText Load()
    {
        GameText text = Resources.Load<GameText>("GameText");
        if (text == null)
        {
            text = CreateInstance<GameText>();
            text.name = "GameText (Runtime Default)";
            text.ApplyDefaults();
        }
        return text;
    }

    /// <summary>把交付的文案填成默认值（新建资产 / 资产缺字段时用）。</summary>
    public void ApplyDefaults()
    {
        if (openingMonologue == null || openingMonologue.Length == 0)
        {
            openingMonologue = new[]
            {
                "好冷......我怎么在这醒过来，",
                "雨的味道好重，下雨了吗",
                "对了，是下雨了，我要给妈妈取东西来着，是什么来着......",
                "算了，妈还在家等我，先回去问问吧",
                "门缝里都还透着光，唔，大家应该都还没睡。他们的东西怎么都还放在外面？下雨了，会淋湿的......",
                "那就帮他们收一收吧。都是些忘了收的东西"
            };
        }

        if (string.IsNullOrEmpty(tutorialA1)) tutorialA1 = "门是进不去的。我只能在楼道和院子里走走";
        if (string.IsNullOrEmpty(tutorialA2)) tutorialA2 = "（用方向键走）";
        if (string.IsNullOrEmpty(tutorialA3)) tutorialA3 = "他们的东西就放在那儿，谁也没收";
        if (string.IsNullOrEmpty(tutorialB1)) tutorialB1 = "这个东西...我好像能碰到";
        if (string.IsNullOrEmpty(tutorialB2)) tutorialB2 = "这件物品似乎因为我的触碰发生了些改变";
        if (string.IsNullOrEmpty(tutorialC1)) tutorialC1 = "有人出来了";
        if (string.IsNullOrEmpty(tutorialC2)) tutorialC2 = "他把它拿回去了";
        if (string.IsNullOrEmpty(tutorialC3)) tutorialC3 = "好像只能触碰1次。那……碰哪个好";
        if (string.IsNullOrEmpty(tutorialD1)) tutorialD1 = "得走近一点才碰得到";
        if (string.IsNullOrEmpty(tutorialD2)) tutorialD2 = "这扇门一直是关着的";
        if (string.IsNullOrEmpty(tutorialD3)) tutorialD3 = "这里没什么可碰的";

        if (forceTouchWarnings == null || forceTouchWarnings.Length == 0)
        {
            forceTouchWarnings = new[]
            {
                "真是奇怪，这东西变得好重，我没办法帮忙收回去，我生病了吗",
                "还是先不要动了吧，也许这家人是故意放在这的？"
            };
        }

        if (forceTouchAccepted == null || forceTouchAccepted.Length == 0)
        {
            forceTouchAccepted = new[]
            {
                "啊！抱歉，我不乱动了",
                "对不起，我以为……",
                "不好意思，我离远一点"
            };
        }

        if (string.IsNullOrEmpty(forceTouchHeavy)) forceTouchHeavy = "呃啊，好痛，还是不要乱动了，砸到手就不好了";
        if (string.IsNullOrEmpty(alreadyTouched)) alreadyTouched = "已经收好了，不需要再动了";
        if (string.IsNullOrEmpty(placementBlockedClothes)) placementBlockedClothes = "刚洗好的衣服，不能放在地上";
        if (string.IsNullOrEmpty(placementBlockedMedicineBag)) placementBlockedMedicineBag = "地上都是水，放地上的话得冲洗药包，还是不要放在地上了吧";
        if (string.IsNullOrEmpty(placementBlockedWoodenBox)) placementBlockedWoodenBox = "好沉......看来我搬不动它，就放在地上吧";

        if (itemMonologues == null || itemMonologues.Count == 0)
        {
            itemMonologues = new List<ItemMonologue>
            {
                Mono("grandma_01", "袖口补过一次，针脚很密。这衣服的主人针线活很好，应该也是那种很有耐心的人"),
                Mono("grandma_02", "花开得不算好，但一片枯叶都没有，养它的人一定很细心"),
                Mono("grandma_03", "纸角折得整整齐齐。看样子主人很有条理"),
                Mono("aunt_01", "这个花盆里的花长得有点歪啊......主人好像并未悉心打理"),
                Mono("aunt_02", "这木雕这么精美，一定是一位热爱木雕的人所拥有的吧"),
                Mono("aunt_03", "外面脏得很，里面工具却排列的很整齐，这个包的主人会不会也很有反差？")
            };
        }
    }

    private static ItemMonologue Mono(string itemId, string text)
    {
        ItemMonologue mono = new ItemMonologue();
        mono.itemId = itemId;
        mono.text = text;
        return mono;
    }

    /// <summary>开场独白第 index 条（越界返回 null）。</summary>
    public string OpeningLine(int index)
    {
        if (openingMonologue == null) return null;
        if (index < 0 || index >= openingMonologue.Length) return null;
        return openingMonologue[index];
    }

    /// <summary>强行触碰第 1 / 2 次的警告词（used 从 1 开始）。</summary>
    public string ForceTouchWarning(int used)
    {
        if (forceTouchWarnings == null || forceTouchWarnings.Length == 0) return string.Empty;
        int index = Mathf.Clamp(used - 1, 0, forceTouchWarnings.Length - 1);
        return forceTouchWarnings[index];
    }

    /// <summary>强行触碰第 3 次起：木箱类用「重物」句，其余随机取一条。</summary>
    public string ForceTouchAcceptedLine(ItemType type)
    {
        // 重物（木箱 / 工具包 / 木雕 / 花盆）用"砸到手"那句，其它在前 3 条里随机
        if (ItemPlacementRules.IsHeavy(type) && !string.IsNullOrEmpty(forceTouchHeavy)) return forceTouchHeavy;
        if (forceTouchAccepted == null || forceTouchAccepted.Length == 0) return string.Empty;
        return forceTouchAccepted[UnityEngine.Random.Range(0, forceTouchAccepted.Length)];
    }

    /// <summary>放置规则被挡时的台词：衣服 / 药包 / 木箱有专用句，其余用传进来的兜底文案。</summary>
    public string PlacementBlocked(ItemType type, string fallback)
    {
        switch (type)
        {
            case ItemType.Clothes: return string.IsNullOrEmpty(placementBlockedClothes) ? fallback : placementBlockedClothes;
            case ItemType.MedicineBag: return string.IsNullOrEmpty(placementBlockedMedicineBag) ? fallback : placementBlockedMedicineBag;
            case ItemType.WoodenBox: return string.IsNullOrEmpty(placementBlockedWoodenBox) ? fallback : placementBlockedWoodenBox;
            default: return fallback;
        }
    }

    /// <summary>按 itemId 取独白（没配返回 null）。</summary>
    public string ItemMonologueFor(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || itemMonologues == null) return null;
        for (int i = 0; i < itemMonologues.Count; i++)
        {
            ItemMonologue mono = itemMonologues[i];
            if (mono != null && mono.itemId == itemId && !string.IsNullOrEmpty(mono.text)) return mono.text;
        }
        return null;
    }
}
