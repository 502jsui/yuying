<<<<<<< HEAD
using System.Collections.Generic;
=======
using System.IO;
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成 / 校验全局配置资产，让策划有一个可以点的入口。
/// </summary>
public static class YuyinConfigTools
{
    private const string ResourceFolder = "Assets/Resources";
    private const string ConfigPath = ResourceFolder + "/GameConfig.asset";
<<<<<<< HEAD
    private const string TextPath = ResourceFolder + "/GameText.asset";
    private const string SfxLibraryPath = ResourceFolder + "/SfxLibrary.asset";
    private const string SfxRoot = "Assets/Audio/SFX";
    private const string RainLoopPath = "Assets/Audio/Ambience/rain_C_amb_loop_2_gopro.ogg";
    private const string MusicLoopPath = "Assets/Audio/Music/piano_C_slow_intermission_springspring.ogg";
=======
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0

    [MenuItem("Tools/余音/创建或选中默认配置")]
    public static void CreateOrSelectConfig()
    {
        GameConfig config = EnsureConfigAsset();
        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
    }

<<<<<<< HEAD
    [MenuItem("Tools/余音/创建或选中文案表")]
    public static void CreateOrSelectGameText()
    {
        GameText text = EnsureGameTextAsset();
        Selection.activeObject = text;
        EditorGUIUtility.PingObject(text);
    }

    [MenuItem("Tools/余音/创建或选中音效库")]
    public static void CreateOrSelectSfxLibrary()
    {
        SfxLibrary library = EnsureSfxLibraryAsset();
        Selection.activeObject = library;
        EditorGUIUtility.PingObject(library);
    }

    /// <summary>批处理入口：一次把配置 / 文案 / 音效库 / 雨声都准备好。</summary>
    public static void EnsureAllFromCommandLine()
    {
        EnsureConfigAsset();
        EnsureGameTextAsset();
        EnsureSfxLibraryAsset();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[余音] GameConfig / GameText / SfxLibrary / rainLoop 都已就绪。");
    }

=======
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
    /// <summary>批处理入口：unity -executeMethod YuyinConfigTools.EnsureConfigAssetFromCommandLine</summary>
    public static void EnsureConfigAssetFromCommandLine()
    {
        EnsureConfigAsset();
        AssetDatabase.SaveAssets();
        Debug.Log("GameConfig 资产已就绪：" + ConfigPath);
    }

    public static GameConfig EnsureConfigAsset()
    {
        GameConfig existing = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
        if (existing != null)
        {
            existing.EnsureDefaults();
            EditorUtility.SetDirty(existing);
<<<<<<< HEAD
            BindRainLoop(existing);
            BindMusicLoop(existing);
=======
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
            return existing;
        }

        if (!AssetDatabase.IsValidFolder(ResourceFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        GameConfig created = ScriptableObject.CreateInstance<GameConfig>();
        created.EnsureDefaults();
        AssetDatabase.CreateAsset(created, ConfigPath);
<<<<<<< HEAD
        BindRainLoop(created);
        BindMusicLoop(created);
=======
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("已创建默认 GameConfig：" + ConfigPath);
        return created;
    }
<<<<<<< HEAD

    /// <summary>把雨声循环挂到 GameConfig.rainLoop（已经挂上就不动）。</summary>
    private static void BindRainLoop(GameConfig config)
    {
        if (config == null) return;

        AudioClip rain = AssetDatabase.LoadAssetAtPath<AudioClip>(RainLoopPath);
        if (rain == null || config.rainLoop == rain) return;

        config.rainLoop = rain;
        EditorUtility.SetDirty(config);
    }

    /// <summary>把背景音乐挂到 GameConfig.musicLoop（已经挂上就不动）。</summary>
    private static void BindMusicLoop(GameConfig config)
    {
        if (config == null) return;

        AudioClip music = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicLoopPath);
        if (music == null || config.musicLoop == music) return;

        config.musicLoop = music;
        EditorUtility.SetDirty(config);
    }

    public static GameText EnsureGameTextAsset()
    {
        GameText existing = AssetDatabase.LoadAssetAtPath<GameText>(TextPath);

        if (existing != null)
        {
            // ApplyDefaults 只填空字段，不会覆盖已经改过的文案
            existing.ApplyDefaults();
            EditorUtility.SetDirty(existing);
            return existing;
        }

        EnsureResourceFolder();
        GameText created = ScriptableObject.CreateInstance<GameText>();
        created.ApplyDefaults();
        AssetDatabase.CreateAsset(created, TextPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("已创建文案表：" + TextPath);
        return created;
    }

    public static SfxLibrary EnsureSfxLibraryAsset()
    {
        EnsureResourceFolder();

        SfxLibrary library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(SfxLibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<SfxLibrary>();
            AssetDatabase.CreateAsset(library, SfxLibraryPath);
        }

        library.entries = BuildSfxEntries();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("已生成音效库：" + SfxLibraryPath + "（共 " + library.entries.Count + " 条）");
        return library;
    }

    private static void EnsureResourceFolder()
    {
        if (!AssetDatabase.IsValidFolder(ResourceFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
    }

    private static List<SfxLibrary.SfxEntry> BuildSfxEntries()
    {
        return new List<SfxLibrary.SfxEntry>
        {
            Entry(SfxId.Touch, 1f, Clip("01_触碰交互/touch_01_confirmation_001.ogg")),
            Entry(SfxId.ItemPlace, 1f, Clip("02_物品放置/place_01_wood_light_000.ogg")),
            // 收走类素材本身偏轻（索引里说 -39dB），做一点音量补偿
            Entry(SfxId.ItemTake, 1.8f, Clip("03_物品收走/take_02_handle_small_leather2.ogg")),
            Entry(SfxId.DoorOpen, 1f, Clip("04_开关门/door_open_01.ogg")),
            Entry(SfxId.DoorClose, 1f, Clip("04_开关门/door_close_01.ogg")),
            Entry(SfxId.FootstepWood, 1f,
                Clip("05_走路脚步/footstep_wood_01.ogg"),
                Clip("05_走路脚步/footstep_wood_02.ogg"),
                Clip("05_走路脚步/footstep_wood_03.ogg"),
                Clip("05_走路脚步/footstep_wood_04.ogg"),
                Clip("05_走路脚步/footstep_wood_05.ogg")),
            Entry(SfxId.FootstepConcrete, 1f,
                Clip("05_走路脚步/footstep_concrete_01.ogg"),
                Clip("05_走路脚步/footstep_concrete_02.ogg"),
                Clip("05_走路脚步/footstep_concrete_03.ogg")),
            Entry(SfxId.FootstepCarpet, 1f,
                Clip("05_走路脚步/footstep-carpet-01.ogg"),
                Clip("05_走路脚步/footstep_carpet_02.ogg"),
                Clip("05_走路脚步/footstep_carpet_03.ogg")),
            Entry(SfxId.Warn, 1f, Clip("06_警告提示/warn_02_error_003.ogg")),
            Entry(SfxId.WarnSoft, 1f, Clip("06_警告提示/warn_01_error_001.ogg"))
        };
    }

    private static SfxLibrary.SfxEntry Entry(SfxId id, float volumeScale, params AudioClip[] clips)
    {
        SfxLibrary.SfxEntry entry = new SfxLibrary.SfxEntry();
        entry.id = id;
        entry.volumeScale = volumeScale;
        entry.clips = clips;
        return entry;
    }

    private static AudioClip Clip(string relativePath)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>(SfxRoot + "/" + relativePath);
    }
=======
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
}
