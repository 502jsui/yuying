using UnityEditor;
using UnityEngine;

/// <summary>
/// 音频导入设置：
///   - Assets/Audio/SFX 下的短音效：解压到内存 + 预加载（避免第一次响的时候卡一下）
///   - Assets/Audio/Ambience 下的环境循环：压缩留在内存，不预加载
/// </summary>
public class SfxPostprocessor : AssetPostprocessor
{
    private const string SfxFolder = "Assets/Audio/SFX/";
    private const string AmbienceFolder = "Assets/Audio/Ambience/";
    private const string MusicFolder = "Assets/Audio/Music/";

    private void OnPreprocessAudio()
    {
        AudioImporter importer = assetImporter as AudioImporter;
        if (importer == null) return;

        if (assetPath.StartsWith(SfxFolder))
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            return;
        }

        if (assetPath.StartsWith(AmbienceFolder) || assetPath.StartsWith(MusicFolder))
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
        }
    }
}
