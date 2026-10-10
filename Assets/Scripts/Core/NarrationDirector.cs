using System.Collections;
using UnityEngine;

/// <summary>
/// 文案触发中枢：把玩法事件翻译成 GameText 里的一句话，交给统一提示通道显示。
/// 只管"什么时候说什么"，不负责画（画在 PromptOverlay）。
/// </summary>
[DefaultExecutionOrder(-800)]
public class NarrationDirector : MonoBehaviour
{
    /// <summary>D1「得走近一点」的冷却时间：避免边走边被念。</summary>
    public float d1CooldownSeconds = 30f;

    private GameText text;
    private GameSession session;
    private float lastD1Time = -999f;

    private void OnEnable()
    {
        EventBus.Subscribe<ItemTouchedEvent>(OnItemTouched);
        EventBus.Subscribe<ItemPromptEvent>(OnItemPrompt);
        EventBus.Subscribe<ForceTouchWarnedEvent>(OnForceTouchWarned);
        EventBus.Subscribe<ItemTakenEvent>(OnItemTaken);
        EventBus.Subscribe<NpcDoorEvent>(OnNpcDoor);
        EventBus.Subscribe<RoundEndedEvent>(OnRoundEnded);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ItemTouchedEvent>(OnItemTouched);
        EventBus.Unsubscribe<ItemPromptEvent>(OnItemPrompt);
        EventBus.Unsubscribe<ForceTouchWarnedEvent>(OnForceTouchWarned);
        EventBus.Unsubscribe<ItemTakenEvent>(OnItemTaken);
        EventBus.Unsubscribe<NpcDoorEvent>(OnNpcDoor);
        EventBus.Unsubscribe<RoundEndedEvent>(OnRoundEnded);
    }

    private GameText Text
    {
        get
        {
            if (text == null && GameRoot.Instance != null) text = GameRoot.Instance.Text;
            return text;
        }
    }

    private GameSession Session
    {
        get
        {
            if (session == null && GameRoot.Instance != null) session = GameRoot.Instance.Session;
            return session;
        }
    }

    /// <summary>对外发一句话（PlayerController 的兜底提示也走这里）。</summary>
    public void ShowLine(string line, NarrationPriority priority, float holdSeconds)
    {
        if (string.IsNullOrEmpty(line)) return;
        EventBus.Publish(new NarrationEvent
        {
            text = line,
            priority = priority,
            holdSeconds = holdSeconds
        });
    }

    // ------------------------------------------------------------------
    // 教学 D：玩家卡住时的兜底
    // ------------------------------------------------------------------

    /// <summary>D1：区域里有物品，但都不在触碰范围内。带 30 秒冷却。</summary>
    public void ShowD1()
    {
        if (Time.unscaledTime - lastD1Time < d1CooldownSeconds) return;
        lastD1Time = Time.unscaledTime;
        ShowLine(Text != null ? Text.tutorialD1 : null, NarrationPriority.Tutorial, 2f);
    }

    /// <summary>D2：对着门按触碰。</summary>
    public void ShowD2()
    {
        ShowLine(Text != null ? Text.tutorialD2 : null, NarrationPriority.Tutorial, 2f);
    }

    /// <summary>D3：这一片没什么可碰的。</summary>
    public void ShowD3()
    {
        ShowLine(Text != null ? Text.tutorialD3 : null, NarrationPriority.Tutorial, 2f);
    }

    // ------------------------------------------------------------------
    // 事件订阅
    // ------------------------------------------------------------------

    private void OnItemTouched(ItemTouchedEvent evt)
    {
        GameText source = Text;
        if (source == null) return;

        // B：第一次触碰先讲玩法，讲完再补这件物品的独白（只出一次）
        GameSession current = Session;
        if (current != null && !current.tutorialFirstTouchDone)
        {
            current.tutorialFirstTouchDone = true;
            StartCoroutine(FirstTouchRoutine(evt.itemId));
            return;
        }

        // 物品独白：每次触碰都播（按 itemId）
        ShowLine(source.ItemMonologueFor(evt.itemId), NarrationPriority.Monologue, 2.5f);
    }

    private IEnumerator FirstTouchRoutine(string itemId)
    {
        GameText source = Text;
        if (source == null) yield break;

        ShowLine(source.tutorialB1, NarrationPriority.Tutorial, 2f);
        yield return new WaitForSecondsRealtime(2f);

        ShowLine(source.tutorialB2, NarrationPriority.Tutorial, 2.5f);
        yield return new WaitForSecondsRealtime(2.5f);

        ShowLine(source.ItemMonologueFor(itemId), NarrationPriority.Monologue, 2.5f);
    }

    private void OnItemPrompt(ItemPromptEvent evt)
    {
        GameText source = Text;
        if (source == null) return;

        switch (evt.status)
        {
            case ItemActionStatus.AlreadyTouched:
                ShowLine(source.alreadyTouched, NarrationPriority.Prompt, 2f);
                break;

            case ItemActionStatus.ForceTouchAccepted:
            {
                ItemType type = ItemType.Unknown;
                GameSession current = Session;
                if (current != null)
                {
                    ItemData item = current.GetItem(evt.itemId);
                    if (item != null) type = item.type;
                }
                ShowLine(source.ForceTouchAcceptedLine(type), NarrationPriority.Prompt, 2f);
                break;
            }

            case ItemActionStatus.PlacementBlocked:
            {
                ItemType type = ItemType.Unknown;
                GameSession current = Session;
                if (current != null)
                {
                    ItemData item = current.GetItem(evt.itemId);
                    if (item != null) type = item.type;
                }
                ShowLine(source.PlacementBlocked(type, evt.message), NarrationPriority.Prompt, 2.5f);
                break;
            }

            case ItemActionStatus.Success:
                // 成功触碰不刷屏（微光已经说明了）
                break;

            default:
                // 其它状态（不在场景里 / 本轮碰过了 / 找不到…）沿用物品系统给的文案
                ShowLine(evt.message, NarrationPriority.Prompt, 2f);
                break;
        }
    }

    private void OnForceTouchWarned(ForceTouchWarnedEvent evt)
    {
        GameText source = Text;
        GameSession current = Session;
        if (source == null || current == null) return;

        int used = Mathf.Max(1, current.forceTouchWarningsUsed);
        ShowLine(source.ForceTouchWarning(used), NarrationPriority.Prompt, 2.5f);
    }

    private void OnItemTaken(ItemTakenEvent evt)
    {
        GameSession current = Session;
        if (current == null || current.tutorialFirstTakenDone) return;

        current.tutorialFirstTakenDone = true;
        ShowLine(Text != null ? Text.tutorialC2 : null, NarrationPriority.Tutorial, 2f);
    }

    private void OnNpcDoor(NpcDoorEvent evt)
    {
        if (!evt.opening) return;

        GameSession current = Session;
        if (current == null || current.tutorialFirstOutDone) return;

        current.tutorialFirstOutDone = true;
        ShowLine(Text != null ? Text.tutorialC1 : null, NarrationPriority.Tutorial, 2f);
    }

    private void OnRoundEnded(RoundEndedEvent evt)
    {
        if (evt.round != 1) return;

        GameSession current = Session;
        if (current == null || current.tutorialFirstSettleDone) return;

        current.tutorialFirstSettleDone = true;
        ShowLine(Text != null ? Text.tutorialC3 : null, NarrationPriority.Tutorial, 3f);
    }
}
