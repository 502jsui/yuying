using System.Collections;
using UnityEngine;

/// <summary>
/// 工程唯一的音频出口：任何人要出声都得经过这里，不要各自 AddComponent&lt;AudioSource&gt;()。
///
/// 谁在用：GameRoot 持有；AudioDirector 订阅玩法事件播音效；UI（设置 / 暂停面板）只调音量与静音接口。
///
/// 接口分四组：
///   1) 音量总线：MasterVolume / MusicVolume / SfxVolume + SetXxxVolume + IsMuted / SetMuted / ToggleMute
///      （数值自动存 PlayerPrefs，重启保留；静音只是把三路输出乘 0，滑条数值不动）
///   2) 音效 SFX：PlaySfx(SfxId, scale) / PlaySfx(clip, scale)，音效表在 Resources/SfxLibrary.asset
///   3) 背景音乐 BGM：PlayMusic / PlayMusicWithFade / PlayMusicIfDifferent / StopMusic / StopMusicWithFade
///      + IsMusicPlaying / CurrentMusic
///   4) 雨声环境音：SetRainIntensity / FadeRainTo / TriggerRainPause / RainVolume / RainTargetVolume
///
/// 调用方一律走 GameRoot.Instance.Audio；雨声循环配在 GameConfig.rainLoop，开局 BGM 配在 GameConfig.musicLoop。
/// </summary>
public class AudioManager : MonoBehaviour
{
    private const string MasterKey = "yuyin_audio_master";
    private const string MusicKey = "yuyin_audio_music";
    private const string SfxKey = "yuyin_audio_sfx";
    private const string MuteKey = "yuyin_audio_muted";

    private AudioSource musicSource;
    private AudioSource sfxSource;
    private AudioSource rainSource;
    private Coroutine rainPauseRoutine;
    private Coroutine rainFadeRoutine;
    private Coroutine musicFadeRoutine;
    private SfxLibrary library;

    /// <summary>雨声当前音量（0~1，淡入淡出的就是它）。</summary>
    private float rainCurrentVolume;
    private float rainTargetVolume;

    public float MasterVolume { get; private set; } = 1f;
    public float MusicVolume { get; private set; } = 1f;
    public float SfxVolume { get; private set; } = 1f;

    /// <summary>最近一次通过 SfxId 播放的音效（测试 / 调试用）。</summary>
    public SfxId LastPlayedSfxId { get; private set; } = SfxId.None;
    public AudioClip LastPlayedClip { get; private set; }
    public float RainVolume { get { return rainCurrentVolume; } }
    public float RainTargetVolume { get { return rainTargetVolume; } }
    public SfxLibrary Library { get { return library; } }

    /// <summary>全局静音。为 true 时三路输出都乘 0，但滑条上的数值 / 存档保持不变。</summary>
    public bool IsMuted { get; private set; }

    /// <summary>静音时给所有音量乘的系数（0 或 1）。</summary>
    private float BusScale { get { return IsMuted ? 0f : 1f; } }

    public void Initialize(GameConfig config)
    {
        Initialize(config, SfxLibrary.Load());
    }

