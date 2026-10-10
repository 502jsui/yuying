using System.Collections;
using UnityEngine;

/// <summary>
/// 开场序列：6 条醒来独白 → A1 → A2（提示到玩家第一次移动）→ A3（高亮最近的物品）。
/// 独白 + A1 期间阻断移动，按任意键可跳过；A3 只做高亮，不推镜头（相机是固定取景）。
/// </summary>
[DefaultExecutionOrder(-790)]
public class IntroNarration : MonoBehaviour
{
    [Tooltip("每条独白 / A1 停留的时间（秒）。")]
    public float lineSeconds = 2.5f;

    [Tooltip("A3 高亮最近物品的时长（秒）。")]
    public float highlightSeconds = 1f;

    private Coroutine routine;
    private bool skipRequested;
    private GameText text;
    private GameSession session;

    public bool IsRunning { get { return routine != null; } }

    private void OnEnable()
    {
        EventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
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

    /// <summary>跳过开场（测试 / 按键都走这里）。</summary>
    public void RequestSkip()
    {
        skipRequested = true;
    }

    private void OnStateChanged(GameStateChangedEvent evt)
    {
        if (evt.current != GameState.Playing) return;
        if (routine != null) return;

        GameSession current = Session;
        if (current == null || current.openingDone) return;

        skipRequested = false;
        routine = StartCoroutine(RunIntro());
    }

    private IEnumerator RunIntro()
    {
        NarrationState.SetIntroPlaying(true);
        NarrationState.SetBlockingInput(true);

        GameText source = Text;

        // 6 条醒来独白
        int count = source != null && source.openingMonologue != null ? source.openingMonologue.Length : 0;
        for (int i = 0; i < count; i++)
        {
            Publish(source.OpeningLine(i), NarrationPriority.Monologue, lineSeconds);
            yield return WaitOrSkip(lineSeconds);
            if (skipRequested) break;
        }

        // A1
        if (!skipRequested)
        {
            Publish(source != null ? source.tutorialA1 : null, NarrationPriority.Tutorial, lineSeconds);
            yield return WaitOrSkip(lineSeconds);
        }

        NarrationState.SetBlockingInput(false);

        if (!skipRequested)
        {
            // A2：一直显示，直到玩家第一次移动
            Publish(source != null ? source.tutorialA2 : null, NarrationPriority.Tutorial, 0f);

            Transform player = FindPlayer();
            float startX = player != null ? player.position.x : 0f;
            while (!FirstMoveDetected(player, startX) && !skipRequested) yield return null;

            // A3：高亮最近的物品
            Publish(source != null ? source.tutorialA3 : null, NarrationPriority.Tutorial, highlightSeconds + 1f);
            yield return HighlightNearestItem();
        }

        Finish();
    }

    private IEnumerator WaitOrSkip(float seconds)
    {
        float end = Time.unscaledTime + Mathf.Max(0f, seconds);
        while (Time.unscaledTime < end)
        {
            if (skipRequested || Input.anyKeyDown) { skipRequested = true; yield break; }
            yield return null;
        }
    }

    private bool FirstMoveDetected(Transform player, float startX)
    {
        if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f) return true;
        return player != null && Mathf.Abs(player.position.x - startX) > 0.01f;
    }

    private IEnumerator HighlightNearestItem()
    {
        ItemView target = FindNearestItem();
        if (target == null)
        {
            yield return new WaitForSeconds(highlightSeconds);
            yield break;
        }

        Vector3 original = target.transform.localScale;
        target.transform.localScale = original * 1.15f;
        yield return new WaitForSeconds(highlightSeconds);
        if (target != null) target.transform.localScale = original;
    }

    private Transform FindPlayer()
    {
        if (GameManager.Instance != null && GameManager.Instance.player != null)
        {
            return GameManager.Instance.player.transform;
        }
        PlayerController player = Object.FindObjectOfType<PlayerController>();
        return player != null ? player.transform : null;
    }

    private ItemView FindNearestItem()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || manager.itemViews == null) return null;

        Transform player = FindPlayer();
        float playerX = player != null ? player.position.x : 0f;

        ItemView best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < manager.itemViews.Length; i++)
        {
            ItemView view = manager.itemViews[i];
            if (view == null || !view.IsTouchable) continue;

            float distance = Mathf.Abs(view.transform.position.x - playerX);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = view;
            }
        }
        return best;
    }

    private void Publish(string line, NarrationPriority priority, float holdSeconds)
    {
        if (string.IsNullOrEmpty(line)) return;
        EventBus.Publish(new NarrationEvent
        {
            text = line,
            priority = priority,
            holdSeconds = holdSeconds
        });
    }

    private void Finish()
    {
        GameSession current = Session;
        if (current != null) current.openingDone = true;

        NarrationState.SetBlockingInput(false);
        NarrationState.SetIntroPlaying(false);
        routine = null;
    }
}
