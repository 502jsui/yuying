using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AudioManager 的音量总线 / 静音接口 —— 设置界面就靠这几个。
/// 注意：会写 PlayerPrefs，所以 TearDown 一定把原值还原，别污染本机设置。
/// </summary>
public class AudioManagerBusTests
{
    private const string MasterKey = "yuyin_audio_master";
    private const string MusicKey = "yuyin_audio_music";
    private const string SfxKey = "yuyin_audio_sfx";
    private const string MuteKey = "yuyin_audio_muted";

    private GameObject host;
    private AudioManager audio;
    private float originalMaster;
    private float originalMusic;
    private float originalSfx;
    private bool originalMuted;

    [SetUp]
    public void SetUp()
    {
        originalMaster = PlayerPrefs.GetFloat(MasterKey, 1f);
        originalMusic = PlayerPrefs.GetFloat(MusicKey, 1f);
        originalSfx = PlayerPrefs.GetFloat(SfxKey, 1f);
        originalMuted = PlayerPrefs.GetInt(MuteKey, 0) == 1;

        host = new GameObject("[AudioManager-Test]");
        audio = host.AddComponent<AudioManager>();
        audio.Initialize(null);   // 不传 GameConfig：只验音量总线，不自动播 BGM / 雨声
    }

    [TearDown]
    public void TearDown()
    {
        if (audio != null)
        {
            audio.SetMasterVolume(originalMaster);
            audio.SetMusicVolume(originalMusic);
            audio.SetSfxVolume(originalSfx);
            audio.SetMuted(originalMuted);
        }

        if (host != null) Object.DestroyImmediate(host);
    }

    [Test]
    public void Volume_ClampsToRange_AndPersists()
    {
        audio.SetMasterVolume(0.4f);
        Assert.AreEqual(0.4f, audio.MasterVolume, 0.001f);
        Assert.AreEqual(0.4f, PlayerPrefs.GetFloat(MasterKey, -1f), 0.001f, "主音量要落盘");

        audio.SetMasterVolume(9f);
        Assert.AreEqual(1f, audio.MasterVolume, 0.001f, "超过 1 要 clamp");

        audio.SetMasterVolume(-3f);
        Assert.AreEqual(0f, audio.MasterVolume, 0.001f, "小于 0 要 clamp");
    }

    [Test]
    public void Mute_TogglesAndPersists_WithoutClearingSliders()
    {
        audio.SetMuted(false);
        Assert.IsFalse(audio.IsMuted);

        Assert.IsTrue(audio.ToggleMute(), "ToggleMute 要返回切换后的状态");
        Assert.IsTrue(audio.IsMuted);
        Assert.AreEqual(1, PlayerPrefs.GetInt(MuteKey, 0), "静音状态要落盘");

        // 静音 ≠ 把滑条清零：数值要留着，取消静音后能原样恢复。
        audio.SetMusicVolume(0.6f);
        Assert.AreEqual(0.6f, audio.MusicVolume, 0.001f, "静音时滑条数值要保留");

        Assert.IsFalse(audio.ToggleMute());
        Assert.IsFalse(audio.IsMuted);
        Assert.AreEqual(0, PlayerPrefs.GetInt(MuteKey, 0));
    }

    [Test]
    public void Mute_IsReadBackOnInitialize()
    {
        audio.SetMuted(true);

        GameObject second = new GameObject("[AudioManager-Test-Second]");
        try
        {
            AudioManager fresh = second.AddComponent<AudioManager>();
            fresh.Initialize(null);
            Assert.IsTrue(fresh.IsMuted, "重新 Initialize 要读回上次的静音状态");
        }
        finally
        {
            Object.DestroyImmediate(second);
        }
    }
}
