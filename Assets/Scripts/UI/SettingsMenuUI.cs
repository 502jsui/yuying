using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SettingsMenuUI : MonoBehaviour
{
    [Header("滑动条（Range 请设成 0~1，不是 0~100）")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;
    public Slider brightnessSlider;

    [Header("静音开关（可选，界面上没有就留空）")]
    public Toggle muteToggle;

    /// <summary>亮度不属于音频，继续自己存。</summary>
    private const string BrightnessKey = "Brightness";

    /// <summary>全局唯一的音频出口。GameRoot 在 Play 模式下一定存在。</summary>
    private static AudioManager Audio
    {
        get { return GameRoot.Instance != null ? GameRoot.Instance.Audio : null; }
    }

    private void Start()
    {
        LoadSettings();

        masterSlider.onValueChanged.AddListener(OnMasterChanged);
        musicSlider.onValueChanged.AddListener(OnMusicChanged);
        sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
        if (muteToggle != null) muteToggle.onValueChanged.AddListener(OnMuteToggled);
    }

    // ------------------------------------------------------------------
    // 音频：全部转发给 AudioManager，自己不再存一份、也不再乘第二层
    // ------------------------------------------------------------------

    /// <summary>主音量。音乐 = Master × Music，音效 = Master × Sfx，雨声也走 Master × Music。</summary>
    public void OnMasterChanged(float value)
    {
        if (Audio != null) Audio.SetMasterVolume(value);    // 内部自动 Clamp01 并存档
    }

    public void OnMusicChanged(float value)
    {
        if (Audio != null) Audio.SetMusicVolume(value);
    }

    public void OnSFXChanged(float value)
    {
        if (Audio != null) Audio.SetSfxVolume(value);
    }

    /// <summary>静音只是把三路输出乘 0，滑条数值不变，取消静音后按原比例恢复。</summary>
    public void OnMuteToggled(bool on)
    {
        if (Audio != null) Audio.SetMuted(on);
    }

    public void OnBrightnessChanged(float value)
    {
        Debug.Log("亮度调节暂未实现");

    }

    private void LoadSettings()
    {
        if (Audio != null)
        {
            // 用 AudioManager 里的当前真值回显，并且不触发 onValueChanged
            masterSlider.SetValueWithoutNotify(Audio.MasterVolume);
            musicSlider.SetValueWithoutNotify(Audio.MusicVolume);
            sfxSlider.SetValueWithoutNotify(Audio.SfxVolume);
            if (muteToggle != null) muteToggle.SetIsOnWithoutNotify(Audio.IsMuted);
        }
        else
        {
            masterSlider.SetValueWithoutNotify(1f);
            musicSlider.SetValueWithoutNotify(1f);
            sfxSlider.SetValueWithoutNotify(1f);
#if UNITY_EDITOR
            Debug.LogWarning("[SettingsMenuUI] 找不到 GameRoot.Instance.Audio，本次音量滑条不生效。" +
                             "（在编辑器里没有进 Play 模式时属于正常现象）");
#endif
        }

        float brightness = PlayerPrefs.GetFloat(BrightnessKey, 1f);
        brightnessSlider.SetValueWithoutNotify(brightness);
        OnBrightnessChanged(brightness);   // 亮度是纯表现，载入时直接应用一次
    }

    /// <summary>返回按钮。场景里是按方法名挂的（Settings.unity），这个签名不能改。</summary>
    public void OnBackClicked()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
