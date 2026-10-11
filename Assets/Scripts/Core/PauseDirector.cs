using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 暂停 / 继续 / 设置 / 退出。
///
/// 暂停的做法（策划口径）：**跳到独立的暂停场景**，游戏场景卸载掉；
/// 暂停场景的背景就是"点暂停那一刻的游戏画面截图"（PauseSnapshot）。
/// 数据都挂在常驻的 GameRoot 上，所以来回跳不会丢进度；玩家站位也会还原。
///
/// 挂在 GameRoot 上（常驻），UI 只要调 <see cref="PauseGame"/> / <see cref="ResumeGame"/> /
/// <see cref="OpenSettings"/> / <see cref="QuitGame"/> 就行；暂停场景里的
/// PauseScreen 已经把这三个按钮回调留好了。
/// </summary>
[DefaultExecutionOrder(-700)]
public class PauseDirector : MonoBehaviour
{
    public static PauseDirector Instance { get; private set; }

    [Tooltip("游戏里按这个键暂停。UI 上的暂停按钮直接调 PauseGame()。")]
    public KeyCode pauseKey = KeyCode.Escape;

    [Tooltip("暂停场景名（要加进 Build Settings）。")]
    public string pauseSceneName = "Pause";

    private bool switching;

    public bool IsPaused
    {
        get
        {
            GameRoot root = GameRoot.Instance;
            return root != null && root.Flow != null && root.Flow.State == GameState.Paused;
        }
    }

    /// <summary>现在能不能暂停：游戏中、玩家可操作（NPC 行动阶段不加暂停，避免演出被打断）。</summary>
    public bool CanPause
    {
        get
        {
            GameRoot root = GameRoot.Instance;
            if (root == null || root.Flow == null || root.Session == null) return false;
            if (switching) return false;
            if (!root.Flow.IsRunning || root.Session.isGameOver) return false;
            return root.Flow.State == GameState.Playing && root.Flow.CanPlayerMove;
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(pauseKey)) PauseGame();
    }

    /// <summary>暂停：截一张当前画面 → 冻结流程 → 跳到暂停场景。</summary>
    public void PauseGame()
    {
        if (!CanPause) return;
        StartCoroutine(PauseRoutine());
    }

    private IEnumerator PauseRoutine()
    {
        switching = true;

        // 截图要等这一帧渲染完。
        // 注意：无渲染设备（-nographics / 批处理）时没有 EndOfFrame 回调，
        // 等它会永远卡住，所以那种情况下直接跳过等待（反正也截不到图）。
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
        {
            yield return new WaitForEndOfFrame();
        }
        yield return null;

        GameRoot root = GameRoot.Instance;
        if (root == null || root.Flow == null || root.Session == null)
        {
            switching = false;
            yield break;
        }

        Texture2D shot = CaptureScreenshot();
        PauseSnapshot.Store(shot, root.Session.playerArea, CurrentPlayerX());

        root.Flow.Pause();   // Time.timeScale = 0 + GameState.Paused（回合循环跟着停）

        switching = false;
        SceneManager.LoadScene(pauseSceneName, LoadSceneMode.Single);
    }

    /// <summary>继续游戏：回到点暂停时那个场景，恢复流程与玩家站位。</summary>
    public void ResumeGame()
    {
        GameRoot root = GameRoot.Instance;
        if (root == null || root.Flow == null) return;

        string sceneName = PauseSnapshot.HasSnapshot
            ? PauseSnapshot.ReturnSceneName
            : PauseSnapshot.SceneNameOf(root.Session != null ? root.Session.playerArea : AreaId.FirstFloor);

        root.Flow.Resume();                                    // 先把 timeScale / 状态恢复
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single); // PlayerController.Start 会把站位放回去

        // 注意：这里不要 PauseSnapshot.Clear()。PlayerController.Start 要到下一帧才跑，
        // 站位要留到那时候消费；截图由暂停场景卸载时（PauseScreen.OnDestroy）释放。

        Debug.Log("[暂停] 继续游戏，回到 " + sceneName);
    }

    /// <summary>设置界面（UI 同学负责，场景叫 Settings 就跳，没有就先只打日志）。</summary>
    public void OpenSettings()
    {
        const string settingsScene = "Settings";
        if (Application.CanStreamedLevelBeLoaded(settingsScene))
        {
            SceneManager.LoadScene(settingsScene, LoadSceneMode.Single);
            return;
        }

        Debug.LogWarning("[暂停] 设置场景还没接（UI 负责）：加载 " + settingsScene + " 失败，先留在暂停界面。");
    }

    /// <summary>退出游戏。</summary>
    public void QuitGame()
    {
        Debug.Log("[暂停] 退出游戏。");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static Texture2D CaptureScreenshot()
    {
        // 无渲染设备（-nographics / 批处理测试）时截不了，返回空，暂停场景会用纯色兜底。
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return null;

        try
        {
            return ScreenCapture.CaptureScreenshotAsTexture();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[暂停] 截图失败，背景退回纯色：" + exception.Message);
            return null;
        }
    }

    private static float CurrentPlayerX()
    {
        GameRoot root = GameRoot.Instance;
        if (root != null && root.Scene != null && root.Scene.player != null)
            return root.Scene.player.transform.position.x;

        PlayerController player = Object.FindObjectOfType<PlayerController>();
        return player != null ? player.transform.position.x : 0f;
    }
}
