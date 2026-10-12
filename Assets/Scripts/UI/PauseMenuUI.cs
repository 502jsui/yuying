using UnityEngine;
using UnityEngine.UI;

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