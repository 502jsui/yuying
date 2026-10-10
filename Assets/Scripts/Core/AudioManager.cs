using System.Collections;
using UnityEngine;

/// <summary>
<<<<<<< HEAD
/// 音频管理：音量总线（主 / 音乐 / 音效）、音效表播放、雨声环境音淡入淡出与「断一拍」。
/// 音效素材配在 Resources/SfxLibrary.asset，雨声循环配在 GameConfig.rainLoop。
=======
/// 音频管理。目前没有正式音频素材，所以只提供接口和音量控制，
/// 之后把 AudioClip 填进 GameConfig 就能直接用（雨声循环、UI 音、断拍）。
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
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
<<<<<<< HEAD
    private Coroutine rainFadeRoutine;
    private SfxLibrary library;

    /// <summary>雨声当前音量（0~1，淡入淡出的就是它）。</summary>
    private float rainCurrentVolume;
    private float rainTargetVolume;
=======
    private float rainBaseVolume = 1f;
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0

    public float MasterVolume { get; private set; } = 1f;
    public float MusicVolume { get; private set; } = 1f;
    public float SfxVolume { get; private set; } = 1f;

<<<<<<< HEAD
    /// <summary>最近一次通过 SfxId 播放的音效（测试 / 调试用）。</summary>
    public SfxId LastPlayedSfxId { get; private set; } = SfxId.None;
    public AudioClip LastPlayedClip { get; private set; }
    public float RainVolume { get { return rainCurrentVolume; } }
    public float RainTargetVolume { get { return rainTargetVolume; } }
    public SfxLibrary Library { get { return library; } }
    public bool MusicIsPlaying { get { return musicSource != null && musicSource.isPlaying; } }
    public AudioClip MusicClip { get { return musicSource != null ? musicSource.clip : null; } }

    public void Initialize(GameConfig config)
    {
        Initialize(config, SfxLibrary.Load());
    }

    public void Initialize(GameConfig config, SfxLibrary sfxLibrary)
    {
        library = sfxLibrary;
=======
    public void Initialize(GameConfig config)
    {
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
        MasterVolume = PlayerPrefs.GetFloat(MasterKey, 1f);
        MusicVolume = PlayerPrefs.GetFloat(MusicKey, 1f);
        SfxVolume = PlayerPrefs.GetFloat(SfxKey, 1f);

        musicSource = CreateSource("Music");
        sfxSource = CreateSource("Sfx");
        rainSource = CreateSource("Rain");

<<<<<<< HEAD
        // 背景音乐：一进游戏就循环（走"音乐"音量总线）
        if (config != null && config.musicLoop != null)
        {
            musicSource.clip = config.musicLoop;
            musicSource.loop = true;
            musicSource.volume = MasterVolume * MusicVolume;
            musicSource.Play();
        }

=======
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
        if (config != null && config.rainLoop != null)
        {
            rainSource.clip = config.rainLoop;
            rainSource.loop = true;
<<<<<<< HEAD
            rainCurrentVolume = 0f;   // 默认静音，进后院再淡入
            rainTargetVolume = 0f;
            ApplyVolumes();
=======
            rainSource.volume = MasterVolume * MusicVolume;
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
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
<<<<<<< HEAD
        if (rainSource != null) rainSource.volume = MasterVolume * MusicVolume * rainCurrentVolume;
=======
        if (rainSource != null) rainSource.volume = MasterVolume * MusicVolume * rainBaseVolume;
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
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
<<<<<<< HEAD
        sfxSource.PlayOneShot(clip, MasterVolume * SfxVolume * Mathf.Clamp(volumeScale, 0f, 4f));
    }

    /// <summary>按音效表播放一条音效（含随机变体和音量补偿）。返回是否真的播了。</summary>
    public bool PlaySfx(SfxId id, float volumeScale = 1f)
    {
        if (library == null) return false;

        AudioClip clip = library.PickClip(id);
        if (clip == null) return false;

        LastPlayedSfxId = id;
        LastPlayedClip = clip;
        PlaySfx(clip, library.GetVolumeScale(id) * volumeScale);
        return true;
    }

    /// <summary>立即把雨声设到某个音量（不淡）。</summary>
    public void SetRainIntensity(float intensity01)
    {
        rainTargetVolume = Mathf.Clamp01(intensity01);
        rainCurrentVolume = rainTargetVolume;
        ApplyVolumes();
    }

    /// <summary>在 seconds 秒内把雨声淡到某个音量（进后院淡入 / 回一楼淡出）。</summary>
    public void FadeRainTo(float intensity01, float seconds = 1.5f)
    {
        rainTargetVolume = Mathf.Clamp01(intensity01);
        if (rainSource == null) return;

        if (!rainSource.isPlaying && rainSource.clip != null) rainSource.Play();
        if (rainFadeRoutine != null) StopCoroutine(rainFadeRoutine);
        rainFadeRoutine = StartCoroutine(RainFadeRoutine(seconds));
    }

    private IEnumerator RainFadeRoutine(float seconds)
    {
        float duration = Mathf.Max(0.01f, seconds);
        while (Mathf.Abs(rainCurrentVolume - rainTargetVolume) > 0.001f)
        {
            rainCurrentVolume = Mathf.MoveTowards(
                rainCurrentVolume, rainTargetVolume, Time.unscaledDeltaTime / duration);
            ApplyVolumes();
            yield return null;
        }

        rainCurrentVolume = rainTargetVolume;
        ApplyVolumes();
        rainFadeRoutine = null;
=======
        sfxSource.PlayOneShot(clip, MasterVolume * SfxVolume * Mathf.Clamp01(volumeScale));
    }

    public void SetRainIntensity(float intensity01)
    {
        rainBaseVolume = Mathf.Clamp01(intensity01);
        if (rainSource != null) rainSource.volume = MasterVolume * MusicVolume * rainBaseVolume;
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
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
<<<<<<< HEAD
        rainSource.volume = 0f;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, duration));
        ApplyVolumes();
=======
        float original = rainSource.volume;
        rainSource.volume = 0f;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, duration));
        rainSource.volume = original;
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
        rainPauseRoutine = null;
    }
}