    public void Initialize(GameConfig config, SfxLibrary sfxLibrary)
    {
        library = sfxLibrary;
        MasterVolume = PlayerPrefs.GetFloat(MasterKey, 1f);
        MusicVolume = PlayerPrefs.GetFloat(MusicKey, 1f);
        SfxVolume = PlayerPrefs.GetFloat(SfxKey, 1f);
        IsMuted = PlayerPrefs.GetInt(MuteKey, 0) == 1;

        musicSource = CreateSource("Music");
        sfxSource = CreateSource("Sfx");
        rainSource = CreateSource("Rain");

        // 背景音乐：一进游戏就循环（走"音乐"音量总线）。
        // 如果有别的 BGM 控制器要接管，把 GameConfig.autoPlayMusicOnStart 关掉即可。
        if (config != null && config.autoPlayMusicOnStart && config.musicLoop != null)
        {
            musicSource.clip = config.musicLoop;
            musicSource.loop = true;
            musicSource.volume = MasterVolume * MusicVolume * BusScale;
            musicSource.Play();
        }

        if (config != null && config.rainLoop != null)
        {
            rainSource.clip = config.rainLoop;
            rainSource.loop = true;
            rainCurrentVolume = 0f;   // 默认静音，进后院再淡入
            rainTargetVolume = 0f;
            ApplyVolumes();
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
        PlayerPrefs.Save();
        ApplyVolumes();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        PlayerPrefs.Save();
        ApplyVolumes();
    }

    // ------------------------------------------------------------------
    // 背景音乐接口（给 BGM 控制器 / 设置界面用）
    // ------------------------------------------------------------------

    /// <summary>现在有没有音乐在播。</summary>
    public bool IsMusicPlaying
    {
        get { return musicSource != null && musicSource.isPlaying; }
    }

    /// <summary>现在这首是什么（没播就返回 null）。</summary>
    public AudioClip CurrentMusic
    {
        get { return musicSource != null ? musicSource.clip : null; }
    }

    /// <summary>立刻停掉背景音乐。</summary>
    public void StopMusic()
    {
        StopMusicFade();
        if (musicSource == null) return;
        musicSource.Stop();
        musicSource.clip = null;
    }

    /// <summary>带淡入淡出地换背景音乐；fadeSeconds 传 0 等价于直接切。</summary>
    public void PlayMusicWithFade(AudioClip clip, float fadeSeconds = 0.6f, bool loop = true)
    {
        if (musicSource == null || clip == null) return;

        if (fadeSeconds <= 0f)
        {
            PlayMusic(clip, loop);
            return;
        }

        StopMusicFade();
        musicFadeRoutine = StartCoroutine(CrossfadeMusic(clip, loop, fadeSeconds));
    }

    /// <summary>
    /// 只有不是同一首在播时才换 —— 用它做"切场景不把同一首 BGM 重头播"。
    /// </summary>
    public void PlayMusicIfDifferent(AudioClip clip, bool loop = true, float fadeSeconds = 0.6f)
    {
        if (clip == null) return;

        if (musicSource != null && musicSource.clip == clip && musicSource.isPlaying)
        {
            musicSource.loop = loop;
            return;
        }

        PlayMusicWithFade(clip, fadeSeconds, loop);
    }

    /// <summary>带淡出地停背景音乐。</summary>
    public void StopMusicWithFade(float fadeSeconds = 0.6f)
    {
        if (musicSource == null) return;

        if (fadeSeconds <= 0f)
        {
            StopMusic();
            return;
        }

        StopMusicFade();
        musicFadeRoutine = StartCoroutine(FadeOutMusic(fadeSeconds));
    }

    private void StopMusicFade()
    {
        if (musicFadeRoutine == null) return;
        StopCoroutine(musicFadeRoutine);
        musicFadeRoutine = null;
    }

    private IEnumerator CrossfadeMusic(AudioClip clip, bool loop, float fadeSeconds)
    {
        float target = MasterVolume * MusicVolume * BusScale;
        float half = Mathf.Max(0.01f, fadeSeconds * 0.5f);

        // 前半段：当前这首淡出
        if (musicSource.isPlaying)
        {
            float from = musicSource.volume;
            float t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(from, 0f, Mathf.Clamp01(t / half));
                yield return null;
            }
        }

        // 换曲
        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.volume = 0f;
        musicSource.Play();

        // 后半段：新这首淡入
        float e = 0f;
        while (e < half)
        {
            e += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(0f, target, Mathf.Clamp01(e / half));
            yield return null;
        }

        musicSource.volume = target;
        musicFadeRoutine = null;
    }

    private IEnumerator FadeOutMusic(float fadeSeconds)
    {
        float from = musicSource.volume;
        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            if (musicSource == null) yield break;
            musicSource.volume = Mathf.Lerp(from, 0f, Mathf.Clamp01(t / fadeSeconds));
            yield return null;
        }

        if (musicSource != null)
        {
            musicSource.Stop();
            musicSource.clip = null;
        }
        musicFadeRoutine = null;
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxKey, SfxVolume);
        PlayerPrefs.Save();
        ApplyVolumes();
    }

    // ------------------------------------------------------------------
    // 静音接口（给设置界面用）
    // ------------------------------------------------------------------

    /// <summary>开关静音。true=静音。会存 PlayerPrefs，重启保留。</summary>
    public void SetMuted(bool muted)
    {
        IsMuted = muted;
        PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
        PlayerPrefs.Save();
        ApplyVolumes();
    }

    /// <summary>切换静音，返回切换后的状态（给"静音"按钮用）。</summary>
    public bool ToggleMute()
    {
        SetMuted(!IsMuted);
        return IsMuted;
    }

    public void ApplyVolumes()
    {
        // 正在淡入淡出时别抢音量，交给协程管。
        if (musicSource != null && musicFadeRoutine == null) musicSource.volume = MasterVolume * MusicVolume * BusScale;
        if (rainSource != null) rainSource.volume = MasterVolume * MusicVolume * rainCurrentVolume * BusScale;

        // 音效走 PlayOneShot(clip, scale)，音量已经在 PlaySfx 里算好，
        // 这里把源音量固定回 1，避免"总线音量被乘两次"。
        if (sfxSource != null) sfxSource.volume = 1f;
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null) return;
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.volume = MasterVolume * MusicVolume * BusScale;
        musicSource.Play();
    }

    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip, MasterVolume * SfxVolume * Mathf.Clamp(volumeScale, 0f, 4f) * BusScale);
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
        rainSource.volume = 0f;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, duration));
        ApplyVolumes();
        rainPauseRoutine = null;
    }
}
