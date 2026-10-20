using NUnit.Framework;
using UnityEngine;

public class PromptOverlayTests
{
    [Test]
    public void GuiScale_UsesReferenceHeightAndClamps()
    {
        Assert.AreEqual(1f, PromptOverlay.ResolveGuiScale(720), 0.001f);
        Assert.AreEqual(1.5f, PromptOverlay.ResolveGuiScale(1080), 0.001f);
        Assert.AreEqual(3f, PromptOverlay.ResolveGuiScale(2160), 0.001f);
        Assert.AreEqual(PromptOverlay.MinGuiScale, PromptOverlay.ResolveGuiScale(120), 0.001f);
        Assert.AreEqual(PromptOverlay.MaxGuiScale, PromptOverlay.ResolveGuiScale(10000), 0.001f);
    }

    [Test]
    public void FontSizes_IncreaseWithNativeResolution()
    {
        int hd = PromptOverlay.ResolveMessageFontSize(720);
        int fullHd = PromptOverlay.ResolveMessageFontSize(1080);
        int fourK = PromptOverlay.ResolveMessageFontSize(2160);

        Assert.Greater(fullHd, hd);
        Assert.Greater(fourK, fullHd);
        Assert.Greater(PromptOverlay.ResolveHintFontSize(1080), PromptOverlay.ResolveHintFontSize(720));
    }

    [Test]
    public void MessageAndHintRects_StayInsideCommonResolutions()
    {
        Vector2Int[] resolutions =
        {
            new Vector2Int(960, 600),
            new Vector2Int(1280, 720),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440),
            new Vector2Int(3840, 2160)
        };

        for (int i = 0; i < resolutions.Length; i++)
        {
            int width = resolutions[i].x;
            int height = resolutions[i].y;
            Rect message = PromptOverlay.ResolveMessageRect(width, height, 0f);
            Rect hint = PromptOverlay.ResolveHintRect(width, height);

            Assert.Greater(message.width, 0f);
            Assert.Greater(message.height, 0f);
            Assert.GreaterOrEqual(message.xMin, 0f);
            Assert.GreaterOrEqual(message.yMin, 0f);
            Assert.LessOrEqual(message.xMax, width);
            Assert.LessOrEqual(message.yMax, height);

            Assert.AreEqual(width, hint.width, 0.001f);
            Assert.GreaterOrEqual(hint.yMin, 0f);
            Assert.LessOrEqual(hint.yMax, height);
        }
    }

    [Test]
    public void LongNarration_GrowsUpwardWithoutLeavingScreen()
    {
        Rect normal = PromptOverlay.ResolveMessageRect(1920, 1080, 0f);
        Rect longText = PromptOverlay.ResolveMessageRect(1920, 1080, 120f);

        Assert.Greater(longText.height, normal.height);
        Assert.Less(longText.y, normal.y);
        Assert.GreaterOrEqual(longText.yMin, 0f);
        Assert.LessOrEqual(longText.yMax, 1080f);
    }
}
