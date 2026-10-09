using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class SettingsMenuUI : MonoBehaviour
{
    [Header("滑动条")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;
    public Slider brightnessSlider;

    [Header("音频混合器")]
    public AudioMixer audioMixer;

    [Header("场景亮度遮罩")]
    public Image brightnessOverlay;

    private void Start()
    {
        // 读取保存的设置
        LoadSettings();

        // 绑定滑动条事件
        masterSlider.onValueChanged.AddListener(OnMasterChanged);
        musicSlider.onValueChanged.AddListener(OnMusicChanged);
        sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
    }

    public void OnMasterChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
    }

    public void OnMusicChanged(float value)
    {
        float db = value > 0.0001f ? Mathf.Log10(value) * 20f : -80f;
        audioMixer.SetFloat("MusicVolume", db);
        PlayerPrefs.SetFloat("MusicVolume", value);
    }

    public void OnSFXChanged(float value)
    {
        float db = value > 0.0001f ? Mathf.Log10(value) * 20f : -80f;
        audioMixer.SetFloat("SFXVolume", db);
        PlayerPrefs.SetFloat("SFXVolume", value);
    }

    public void OnBrightnessChanged(float value)
    {
        if (brightnessOverlay == null) return;
        float alpha = Mathf.Lerp(0.8f, 0f, value);
        Color c = brightnessOverlay.color;
        c.a = alpha;
        brightnessOverlay.color = c;
        PlayerPrefs.SetFloat("Brightness", value);
    }

    private void LoadSettings()
    {
        float master = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float music = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        float sfx = PlayerPrefs.GetFloat("SFXVolume", 0.5f);
        float brightness = PlayerPrefs.GetFloat("Brightness", 1f);

        masterSlider.value = master;
        musicSlider.value = music;
        sfxSlider.value = sfx;
        brightnessSlider.value = brightness;

        OnMasterChanged(master);
        OnMusicChanged(music);
        OnSFXChanged(sfx);
        OnBrightnessChanged(brightness);
    }

    // 返回按钮
    public void OnBackClicked()
    {
        SceneManager.LoadScene("MainMenu");
    }
}