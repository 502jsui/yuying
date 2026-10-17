using System.Collections;
using UnityEngine;

/// <summary>
/// 开场序列：6 条醒来独白 → A1 → A2（提示到玩家第一次移动）→ A3（高亮最近的物品）。
/// 独白期间镜头推近主角；独白结束、A1 之前镜头回到单场景取景。
/// 独白 + A1 期间阻断移动；Space 一句一按，跳过当前句。
/// </summary>
[DefaultExecutionOrder(-790)]
public class IntroNarration : MonoBehaviour
{
    [Tooltip("每条独白 / A1 停留的时间（秒）。")]
    public float lineSeconds = 2.5f;

    [Tooltip("A3 高亮最近物品的时长（秒）。")]
    public float highlightSeconds = 1f;

    [Header("独白推进")]
    [Tooltip("一句一按：按一下结束当前独白句，进入下一句。")]
    public KeyCode skipKey = KeyCode.Space;

    [Header("开场镜头")]
    [Tooltip("先切到的中景尺寸，再慢慢推到特写尺寸。")]
    public float focusFromSize = 3f;
    [Tooltip("开场特写的正交尺寸（越小越近）。")]
    public float focusSize = 2f;
    [Tooltip("从 focusFromSize 推到 focusSize 的时长。")]
    public float focusDuration = 2f;
    [Tooltip("独白结束、回到单场景取景的时长。")]
    public float returnDuration = 0.8f;

    private Coroutine routine;
    private bool skipRequested;
    private bool advanceRequested;
    private bool cameraReturned;
    private SceneCameraFollow2D cameraFraming;
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

    private void Update()
    {
        if (routine == null || IsPaused) return;
        if (Input.GetKeyDown(skipKey)) RequestAdvance();
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

    /// <summary>整段开场直接结束（测试 / 调试用）。</summary>
    public void RequestSkip()
    {
        skipRequested = true;
    }

    /// <summary>Space 一句一按：结束当前句，进入下一句。</summary>
    public void RequestAdvance()
    {
        advanceRequested = true;
    }

    private void OnStateChanged(GameStateChangedEvent evt)
    {
        if (evt.current != GameState.Playing) return;
        if (routine != null) return;

        GameSession current = Session;
        if (current == null || current.openingDone) return;

        skipRequested = false;
        advanceRequested = false;
        cameraReturned = false;
        routine = StartCoroutine(RunIntro());
    }

    private IEnumerator RunIntro()
    {
        NarrationState.SetIntroPlaying(true);
        NarrationState.SetBlockingInput(true);

        GameText source = Text;
        Transform player = FindPlayer();

        FocusOnPlayer(player);

        // 6 条醒来独白
        int count = source != null && source.openingMonologue != null ? source.openingMonologue.Length : 0;
        for (int i = 0; i < count; i++)
        {
            Publish(source.OpeningLine(i), NarrationPriority.Monologue, lineSeconds);
            yield return WaitOrAdvance(lineSeconds);

            if (skipRequested) break;
            if (advanceRequested)
            {
                advanceRequested = false;
                ClearLine(NarrationPriority.Monologue);
            }
        }

        // 独白结束（或整段跳过）：先把镜头拉回单场景，再进 A1。
        ReturnCameraToScene();

        // A1
        if (!skipRequested)
        {
            Publish(source != null ? source.tutorialA1 : null, NarrationPriority.Tutorial, lineSeconds);
            yield return WaitSeconds(lineSeconds);
        }

        NarrationState.SetBlockingInput(false);

        if (!skipRequested)
        {
            // A2：一直显示，直到玩家第一次移动
            Publish(source != null ? source.tutorialA2 : null, NarrationPriority.Tutorial, 0f);

            float startX = player != null ? player.position.x : 0f;
            while (!FirstMoveDetected(player, startX) && !skipRequested)
            {
                yield return null;
            }

            // A3：高亮最近的物品
            Publish(source != null ? source.tutorialA3 : null, NarrationPriority.Tutorial, highlightSeconds + 1f);
            yield return HighlightNearestItem();
        }

        Finish();
    }

    private IEnumerator WaitOrAdvance(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < Mathf.Max(0f, seconds))
        {
            if (skipRequested) yield break;
            if (IsPaused) { yield return null; continue; }
            if (advanceRequested) yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitSeconds(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < Mathf.Max(0f, seconds))
        {
            if (skipRequested) yield break;
            if (IsPaused) { yield return null; continue; }

            elapsed += Time.unscaledDeltaTime;
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

    private void FocusOnPlayer(Transform player)
    {
        if (player == null) return;

        Camera camera = Camera.main;
        if (camera == null) return;

        cameraFraming = camera.GetComponent<SceneCameraFollow2D>();
        if (cameraFraming == null) return;

        Vector3 center = player.position + Vector3.up * 1.1f;
        cameraFraming.FocusOn(center, focusFromSize, focusSize, focusDuration);
        cameraReturned = false;
    }

    private void ReturnCameraToScene()
    {
        if (cameraReturned) return;
        cameraReturned = true;

        if (cameraFraming != null)
        {
            cameraFraming.AnimateToSceneFraming(returnDuration);
        }
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

    private static void ClearLine(NarrationPriority priority)
    {
        EventBus.Publish(new NarrationClearEvent { priority = priority });
    }

    private static bool IsPaused
    {
        get
        {
            GameRoot root = GameRoot.Instance;
            return root != null && root.Flow != null && root.Flow.State == GameState.Paused;
        }
    }

    private void Finish()
    {
        ReturnCameraToScene();
        advanceRequested = false;

        GameSession current = Session;
        if (current != null) current.openingDone = true;

        NarrationState.SetBlockingInput(false);
        NarrationState.SetIntroPlaying(false);
        routine = null;
    }
}
