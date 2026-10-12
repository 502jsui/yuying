using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 真机截图那条路（需要真显卡）：批处理 / -nographics 下会被跳过。
/// 想手动验证就带图形跑一次：
///   Unity.exe -projectPath &lt;项目&gt; -runTests -testPlatform PlayMode -testFilter PauseScreenshotTests
/// </summary>
public class PauseScreenshotTests
{
    [TearDown]
    public void TearDown()
    {
        if (GameRoot.Instance != null) Object.Destroy(GameRoot.Instance.gameObject);
        PauseSnapshot.Clear();
    }

    [UnityTest]
    public IEnumerator Pause_CapturesRealScreenshot_AndUsesItAsBackground()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Assert.Ignore("当前没有渲染设备（-nographics / 批处理），截图路径跳过。");
        }

        yield return null;

        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        AsyncOperation load = SceneManager.LoadSceneAsync("FirstFloor", LoadSceneMode.Single);
        while (load != null && !load.isDone) yield return null;
        yield return null;
        yield return null;

        float start = Time.realtimeSinceStartup;
        while (GameRoot.Instance.Flow.Phase != TurnPhase.PlayerInput && Time.realtimeSinceStartup - start < 10f)
        {
            yield return null;
        }

        PauseDirector director = PauseDirector.Instance;
        Assert.IsNotNull(director);
        Assert.IsTrue(director.CanPause, "玩家输入阶段应该允许暂停");

        director.PauseGame();

        start = Time.realtimeSinceStartup;
        while (SceneManager.GetActiveScene().name != "Pause" && Time.realtimeSinceStartup - start < 10f)
        {
            yield return null;
        }

        Assert.AreEqual("Pause", SceneManager.GetActiveScene().name);
        yield return null;

        Assert.IsNotNull(PauseSnapshot.Texture, "有显卡时应该真的截到一张图");
        Assert.Greater(PauseSnapshot.Texture.width, 16, "截图宽度应该像一张真实画面");
        Assert.Greater(PauseSnapshot.Texture.height, 16);

        PauseScreen screen = Object.FindObjectOfType<PauseScreen>();
        Assert.IsNotNull(screen);
        Assert.IsNotNull(screen.background.sprite, "背景应该就是那张截图生成的 sprite");
        Assert.AreEqual(PauseSnapshot.Texture.width, screen.background.sprite.texture.width);
        Assert.AreEqual(PauseSnapshot.Texture.height, screen.background.sprite.texture.height);
        Assert.AreEqual(Color.white, screen.background.color, "背景不能被染色，否则颜色就不是游戏里那样了");

        // 颜色必须和游戏里一致：把暂停场景按同一个相机渲染出来，和抓到的画面比平均色。
        Texture2D rendered = RenderPauseFrame(PauseSnapshot.Texture.width, PauseSnapshot.Texture.height);
        Assert.IsNotNull(rendered, "暂停场景应该能渲染出来");

        Color source = MeanColor(PauseSnapshot.Texture);
        Color shown = MeanColor(rendered);
        Assert.AreEqual(source.r, shown.r, 0.03f, "暂停背景整体色调要跟游戏一致（红）");
        Assert.AreEqual(source.g, shown.g, 0.03f, "暂停背景整体色调要跟游戏一致（绿）");
        Assert.AreEqual(source.b, shown.b, 0.03f, "暂停背景整体色调要跟游戏一致（蓝）");

        // 顺手把「抓到的画面」和「暂停场景实际画出来的样子」都存到 Logs/，方便人眼对比。
        WritePng(PauseSnapshot.Texture, "Logs/pause-source.png");
        WritePng(rendered, "Logs/pause-rendered.png");

        Object.Destroy(rendered);
    }

    /// <summary>按暂停场景自己的相机渲染一帧（用来和抓到的画面对比）。</summary>
    private static Texture2D RenderPauseFrame(int width, int height)
    {
        Camera camera = Camera.main;
        if (camera == null) return null;

        RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply();
            return texture;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static Color MeanColor(Texture2D texture)
    {
        Color[] pixels = texture.GetPixels();
        if (pixels.Length == 0) return Color.black;

        double r = 0d, g = 0d, b = 0d;
        for (int i = 0; i < pixels.Length; i++)
        {
            r += pixels[i].r;
            g += pixels[i].g;
            b += pixels[i].b;
        }
        float n = pixels.Length;
        return new Color((float)(r / n), (float)(g / n), (float)(b / n), 1f);
    }

    private static void WritePng(Texture2D texture, string path)
    {
        try
        {
            byte[] bytes = texture.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[暂停] 存对比图失败：" + exception.Message);
        }
    }
}
