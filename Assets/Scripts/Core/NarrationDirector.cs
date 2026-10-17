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

    [Header("独白推进")]
    [Tooltip("触碰旁白一句一按：按一下结束当前句，进入下一句。")]
    public KeyCode skipKey = KeyCode.Space;

    private GameText text;
    private GameSession session;
    private float lastD1Time = -999f;
    private Coroutine touchRoutine;
    private bool advanceRequested;

    private void Update()
    {
        if (touchRoutine == null || IsPaused) return;
        if (Input.GetKeyDown(skipKey)) SkipCurrentLine();
    }

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
        FinishTouchNarration();

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
        bool firstTouch = false;
        if (current != null && !current.tutorialFirstTouchDone)
        {
            current.tutorialFirstTouchDone = true;
            firstTouch = true;
        }

        StartTouchNarration(evt.itemId, firstTouch);
    }

    /// <summary>整段触碰旁白播完（或跳过）后放行；NPC 阶段靠这个等镜头拉远。</summary>
    public void FinishTouchNarration()
    {
        if (touchRoutine != null)
        {
            StopCoroutine(touchRoutine);
            touchRoutine = null;
        }

        advanceRequested = false;
        NarrationState.SetTouchNarrationPlaying(false);
    }

    /// <summary>Space：结束当前句，进入下一句。</summary>
    public void SkipCurrentLine()
    {
        if (touchRoutine == null) return;
        advanceRequested = true;
    }

    private void StartTouchNarration(string itemId, bool firstTouch)
    {
        if (touchRoutine != null) StopCoroutine(touchRoutine);

        advanceRequested = false;
        NarrationState.SetTouchNarrationPlaying(true);
        touchRoutine = StartCoroutine(TouchNarrationRoutine(itemId, firstTouch));
    }

    private IEnumerator TouchNarrationRoutine(string itemId, bool firstTouch)
    {
        GameText source = Text;

        if (firstTouch)
        {
            yield return ShowSkippable(source != null ? source.tutorialB1 : null, NarrationPriority.Tutorial, 2f);
            yield return ShowSkippable(source != null ? source.tutorialB2 : null, NarrationPriority.Tutorial, 2.5f);
        }

        yield return ShowSkippable(source != null ? source.ItemMonologueFor(itemId) : null, NarrationPriority.Monologue, 2.5f);

        CompleteTouchNarration();
    }

    private IEnumerator ShowSkippable(string line, NarrationPriority priority, float seconds)
    {
        if (string.IsNullOrEmpty(line)) yield break;

        ShowLine(line, priority, seconds);

        float elapsed = 0f;
        while (elapsed < Mathf.Max(0f, seconds))
        {
            if (!NarrationState.IsTouchNarrationPlaying) yield break;
            if (IsPaused) { yield return null; continue; }

            if (advanceRequested)
            {
                advanceRequested = false;
                EventBus.Publish(new NarrationClearEvent { priority = priority });
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void CompleteTouchNarration()
    {
        touchRoutine = null;
        advanceRequested = false;
        NarrationState.SetTouchNarrationPlaying(false);
    }

    private static bool IsPaused
    {
        get
        {
            GameRoot root = GameRoot.Instance;
            return root != null && root.Flow != null && root.Flow.State == GameState.Paused;
        }
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
