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

        // 顺手把暂停界面存一张图到 Logs/，方便人眼确认"背景就是游戏画面"。
        ScreenCapture.CaptureScreenshot("Logs/pause-screen-preview.png");
        yield return new WaitForEndOfFrame();
        yield return null;
    }
}
