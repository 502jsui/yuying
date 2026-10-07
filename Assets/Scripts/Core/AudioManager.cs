using System.Collections;
using UnityEngine;

/// <summary>
/// 音频管理。目前没有正式音频素材，所以只提供接口和音量控制，
/// 之后把 AudioClip 填进 GameConfig 就能直接用（雨声循环、UI 音、断拍）。
/// </summary>
public class AudioManager : MonoBehaviour
{
    private const string MasterKey = "yuyin_audio_master";
    private const string MusicKey = "yuyin_audio_music";
    private const string SfxKey = "yuyin_audio_sfx";

    private AudioSource musicSource;
    private AudioSource sfxSource;
    private AudioSource rainSource;
    private Coroutine rainPauseRoutine;
    private float rainBaseVolume = 1f;

    public float MasterVolume { get; private set; } = 1f;
    public float MusicVolume { get; private set; } = 1f;
    public float SfxVolume { get; private set; } = 1f;

    public void Initialize(GameConfig config)
    {
        MasterVolume = PlayerPrefs.GetFloat(MasterKey, 1f);
        MusicVolume = PlayerPrefs.GetFloat(MusicKey, 1f);
        SfxVolume = PlayerPrefs.GetFloat(SfxKey, 1f);

        musicSource = CreateSource("Music");
        sfxSource = CreateSource("Sfx");
        rainSource = CreateSource("Rain");

        if (config != null && config.rainLoop != null)
        {
            rainSource.clip = config.rainLoop;
            rainSource.loop = true;
            rainSource.volume = MasterVolume * MusicVolume;
            rainSource.Play();
        }
    }

    private AudioSource CreateSource(string sourceName)
    {
        GameObject child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MasterKey, MasterVolume);
        ApplyVolumes();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        ApplyVolumes();
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxKey, SfxVolume);
    }

    public void ApplyVolumes()
    {
        if (musicSource != null) musicSource.volume = MasterVolume * MusicVolume;
        if (rainSource != null) rainSource.volume = MasterVolume * MusicVolume * rainBaseVolume;
        if (sfxSource != null) sfxSource.volume = MasterVolume * SfxVolume;
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null) return;
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.volume = MasterVolume * MusicVolume;
        musicSource.Play();
    }

    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip, MasterVolume * SfxVolume * Mathf.Clamp01(volumeScale));
    }

    public void SetRainIntensity(float intensity01)
    {
        rainBaseVolume = Mathf.Clamp01(intensity01);
        if (rainSource != null) rainSource.volume = MasterVolume * MusicVolume * rainBaseVolume;
    }

    /// <summary>雨声断一拍：某户被影响时调用。</summary>
    public void TriggerRainPause(float duration = 0.18f)
    {
        if (rainSource == null) return;
        if (rainPauseRoutine != null) StopCoroutine(rainPauseRoutine);
        rainPauseRoutine = StartCoroutine(RainPauseRoutine(duration));
    }

    private IEnumerator RainPauseRoutine(float duration)
    {
        float original = rainSource.volume;
        rainSource.volume = 0f;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, duration));
        rainSource.volume = original;
        rainPauseRoutine = null;
    }
}
