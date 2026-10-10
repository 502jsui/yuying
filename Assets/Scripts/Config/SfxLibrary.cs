using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音效表：SfxId → 一个或多个 AudioClip（多个就是随机变体，比如脚步）。
/// 资产在 Assets/Resources/SfxLibrary.asset，由菜单 余音/创建或选中音效库 生成。
/// </summary>
[CreateAssetMenu(menuName = "余音/音效库", fileName = "SfxLibrary")]
public class SfxLibrary : ScriptableObject
{
    [Serializable]
    public class SfxEntry
    {
        public SfxId id;
        public AudioClip[] clips;

        [Tooltip("整体音量补偿：收走类素材偏轻，可以调大一点。")]
        public float volumeScale = 1f;
    }

    public List<SfxEntry> entries = new List<SfxEntry>();

    /// <summary>从 Resources 读取；没有资产时返回 null（调用方自己兜底）。</summary>
    public static SfxLibrary Load()
    {
        return Resources.Load<SfxLibrary>("SfxLibrary");
    }

    public SfxEntry GetEntry(SfxId id)
    {
        if (entries == null) return null;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].id == id) return entries[i];
        }
        return null;
    }

    public bool HasClip(SfxId id)
    {
        SfxEntry entry = GetEntry(id);
        if (entry == null || entry.clips == null) return false;
        for (int i = 0; i < entry.clips.Length; i++)
        {
            if (entry.clips[i] != null) return true;
        }
        return false;
    }

    /// <summary>随机取一条变体（没有配置时返回 null）。</summary>
    public AudioClip PickClip(SfxId id)
    {
        SfxEntry entry = GetEntry(id);
        if (entry == null || entry.clips == null || entry.clips.Length == 0) return null;

        if (entry.clips.Length == 1) return entry.clips[0];

        int start = UnityEngine.Random.Range(0, entry.clips.Length);
        for (int i = 0; i < entry.clips.Length; i++)
        {
            AudioClip clip = entry.clips[(start + i) % entry.clips.Length];
            if (clip != null) return clip;
        }
        return null;
    }

    public float GetVolumeScale(SfxId id)
    {
        SfxEntry entry = GetEntry(id);
        return entry != null ? Mathf.Clamp(entry.volumeScale, 0f, 4f) : 1f;
    }

    /// <summary>把所有条目按 id 排列好（编辑器生成资产时用）。</summary>
    public void SortById()
    {
        if (entries == null) return;
        entries.Sort(delegate (SfxEntry a, SfxEntry b) { return a.id.CompareTo(b.id); });
    }
}
