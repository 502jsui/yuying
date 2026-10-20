using UnityEngine;

/// <summary>
/// 结算演出专用字幕层。它不读取也不修改现有 PromptOverlay / 暂停 HUD，
/// 只在 EndingDirector 播放结算时绘制黑边、压暗、时间卡和字幕。
/// </summary>
[DefaultExecutionOrder(1100)]
public class EndingSubtitleOverlay : MonoBehaviour
{
    public bool visible;
    public string speaker;
    public string line;
    public float subtitleAlpha;
    public float darkness;
    public float blackAlpha;
    public float whiteAlpha;
    public string timeCard;
    public float timeCardAlpha;
    public string finalTitle;
    public float finalTitleAlpha;

    private GUIStyle subtitleStyle;
    private GUIStyle speakerStyle;
    private GUIStyle cardStyle;
    private GUIStyle titleStyle;
    private GUIStyle endStyle;
    private Texture2D whiteTexture;

    private void Awake()
    {
        whiteTexture = Texture2D.whiteTexture;
    }

    private void OnGUI()
    {
        if (!visible) return;

        GUI.depth = -100;
        DrawFullScreen(new Color(0f, 0f, 0f, Mathf.Clamp01(darkness)));
        DrawLetterbox();
        if (whiteAlpha > 0.001f) DrawFullScreen(new Color(0.94f, 0.96f, 0.97f, Mathf.Clamp01(whiteAlpha)));
        if (blackAlpha > 0.001f) DrawFullScreen(new Color(0f, 0f, 0f, Mathf.Clamp01(blackAlpha)));
        DrawTimeCard();
        DrawSubtitle();
        DrawFinalTitle();
    }

    private void DrawLetterbox()
    {
        float barHeight = Screen.height * 0.12f;
        DrawRect(new Rect(0f, 0f, Screen.width, barHeight), new Color(0f, 0f, 0f, 0.88f));
        DrawRect(new Rect(0f, Screen.height - barHeight, Screen.width, barHeight), new Color(0f, 0f, 0f, 0.88f));
    }

    private void DrawSubtitle()
    {
        if (string.IsNullOrEmpty(line) || subtitleAlpha <= 0.001f) return;

        EnsureSubtitleStyles();
        float width = Mathf.Min(Screen.width - 80f, 1180f);
        float left = (Screen.width - width) * 0.5f;
        float speakerTop = Screen.height * 0.79f;
        float textTop = speakerTop + Mathf.Max(28f, Screen.height * 0.032f);

        if (!string.IsNullOrEmpty(speaker))
        {
            speakerStyle.normal.textColor = SpeakerColor(speaker, subtitleAlpha);
            GUI.Label(new Rect(left, speakerTop, width, 30f), speaker, speakerStyle);
        }

        subtitleStyle.normal.textColor = new Color(0.96f, 0.97f, 0.98f, subtitleAlpha);
        GUI.Label(new Rect(left, textTop, width, Screen.height * 0.12f), line, subtitleStyle);
    }

    private void DrawTimeCard()
    {
        if (string.IsNullOrEmpty(timeCard) || timeCardAlpha <= 0.001f) return;

        EnsureCardStyle();
        cardStyle.normal.textColor = new Color(0.88f, 0.9f, 0.92f, timeCardAlpha);
        GUI.Label(new Rect(0f, Screen.height * 0.42f, Screen.width, Screen.height * 0.16f), timeCard, cardStyle);
    }

    private void DrawFinalTitle()
    {
        if (string.IsNullOrEmpty(finalTitle) || finalTitleAlpha <= 0.001f) return;

        EnsureTitleStyles();
        float a = Mathf.Clamp01(finalTitleAlpha);
        DrawFullScreen(new Color(0.015f, 0.02f, 0.024f, a));

        endStyle.normal.textColor = new Color(0.6f, 0.62f, 0.64f, a);
        titleStyle.normal.textColor = new Color(0.94f, 0.95f, 0.95f, a);
        GUI.Label(new Rect(0f, Screen.height * 0.39f, Screen.width, 42f), "终", endStyle);
        GUI.Label(new Rect(0f, Screen.height * 0.46f, Screen.width, 90f), finalTitle, titleStyle);
    }

    private void EnsureSubtitleStyles()
    {
        if (subtitleStyle == null)
        {
            subtitleStyle = new GUIStyle(GUI.skin.label);
            subtitleStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.026f, 22f, 34f));
            subtitleStyle.alignment = TextAnchor.UpperCenter;
            subtitleStyle.wordWrap = true;
            subtitleStyle.richText = false;
        }

        if (speakerStyle == null)
        {
            speakerStyle = new GUIStyle(GUI.skin.label);
            speakerStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.019f, 17f, 24f));
            speakerStyle.alignment = TextAnchor.MiddleCenter;
            speakerStyle.fontStyle = FontStyle.Bold;
        }
    }

    private void EnsureCardStyle()
    {
        if (cardStyle != null) return;
        cardStyle = new GUIStyle(GUI.skin.label);
        cardStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.042f, 30f, 54f));
        cardStyle.alignment = TextAnchor.MiddleCenter;
    }

    private void EnsureTitleStyles()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.052f, 36f, 66f));
            titleStyle.alignment = TextAnchor.MiddleCenter;
        }

        if (endStyle == null)
        {
            endStyle = new GUIStyle(GUI.skin.label);
            endStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.024f, 20f, 30f));
            endStyle.alignment = TextAnchor.MiddleCenter;
        }
    }

    private void DrawFullScreen(Color color)
    {
        if (whiteTexture == null) whiteTexture = Texture2D.whiteTexture;
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), whiteTexture);
        GUI.color = previous;
    }

    private void DrawRect(Rect rect, Color color)
    {
        if (whiteTexture == null) whiteTexture = Texture2D.whiteTexture;
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, whiteTexture);
        GUI.color = previous;
    }

    private static Color SpeakerColor(string name, float alpha)
    {
        if (name == "旁白") return new Color(0.72f, 0.75f, 0.78f, alpha);
        if (name == "母亲的声" || name == "母亲") return new Color(1f, 0.84f, 0.62f, alpha);
        if (name == "女孩") return new Color(0.82f, 0.9f, 1f, alpha);
        if (name == "主人公") return new Color(0.86f, 0.92f, 0.96f, alpha);
        if (name == "老婆婆") return new Color(1f, 0.88f, 0.68f, alpha);
        if (name == "大叔") return new Color(0.86f, 0.82f, 0.74f, alpha);
        return new Color(0.9f, 0.92f, 0.94f, alpha);
    }
}
