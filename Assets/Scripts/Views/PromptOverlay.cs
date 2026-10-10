using UnityEngine;

/// <summary>
<<<<<<< HEAD
/// 统一提示通道：屏幕下三分之一的文字条（2 行内）。
/// 旁白（教学 / 独白）和拒绝类提示都走这里，按优先级决定谁顶掉谁：
///   拒绝提示(2) &gt; 教学(1) &gt; 物品独白(0)
/// 低优先级的句子不会打断还在显示的高优先级句子。
=======
/// 极简提示层：订阅物品提示 / 强碰警告，把文字直接画在屏幕下方。
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
/// 不需要 Canvas / Prefab，正式的 UI 接进来之后可以删掉这个组件。
/// </summary>
[DefaultExecutionOrder(1000)]
public class PromptOverlay : MonoBehaviour
{
<<<<<<< HEAD
    [Tooltip("holdSeconds <= 0 的句子（比如 A2）没有时长，一直显示到被下一条替换。")]
    public float defaultHoldSeconds = 2f;

    private string message;
    private float hideAt;          // > 0 = 到点消失；<= 0 = 一直显示
    private NarrationPriority messagePriority;
=======
    public float holdSeconds = 2f;

    private string message;
    private float hideAt;
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
    private GUIStyle style;
    private GUIStyle hintStyle;
    private bool interactAvailable;

<<<<<<< HEAD
    public string CurrentMessage { get { return message; } }
    public NarrationPriority CurrentPriority { get { return messagePriority; } }

    private void OnEnable()
    {
        EventBus.Subscribe<NarrationEvent>(OnNarration);
=======
    private void OnEnable()
    {
        EventBus.Subscribe<ItemPromptEvent>(OnItemPrompt);
        EventBus.Subscribe<ForceTouchWarnedEvent>(OnForceTouchWarned);
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
        EventBus.Subscribe<InteractHintEvent>(OnInteractHint);
    }

    private void OnDisable()
    {
<<<<<<< HEAD
        EventBus.Unsubscribe<NarrationEvent>(OnNarration);
=======
        EventBus.Unsubscribe<ItemPromptEvent>(OnItemPrompt);
        EventBus.Unsubscribe<ForceTouchWarnedEvent>(OnForceTouchWarned);
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
        EventBus.Unsubscribe<InteractHintEvent>(OnInteractHint);
    }

    private void OnInteractHint(InteractHintEvent evt)
    {
        interactAvailable = evt.available;
    }

<<<<<<< HEAD
    private void OnNarration(NarrationEvent evt)
    {
        if (string.IsNullOrEmpty(evt.text)) return;

        // 还在显示的高优先级句子，不被低优先级顶掉
        bool alive = !string.IsNullOrEmpty(message) && (hideAt <= 0f || Time.unscaledTime < hideAt);
        if (alive && evt.priority < messagePriority) return;

        message = evt.text;
        messagePriority = evt.priority;
        hideAt = evt.holdSeconds <= 0f ? 0f : Time.unscaledTime + evt.holdSeconds;

        Debug.Log("[提示] " + message);
    }

    private void Update()
    {
        if (string.IsNullOrEmpty(message)) return;
        if (hideAt > 0f && Time.unscaledTime >= hideAt) message = null;
=======
    private void Update()
    {
        if (!string.IsNullOrEmpty(message) && Time.unscaledTime >= hideAt) message = null;
    }

    private void OnItemPrompt(ItemPromptEvent evt)
    {
        // 成功触碰（微光）不用刷屏，只提示被拒绝 / 有交代的情况。
        if (evt.status == ItemActionStatus.Success) return;
        if (string.IsNullOrEmpty(evt.message)) return;
        Show(evt.message);
    }

    private void OnForceTouchWarned(ForceTouchWarnedEvent evt)
    {
        string text = "他还在那边，你现在碰不到。";
        if (evt.warningsRemaining > 0) text += "（还剩 " + evt.warningsRemaining + " 次警告）";
        Show(text);
    }

    private void Show(string text)
    {
        message = text;
        hideAt = Time.unscaledTime + holdSeconds;
        Debug.Log("[提示] " + text);
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
    }

    private void OnGUI()
    {
        DrawInteractHint();

        if (string.IsNullOrEmpty(message)) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 22;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.white;
            style.wordWrap = true;
        }

<<<<<<< HEAD
        float width = Mathf.Min(Screen.width - 40f, 760f);
        Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 150f, width, 78f);
=======
        float width = Mathf.Min(Screen.width - 40f, 720f);
        Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 130f, width, 64f);
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(rect, message, style);
    }

    private void DrawInteractHint()
    {
        if (!interactAvailable) return;

        if (hintStyle == null)
        {
            hintStyle = new GUIStyle(GUI.skin.label);
            hintStyle.fontSize = 18;
            hintStyle.alignment = TextAnchor.MiddleCenter;
            hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        }

        Rect rect = new Rect(0f, Screen.height - 60f, Screen.width, 30f);
        GUI.Label(rect, "按 E 触碰", hintStyle);
    }
<<<<<<< HEAD
=======

>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
}
