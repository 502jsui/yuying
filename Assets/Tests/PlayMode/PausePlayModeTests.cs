using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 暂停 = 跳到暂停场景（背景是暂停那一刻的截图），继续 = 回到原场景原位置、进度不丢。
/// </summary>
public class PausePlayModeTests
{
    [TearDown]
    public void TearDown()
    {
        if (GameRoot.Instance != null) Object.Destroy(GameRoot.Instance.gameObject);
        PauseSnapshot.Clear();
    }

    [UnityTest]
    public IEnumerator Pause_GoesToPauseScene_ThenResumeRestoresScene()
    {
        yield return null;
        yield return EnsureGameRoot();
        yield return LoadScene("FirstFloor");
        yield return WaitUntil(delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; }, 10f);

        int round = GameRoot.Instance.Session.round;
        PlayerController player = Object.FindObjectOfType<PlayerController>();
        Assert.IsNotNull(player, "一楼应该有主角");

        // 挪到一个有辨识度的位置，恢复后要一模一样。
        Vector3 moved = player.transform.position;
        moved.x = -3.25f;
        player.transform.position = moved;

        PauseDirector director = PauseDirector.Instance;
        Assert.IsNotNull(director, "GameRoot 上应该有 PauseDirector");
        Assert.IsTrue(director.CanPause, "玩家输入阶段应该允许暂停");

        director.PauseGame();

        yield return null;   // 等 WaitForEndOfFrame
        yield return null;
        yield return null;

        Assert.AreEqual("Pause", SceneManager.GetActiveScene().name, "暂停应该跳到暂停场景");
        Assert.AreEqual(GameState.Paused, GameRoot.Instance.Flow.State);
        Assert.AreEqual(0f, Time.timeScale, "暂停时游戏时间要冻住");
        Assert.IsNull(Object.FindObjectOfType<PlayerController>(), "游戏场景应该已经卸载");
        Assert.IsNotNull(Object.FindObjectOfType<PauseScreen>(), "暂停场景里要有 PauseScreen");
        Assert.IsTrue(director.IsPaused);

        director.ResumeGame();

        yield return null;
        yield return null;
        yield return null;

        Assert.AreEqual("FirstFloor", SceneManager.GetActiveScene().name, "继续应该回到原来的场景");
        Assert.AreEqual(GameState.Playing, GameRoot.Instance.Flow.State);
        Assert.AreEqual(1f, Time.timeScale, "继续要把时间放回来");
        Assert.AreEqual(round, GameRoot.Instance.Session.round, "暂停不应该推进轮次");

        PlayerController back = Object.FindObjectOfType<PlayerController>();
        Assert.IsNotNull(back, "回到游戏后主角要还在");
        Assert.AreEqual(-3.25f, back.transform.position.x, 0.05f, "要继续在暂停前的站位");
    }

    [UnityTest]
    public IEnumerator Pause_IsIgnoredDuringNpcActionPhase()
    {
        yield return null;
        yield return EnsureGameRoot();
        yield return LoadScene("FirstFloor");
        yield return WaitUntil(delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; }, 10f);

        PauseDirector director = PauseDirector.Instance;
        Assert.IsNotNull(director);

        // 玩家输入阶段碰一下，流程会进入 NPC 行动阶段（此时不该允许暂停，避免演出被打断）。
        ItemView item = Object.FindObjectOfType<ItemView>();
        if (item != null) GameRoot.Instance.Flow.RegisterPlayerTouch(item.itemId);

        yield return WaitUntil(delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.NpcAction; }, 10f);
        Assert.IsFalse(director.CanPause, "NPC 行动阶段不开放暂停");
    }

    [UnityTest]
    public IEnumerator PauseScene_BackgroundCoversScreen_WithSnapshot()
    {
        yield return null;
        yield return EnsureGameRoot();

        // 手动塞一张"截图"，检查暂停场景的背景确实是用它铺满相机的。
        PauseSnapshot.Clear();
        Texture2D texture = new Texture2D(64, 36, TextureFormat.RGBA32, false);
        PauseSnapshot.Store(texture, AreaId.FirstFloor, 0f);

        yield return LoadScene("Pause");
        yield return null;

        PauseScreen screen = Object.FindObjectOfType<PauseScreen>();
        Assert.IsNotNull(screen, "暂停场景里要有 PauseScreen");
        Assert.IsNotNull(screen.background, "暂停场景要有背景渲染器");
        Assert.IsNotNull(screen.background.sprite, "背景 sprite 应该由快照生成");
        Assert.IsTrue(screen.background.enabled);

        Camera camera = Camera.main;
        Assert.IsNotNull(camera, "暂停场景要有主相机");

        Vector3 scale = screen.background.transform.lossyScale;
        float width = screen.background.sprite.bounds.size.x * Mathf.Abs(scale.x);
        float height = screen.background.sprite.bounds.size.y * Mathf.Abs(scale.y);
        float viewHeight = camera.orthographicSize * 2f;
        float viewWidth = viewHeight * camera.aspect;

        Assert.GreaterOrEqual(width, viewWidth - 0.01f, "背景要盖满屏幕宽度");
        Assert.GreaterOrEqual(height, viewHeight - 0.01f, "背景要盖满屏幕高度");
    }

    private static IEnumerator EnsureGameRoot()
    {
        if (GameRoot.Instance != null) yield break;

        GameObject rootObject = new GameObject("[GameRoot-Test]");
        rootObject.AddComponent<GameRoot>();
        yield return null;
    }

    private static IEnumerator LoadScene(string sceneName)
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (load != null && !load.isDone) yield return null;
        yield return null;
        yield return null;
    }

    private static IEnumerator WaitUntil(System.Func<bool> condition, float timeout)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - start < timeout) yield return null;
    }
}
