using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;  

/// <summary>
/// 暂停界面及设置项的转发
/// 暂停界面和设置项互斥显示
/// 依赖于主程序埋下的音频接口
/// </summary>

public class PauseMenuUI : MonoBehaviour
{
    [Header("两个面板")]
    public GameObject mainPanel;       // 🆕 暂停主菜单（三个按钮）
    public GameObject settingsPanel;   // 设置面板

    [Header("设置面板里的控件（转发给 AudioManager）")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;
    public Toggle muteToggle;

    [Header("场景亮度遮罩")]
    public Image brightnessOverlay;

    [Header("重新开始")]
    [Tooltip("重开一局时加载的起始场景，必须在 Build Settings 里")]
    public string restartSceneName = "FirstFloor";


    /// <summary>
    /// 暂停时「重新开始」按钮的回调：放弃当前进度，从头开一局。
    /// StartNewGame() 会重置 session / 道具 / 随机种子 / timeScale / 回合循环；
    /// 但它不重载场景，所以这里再 LoadScene 一次，把场景里的物件和站位也刷新。
    /// </summary>
    public void OnRestartClicked()
    {
        GameRoot root = GameRoot.Instance;
        if (root == null) return;

        // ① 先离开"暂停"状态（Resume 只在 Paused 时生效，会把 timeScale 恢复成 1）
        if (root.Flow != null) root.Flow.Resume();

        // ② 清掉暂停现场（截图 / 待恢复的站位），避免带脏数据
        PauseSnapshot.Clear();

        // ③ 重置这一局（session / 道具 / 随机种子 / 计时 / 回合循环）
        root.StartNewGame();

        // ④ 重新加载起始场景，让场景里的物件和站位也回到初始状态
        SceneManager.LoadScene(restartSceneName, LoadSceneMode.Single);
    }
    private AudioManager Audio
    {
        get { return GameRoot.Instance != null ? GameRoot.Instance.Audio : null; }
    }

    private void Start()
    {
        // 初始状态：只显示主菜单
        if (mainPanel != null) mainPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (musicSlider  != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (sfxSlider    != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        if (muteToggle   != null) muteToggle.onValueChanged.AddListener(OnMuteToggled);
    }

    // ---- 面板开关 ----
    public void OnReturnToMainMenuClicked()
    {
        if (PauseDirector.Instance != null) PauseDirector.Instance.ReturnToMainMenu();
    }
    public void OpenSettingsPanel()
    {
        if (settingsPanel == null)
        {
            Debug.LogWarning("[暂停菜单] settingsPanel 没填！把 SettingsPanel 拖到 PauseMenuUI 的这个字段上。");
            return;
        }

        // 👇 关键：先把主菜单藏起来
        if (mainPanel != null) mainPanel.SetActive(false);

        settingsPanel.SetActive(true);

        if (Audio != null)
        {
            if (masterSlider != null) masterSlider.SetValueWithoutNotify(Audio.MasterVolume);
            if (musicSlider  != null) musicSlider.SetValueWithoutNotify(Audio.MusicVolume);
            if (sfxSlider    != null) sfxSlider.SetValueWithoutNotify(Audio.SfxVolume);
            if (muteToggle   != null) muteToggle.SetIsOnWithoutNotify(Audio.IsMuted);
        }
    }

    public void CloseSettingsPanel()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // 👇 再把主菜单放回来
        if (mainPanel != null) mainPanel.SetActive(true);
    }

 public void OnBrightnessChanged(float value)
    {
        Debug.Log("亮度调节暂未实现");
        
    }
    // ---- 音量：只转发给 AudioManager ----

    public void OnMasterChanged(float v) { if (Audio != null) Audio.SetMasterVolume(v); }
    public void OnMusicChanged (float v) { if (Audio != null) Audio.SetMusicVolume(v);  }
    public void OnSfxChanged   (float v) { if (Audio != null) Audio.SetSfxVolume(v);    }
    public void OnMuteToggled  (bool on) { if (Audio != null) Audio.SetMuted(on);       }
}