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
/// <see cref="QuitGame"/> 就行；暂停里的「设置」走暂停场景内的面板（`PauseMenuUI.OpenSettingsPanel`），
/// 不切场景 —— 否则会把暂停状态（timeScale = 0 + Paused）带丢。
/// </summary>
[DefaultExecutionOrder(-700)]
public class PauseDirector : MonoBehaviour
{
    public static PauseDirector Instance { get; private set; }

    [Tooltip("游戏里按这个键暂停。UI 上的暂停按钮直接调 PauseGame()。")]
    public KeyCode pauseKey = KeyCode.Escape;

    [Tooltip("暂停场景名（要加进 Build Settings）。")]
    public string pauseSceneName = "Pause";

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
            if (!root.Flow.IsRunning || root.Session.isGameOver) return false;
            if (SceneTransitionState.IsLoading) return false;   // 正在切场景：别把传送打断
            if (NarrationState.IsBlockingInput) return false;   // 开场独白期间别暂停，镜头状态会错位
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

        GameRoot root = GameRoot.Instance;
        if (root == null || root.Flow == null || root.Session == null) return;

        PauseSnapshot.Store(CaptureGameFrame(), root.Session.playerArea, CurrentPlayerX());

        root.Flow.Pause();   // Time.timeScale = 0 + GameState.Paused（回合循环跟着停）
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

    /// <summary>
    /// 把游戏相机重渲染到 RenderTexture 再读成贴图。
    ///
    /// 刻意不用 ScreenCapture.CaptureScreenshotAsTexture：那玩意儿读的是屏幕后缓冲，
    /// 无边框全屏（DXGI 翻转模型）下经常拿到全黑/空图，暂停背景就只剩兜底纯色了。
    /// 重渲染这条路不碰屏幕缓冲，窗口 / 全屏一致，颜色也和游戏里一模一样。
    /// </summary>
    private static Texture2D CaptureGameFrame()
    {
        // 无渲染设备（-nographics / 批处理测试）时截不了，返回空，暂停场景会用纯色兜底。
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return null;

        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("[暂停] 场景里找不到主相机，暂停背景退回纯色。");
            return null;
        }

        int width = Mathf.Max(2, Screen.width);
        int height = Mathf.Max(2, Screen.height);

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
        catch (System.Exception exception)
        {
            Debug.LogWarning("[暂停] 抓取画面失败，背景退回纯色：" + exception.Message);
            return null;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
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
