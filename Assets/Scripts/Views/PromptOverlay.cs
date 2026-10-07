using UnityEngine;

/// <summary>
/// 极简提示层：订阅物品提示 / 强碰警告，把文字直接画在屏幕下方。
/// 不需要 Canvas / Prefab，正式的 UI 接进来之后可以删掉这个组件。
/// </summary>
[DefaultExecutionOrder(1000)]
public class PromptOverlay : MonoBehaviour
{
    public float holdSeconds = 2f;

    private string message;
    private float hideAt;
    private GUIStyle style;
    private GUIStyle hintStyle;
    private bool interactAvailable;

    private void OnEnable()
    {
        EventBus.Subscribe<ItemPromptEvent>(OnItemPrompt);
        EventBus.Subscribe<ForceTouchWarnedEvent>(OnForceTouchWarned);
        EventBus.Subscribe<InteractHintEvent>(OnInteractHint);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ItemPromptEvent>(OnItemPrompt);
        EventBus.Unsubscribe<ForceTouchWarnedEvent>(OnForceTouchWarned);
        EventBus.Unsubscribe<InteractHintEvent>(OnInteractHint);
    }

    private void OnInteractHint(InteractHintEvent evt)
    {
        interactAvailable = evt.available;
    }

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

        float width = Mathf.Min(Screen.width - 40f, 720f);
        Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 130f, width, 64f);

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
}
