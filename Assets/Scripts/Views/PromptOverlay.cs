using UnityEngine;

/// <summary>
/// 统一提示通道：屏幕下三分之一的文字条（2 行内）。
/// 旁白（教学 / 独白）和拒绝类提示都走这里，按优先级决定谁顶掉谁：
///   拒绝提示(2) &gt; 教学(1) &gt; 物品独白(0)
/// 低优先级的句子不会打断还在显示的高优先级句子。
/// IMGUI 层按屏幕高度缩放，避免打包成全屏原生分辨率后文字显得过小。
/// </summary>
[DefaultExecutionOrder(1000)]
public class PromptOverlay : MonoBehaviour
{
    public const float ReferenceScreenHeight = 720f;
    public const float MinGuiScale = 1f;
    public const float MaxGuiScale = 4f;

    private const float MessageFontSize = 22f;
    private const float HintFontSize = 18f;
    private const float MessageMaxWidth = 760f;
    private const float MessageMinHeight = 78f;
    private const float MessageBottomMargin = 72f;
    private const float MessagePadding = 10f;
    private const float HintHeight = 30f;
    private const float HintBottomMargin = 30f;

    private string message;
    private float hideAt;          // > 0 = 到点消失；<= 0 = 一直显示
    private NarrationPriority messagePriority;
    private GUIStyle style;
    private GUIStyle hintStyle;
    private bool interactAvailable;
    private string interactText;
    private bool portalAvailable;
    private string portalText;

    public string CurrentMessage { get { return message; } }
    public NarrationPriority CurrentPriority { get { return messagePriority; } }

    private void OnEnable()
    {
        EventBus.Subscribe<NarrationEvent>(OnNarration);
        EventBus.Subscribe<NarrationClearEvent>(OnNarrationClear);
        EventBus.Subscribe<InteractHintEvent>(OnInteractHint);
        EventBus.Subscribe<PortalHintEvent>(OnPortalHint);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<NarrationEvent>(OnNarration);
        EventBus.Unsubscribe<NarrationClearEvent>(OnNarrationClear);
        EventBus.Unsubscribe<InteractHintEvent>(OnInteractHint);
        EventBus.Unsubscribe<PortalHintEvent>(OnPortalHint);
    }

    private void OnInteractHint(InteractHintEvent evt)
    {
        interactAvailable = evt.available;
        interactText = evt.text;
    }

    private void OnPortalHint(PortalHintEvent evt)
    {
        portalAvailable = evt.available;
        portalText = evt.text;
    }

    private void OnNarrationClear(NarrationClearEvent evt)
    {
        if (string.IsNullOrEmpty(message)) return;
        if (messagePriority > evt.priority) return;
        message = null;
        messagePriority = NarrationPriority.Monologue;
    }

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
        // 暂停界面里不要继续倒计时，回来时那句话还在。
        if (IsPaused) return;
        if (string.IsNullOrEmpty(message)) return;
        if (hideAt > 0f && Time.unscaledTime >= hideAt) message = null;
    }

    private void OnGUI()
    {
        // 暂停场景自己有背景和 UI，游戏里的提示条不要再叠上去（会重影）。
        if (IsPaused) return;

        DrawInteractHint();

        if (string.IsNullOrEmpty(message)) return;

        float scale = ResolveGuiScale(Screen.height);
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.white;
            style.wordWrap = true;
        }
        style.fontSize = ResolveMessageFontSize(Screen.height);

        float padding = MessagePadding * scale;
        Rect baseRect = ResolveMessageRect(Screen.width, Screen.height, 0f);
        float availableWidth = Mathf.Max(1f, baseRect.width - padding * 2f);
        float textHeight = style.CalcHeight(new GUIContent(message), availableWidth);
        Rect rect = ResolveMessageRect(Screen.width, Screen.height, textHeight);
        Rect textRect = new Rect(
            rect.x + padding,
            rect.y + padding,
            Mathf.Max(1f, rect.width - padding * 2f),
            Mathf.Max(1f, rect.height - padding * 2f));

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(textRect, message, style);
    }

    private static bool IsPaused
    {
        get
        {
            GameRoot root = GameRoot.Instance;
            return root != null && root.Flow != null && root.Flow.State == GameState.Paused;
        }
    }

    private void DrawInteractHint()
    {
        if (!interactAvailable && !portalAvailable) return;

        if (hintStyle == null)
        {
            hintStyle = new GUIStyle(GUI.skin.label);
            hintStyle.alignment = TextAnchor.MiddleCenter;
            hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        }
        hintStyle.fontSize = ResolveHintFontSize(Screen.height);

        string text = "";
        if (interactAvailable && !string.IsNullOrEmpty(interactText)) text = interactText;
        if (portalAvailable && !string.IsNullOrEmpty(portalText))
        {
            text = string.IsNullOrEmpty(text) ? portalText : text + "　·　" + portalText;
        }
        if (string.IsNullOrEmpty(text)) return;

        Rect rect = ResolveHintRect(Screen.width, Screen.height);
        GUI.Label(rect, text, hintStyle);
    }

    public static float ResolveGuiScale(int screenHeight)
    {
        if (screenHeight <= 0) return MinGuiScale;
        return Mathf.Clamp(screenHeight / ReferenceScreenHeight, MinGuiScale, MaxGuiScale);
    }

    public static int ResolveMessageFontSize(int screenHeight)
    {
        return Mathf.Max(12, Mathf.RoundToInt(MessageFontSize * ResolveGuiScale(screenHeight)));
    }

    public static int ResolveHintFontSize(int screenHeight)
    {
        return Mathf.Max(10, Mathf.RoundToInt(HintFontSize * ResolveGuiScale(screenHeight)));
    }

    public static Rect ResolveMessageRect(int screenWidth, int screenHeight, float contentHeight)
    {
        float scale = ResolveGuiScale(screenHeight);
        float sideMargin = 20f * scale;
        float width = Mathf.Max(1f, Mathf.Min(screenWidth - sideMargin * 2f, MessageMaxWidth * scale));
        float padding = MessagePadding * scale;
        float height = Mathf.Max(MessageMinHeight * scale, Mathf.Max(0f, contentHeight) + padding * 2f);
        height = Mathf.Min(height, Mathf.Max(MessageMinHeight * scale, screenHeight - sideMargin * 2f));
        float bottomMargin = MessageBottomMargin * scale;
        float y = Mathf.Max(sideMargin, screenHeight - bottomMargin - height);
        return new Rect((screenWidth - width) * 0.5f, y, width, height);
    }

    public static Rect ResolveHintRect(int screenWidth, int screenHeight)
    {
        float scale = ResolveGuiScale(screenHeight);
        float height = HintHeight * scale;
        return new Rect(0f, screenHeight - HintBottomMargin * scale - height, screenWidth, height);
    }
}
