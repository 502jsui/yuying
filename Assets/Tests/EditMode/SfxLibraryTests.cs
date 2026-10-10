using NUnit.Framework;
using UnityEngine;

/// <summary>音效库资产是否齐全、雨声是否挂上。</summary>
public class SfxLibraryTests
{
    private static readonly SfxId[] Required =
    {
        SfxId.Touch,
        SfxId.ItemPlace,
        SfxId.ItemTake,
        SfxId.DoorOpen,
        SfxId.DoorClose,
        SfxId.FootstepWood,
        SfxId.FootstepConcrete,
        SfxId.Warn,
        SfxId.WarnSoft
    };

    [Test]
    public void ResourcesLibrary_Exists()
    {
        SfxLibrary library = SfxLibrary.Load();
        Assert.IsNotNull(library, "Resources/SfxLibrary.asset 应该存在（跑 余音/创建或选中音效库 生成）");
    }

    [Test]
    public void Library_CoversEveryIdUsedByAudioDirector()
    {
        SfxLibrary library = SfxLibrary.Load();
        Assert.IsNotNull(library);

        for (int i = 0; i < Required.Length; i++)
        {
            Assert.IsTrue(library.HasClip(Required[i]), "缺少音效：" + Required[i]);
        }
    }

    [Test]
    public void PickClip_ReturnsAVariantOfFootstepWood()
    {
        SfxLibrary library = SfxLibrary.Load();
        Assert.IsNotNull(library);

        SfxLibrary.SfxEntry entry = library.GetEntry(SfxId.FootstepWood);
        Assert.IsNotNull(entry);
        Assert.AreEqual(5, entry.clips.Length, "木头脚步应该有 5 个变体");

        AudioClip clip = library.PickClip(SfxId.FootstepWood);
        Assert.IsNotNull(clip);
        CollectionAssert.Contains(entry.clips, clip);
    }

    [Test]
    public void GameConfig_HasRainLoopBound()
    {
        GameConfig config = GameConfig.Load();
        Assert.IsNotNull(config);
        Assert.IsNotNull(config.rainLoop, "GameConfig.rainLoop 应该指向雨声循环 ogg");
    }

    [Test]
    public void GameConfig_HasMusicLoopBound()
    {
        GameConfig config = GameConfig.Load();
        Assert.IsNotNull(config);
        Assert.IsNotNull(config.musicLoop, "GameConfig.musicLoop 应该指向钢琴 BGM ogg");
    }

    [Test]
    public void TakeSound_HasVolumeCompensation()
    {
        SfxLibrary library = SfxLibrary.Load();
        Assert.IsNotNull(library);
        // 索引里说收走类素材偏轻，约定做 +音量 补偿
        Assert.Greater(library.GetVolumeScale(SfxId.ItemTake), 1f);
    }
}
