using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>物品正视/侧视素材、ItemArt 配置和导入设置。</summary>
public class ItemArtTests
{
    private static readonly string[] ItemIds =
    {
        "grandma_01", "grandma_02", "grandma_03",
        "aunt_01", "aunt_02", "aunt_03",
        "mother_01", "mother_02", "mother_03"
    };

    [Test]
    public void Config_HasFrontAndSideForEveryItem()
    {
        ItemArtConfig config = AssetDatabase.LoadAssetAtPath<ItemArtConfig>("Assets/Resources/ItemArt.asset");
        Assert.IsNotNull(config);

        for (int i = 0; i < ItemIds.Length; i++)
        {
            ItemArtEntry entry = config.Get(ItemIds[i]);
            Assert.IsNotNull(entry, ItemIds[i]);
            Assert.IsNotNull(entry.frontSprite, ItemIds[i] + " 缺少正视");
            Assert.IsNotNull(entry.sideSprite, ItemIds[i] + " 缺少侧视");
            StringAssert.Contains(ItemIds[i] + "_Front", entry.frontSprite.name);
            StringAssert.Contains(ItemIds[i] + "_Side", entry.sideSprite.name);
            Assert.Greater(entry.worldHeight, 0.1f);
        }
    }

    [Test]
    public void AllItemSprites_UseSpriteImportSettings()
    {
        for (int i = 0; i < ItemIds.Length; i++)
        {
            AssertImporter("Assets/Art/Items/" + ItemIds[i] + "_Front.png");
            AssertImporter("Assets/Art/Items/" + ItemIds[i] + "_Side.png");
        }
    }

    private static void AssertImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Assert.IsNotNull(importer, "找不到 " + path);
        Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
        Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
        Assert.AreEqual(100f, importer.spritePixelsPerUnit, 0.001f);
        Assert.IsTrue(importer.alphaIsTransparency);
        Assert.IsFalse(importer.mipmapEnabled);

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        Assert.AreEqual((int)SpriteAlignment.BottomCenter, settings.spriteAlignment);
    }
}
