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

    [Header("三档结算演出")]
    public List<EndingScript> endingScripts = new List<EndingScript>();

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
        else
        {
            text.EnsureEndingDefaults();
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

        EnsureEndingDefaults();
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

    /// <summary>取一套结局演出；资产缺这一档时补默认文案。</summary>
    public EndingScript EndingScriptFor(EndingId id)
    {
        EnsureEndingDefaults();
        for (int i = 0; i < endingScripts.Count; i++)
        {
            EndingScript script = endingScripts[i];
            if (script != null && script.id == id) return script;
        }
        return null;
    }

    /// <summary>旧资产没有结局字段时自动补齐，不需要手工迁移。</summary>
    public void EnsureEndingDefaults()
    {
        if (endingScripts == null) endingScripts = new List<EndingScript>();

        EndingId[] ids =
        {
            EndingId.RainInTheCorridor,
            EndingId.WhenSheWasLittle,
            EndingId.BeforeSnowStops
        };

        for (int i = 0; i < ids.Length; i++)
        {
            EndingScript existing = null;
            for (int j = 0; j < endingScripts.Count; j++)
            {
                EndingScript candidate = endingScripts[j];
                if (candidate != null && candidate.id == ids[i]) { existing = candidate; break; }
            }

            if (existing == null || existing.lines == null || existing.lines.Count == 0)
            {
                if (existing == null)
                {
                    endingScripts.Add(DefaultEndingScript(ids[i]));
                }
                else
                {
                    EndingScript defaults = DefaultEndingScript(ids[i]);
                    existing.title = defaults.title;
                    existing.lines = defaults.lines;
                }
            }
        }
    }

    private static EndingScript DefaultEndingScript(EndingId id)
    {
        switch (id)
        {
            case EndingId.RainInTheCorridor: return BuildRainInTheCorridor();
            case EndingId.WhenSheWasLittle: return BuildWhenSheWasLittle();
            case EndingId.BeforeSnowStops: return BuildBeforeSnowStops();
            default: return new EndingScript { id = id, title = EndingRules.TitleOf(id) };
        }
    }

    private static EndingScript BuildRainInTheCorridor()
    {
        return Script(EndingId.RainInTheCorridor,
            Line("旁白", "楼道里最后一个人回家了。", 1.6f, EndingCue.RainHeavy),
            Line("旁白", "门没有关严。", 1.6f, EndingCue.LightsOut),
            Line("妈妈的声", "…… 你回去吧。", 1.6f),
            Line("主人公", "妈 ....... 妈？", 0.8f),
            Line("妈妈的声", "东西我明天给你拿。", 0.8f),
            Line("妈妈的声", "明天。", 1.6f),
            Line("旁白", "门里一直没有脚步声走开。", 1.6f, EndingCue.FocusMotherDoor),
            Line("旁白", "门外也没有。", 1.6f),
            Line("主人公", "妈 , 为什么不让我留下 ... 我不想走。", 0.8f),
            Line("妈妈的声", "（忽然很轻）雨停了就好了。", 1.6f),
            Line("旁白", "门里又亮了一点。她大概是在等一句话。", 1.6f),
            Line("旁白", "没有人说。", 1.6f),
            Line("旁白", "她没有开门。", 1.6f),
            Line("旁白", "他也没有走。", 1.6f),
            Line("旁白", "雨没有停。", 3f));
    }

    private static EndingScript BuildWhenSheWasLittle()
    {
        return Script(EndingId.WhenSheWasLittle,
            Card("葬礼"),
            Line("旁白", "葬礼上。", 1.6f, EndingCue.AllAtDoors),
            Line("旁白", "她抬起头，看向走廊空着的那一头。", 1.6f),
            Line("女孩", "（小声）哥哥，你怎么站那么远。", 1.6f),
            Line("主人公", "……", 1.6f),
            Line("女孩", "（自己接话）你不过来吗。", 1.6f),
            Line("主人公", "……", 1.6f),
            Line("女孩", "那我自己过去。", 1.6f),
            Line("旁白", "她似乎能看见你", 1.6f),
            Line("旁白", "但你并没有回答", 1.6f),
            Card("三年后"),
            Line("旁白", "院子里。她长高了，抬头往楼道看。", 1.6f, EndingCue.GirlOlder),
            Line("女孩", "哥哥？", 1.6f),
            Line("主人公", "……", 1.6f),
            Line("女孩", "…… 你今天在啊。", 1.6f),
            Card("又几年"),
            Line("旁白", "楼空了一半。她回来搬东西，穿过楼道。", 1.6f, EndingCue.GirlOlder),
            Line("女孩", "（走过时，头也没抬）我走了啊。", 1.6f),
            Line("主人公", "……", 1.6f),
            Line("旁白", "这一次她没有等回答。", 1.6f),
            Card("搬走那天"),
            Line("旁白", "楼下堆着箱子。灯坏了几盏。她站在楼道口，最后回头。", 1.6f, EndingCue.GirlLeaving),
            Line("旁白", "她搬走的时候，回头看了一眼。", 1.6f),
            Line("女孩", "（对空处）…… 我小时候是不是在这儿见过一个人。", 1.6f),
            Line("主人公", "……", 1.6f),
            Line("女孩", "（笑了一下）算了。", 1.6f),
            Line("旁白", "她转身上车。", 1.6f, EndingCue.GirlAlone),
            Line("旁白", "她没有再问。", 3f));
    }

    private static EndingScript BuildBeforeSnowStops()
    {
        return Script(EndingId.BeforeSnowStops,
            Line("旁白", "雨小了。", 1.6f, EndingCue.DoorsOpen),
            Line("旁白", "楼上楼下，门一扇一扇开了。", 1.6f),
            Line("老婆婆", "好孩子 ... 安心吧，奶奶不会忘记你，这里不会忘记你，大胆的往前走吧", 1.6f),
            Line("大叔", "（别过脸）东西别拿了。", 0.8f),
            Line("大叔", "等你再回来的时候，我会再给你雕一个出来", 1.6f),
            Line("旁白", "那把刻刀，他攥了一夜。", 1.6f),
            Line("母亲", "（终于出声）我的小宝 ...... 我的小宝啊！", 1.6f),
            Line("母亲", "你是不是要走了。", 1.6f),
            Line("母亲", "没关系的，都没关系的。", 1.6f),
            Line("母亲", "妈妈，妈妈会好好的，你放心。", 1.6f),
            Line("女孩", "（从门后探出来）哥哥，你为什么这么凉啊。", 1.6f),
            Line("旁白", "她说完就缩回去了。没有人怪她。", 1.6f, EndingCue.RainFadeOut),
            Line("旁白", "走廊尽头亮了起来，不是灯。", 1.6f),
            Line("旁白", "所有门都开着。所有人都站着，没有人开口，也没有人走开。", 1.6f),
            Line("旁白", "他往那边走。这一步没有声音。", 1.6f, EndingCue.WalkToExit),
            Line("旁白", "这一次没有人喊他回去。", 1.6f),
            Line("旁白", "他一直走到走廊尽头，一次都没有回头。", 1.6f),
            Line("旁白", "雪落下来，落在走廊上，没有声音。", 1.6f, EndingCue.SnowStart),
            Line("旁白", "门一扇一扇，都没关。", 3f, EndingCue.FadeToWhite));
    }

    private static EndingScript Script(EndingId id, params EndingLine[] lines)
    {
        EndingScript script = new EndingScript
        {
            id = id,
            title = EndingRules.TitleOf(id)
        };
        if (lines != null) script.lines.AddRange(lines);
        return script;
    }

    private static EndingLine Card(string text)
    {
        EndingLine line = Line(null, null, 0.8f, EndingCue.TimeCard);
        line.cueText = text;
        line.cueSeconds = 2.4f;
        return line;
    }

    private static EndingLine Line(
        string speaker,
        string text,
        float pauseAfterSeconds,
        EndingCue cue = EndingCue.None)
    {
        return new EndingLine
        {
            speaker = speaker,
            text = text,
            holdSeconds = 0f,
            pauseAfterSeconds = pauseAfterSeconds,
            cue = cue,
            cueText = null,
            cueSeconds = 0f
        };
    }
}
