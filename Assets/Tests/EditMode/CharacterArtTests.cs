using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 角色美术接入的自测：配置、导入设置、prefab 上的实际尺寸。
/// 对应的工具是 Assets/Scripts/Editor/YuyinArtTool.cs。
/// </summary>
public class CharacterArtTests
{
    private const string ConfigPath = "Assets/Resources/CharacterArt.asset";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string GrandmaPrefabPath = "Assets/Prefabs/Resident_Grandma.prefab";
    private const string UnclePrefabPath = "Assets/Prefabs/Resident_Uncle.prefab";
    private const string GirlPrefabPath = "Assets/Prefabs/Resident_Girl.prefab";
    private const string MotherPrefabPath = "Assets/Prefabs/Resident_Mother.prefab";
    private const string PlayerSpritePath = "Assets/Art/Characters/Player.png";
    private const string GrandmaSittingPath = "Assets/Art/Characters/Grandma_Sitting.png";
    private const string UncleSittingPath = "Assets/Art/Characters/Uncle_Sitting.png";
    private const string GrandmaWalkingPath = "Assets/Art/Characters/Grandma_Walking.png";
    private const string UncleWalkingPath = "Assets/Art/Characters/Uncle_Walking.png";
    private const string GrandmaTalkingPath = "Assets/Art/Characters/Grandma_Talking.png";
    private const string UncleTalkingPath = "Assets/Art/Characters/Uncle_Talking.png";

    [Test]
    public void Config_PlayerEntry_MatchesDesign()
    {
        CharacterArtConfig config = AssetDatabase.LoadAssetAtPath<CharacterArtConfig>(ConfigPath);
        Assert.IsNotNull(config, "应该有 Assets/Resources/CharacterArt.asset");

        CharacterArtEntry player = config.PlayerEntry;
        Assert.IsNotNull(player.sprite, "主角立绘应该已经绑上");
        StringAssert.Contains("Player", player.sprite.name);
        Assert.AreEqual(2.2f, player.height, 0.001f);
        Assert.AreEqual(-0.5f, player.footOffsetY, 0.001f);
        Assert.AreEqual(5, player.sortingOrder);
        Assert.IsTrue(player.flipWithMovement, "主角要按左右方向翻转");
        Assert.IsFalse(player.tintByWarmth, "主角立绘不染暖意色");
    }

    [Test]
    public void Sprite_ImportSettings_AreSpriteForCharacterArt()
    {
        TextureImporter importer = AssetImporter.GetAtPath(PlayerSpritePath) as TextureImporter;
        Assert.IsNotNull(importer, "找不到 " + PlayerSpritePath);

        Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
        Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
        Assert.AreEqual(100f, importer.spritePixelsPerUnit, 0.001f);
        Assert.IsTrue(importer.alphaIsTransparency, "立绘要勾 Alpha Is Transparency");
        Assert.IsFalse(importer.mipmapEnabled);

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        Assert.AreEqual((int)SpriteAlignment.BottomCenter, settings.spriteAlignment,
            "立绘枢轴要贴底边（脚底对齐）");
    }

    [Test]
    public void SittingArt_IsBoundForGrandmaAndUncle()
    {
        CharacterArtConfig config = AssetDatabase.LoadAssetAtPath<CharacterArtConfig>(ConfigPath);
        Assert.IsNotNull(config);

        CharacterArtEntry grandma = config.ForResidentSitting(ResidentId.Grandma);
        CharacterArtEntry uncle = config.ForResidentSitting(ResidentId.Uncle);

        Assert.IsNotNull(grandma.sprite);
        Assert.IsNotNull(uncle.sprite);
        StringAssert.Contains("Grandma_Sitting", grandma.sprite.name);
        StringAssert.Contains("Uncle_Sitting", uncle.sprite.name);
        Assert.AreEqual(1.8f, grandma.height, 0.001f);
        Assert.AreEqual(2.1f, uncle.height, 0.001f);
        Assert.IsFalse(grandma.tintByWarmth, "坐姿成品图不叠暖意染色");
        Assert.IsFalse(uncle.tintByWarmth, "坐姿成品图不叠暖意染色");
    }

    [Test]
    public void SittingSprites_UseCharacterArtImportSettings()
    {
        AssertSittingImporter(GrandmaSittingPath);
        AssertSittingImporter(UncleSittingPath);
    }

    [Test]
    public void WalkingAndTalkingArt_IsBoundForGrandmaAndUncle()
    {
        CharacterArtConfig config = AssetDatabase.LoadAssetAtPath<CharacterArtConfig>(ConfigPath);
        Assert.IsNotNull(config);

        AssertPose(config, ResidentId.Grandma, NpcVisualPose.Walking, "Grandma_Walking", 2.0f);
        AssertPose(config, ResidentId.Uncle, NpcVisualPose.Walking, "Uncle_Walking", 2.3f);
        AssertPose(config, ResidentId.Grandma, NpcVisualPose.Talking, "Grandma_Talking", 2.0f);
        AssertPose(config, ResidentId.Uncle, NpcVisualPose.Talking, "Uncle_Talking", 2.3f);
    }

    [Test]
    public void WalkingAndTalkingSprites_UseCharacterArtImportSettings()
    {
        AssertSittingImporter(GrandmaWalkingPath);
        AssertSittingImporter(UncleWalkingPath);
        AssertSittingImporter(GrandmaTalkingPath);
        AssertSittingImporter(UncleTalkingPath);
    }

    [Test]
    public void PlayerPrefab_Body_UsesArtAtDesignHeight()
    {
        SpriteRenderer body = FindBody(PlayerPrefabPath);
        Assert.IsNotNull(body, "Player.prefab 应该有 Body");
        Assert.IsNotNull(body.sprite, "Body 应该有立绘 sprite");
        StringAssert.Contains("Player", body.sprite.name);

        Assert.AreEqual(5, body.sortingOrder, "立绘要排在门（order 0）前面");
        Assert.AreEqual(-0.5f, body.transform.localPosition.y, 0.001f,
            "立绘底边应该落在 transform 下方 0.5");
        AssertColorWhite(body.color);

        Vector2 size = CharacterArtApplier.ResolveWorldSize(body);
        Assert.AreEqual(2.2f, size.y, 0.02f, "主角立绘在世界里应该高 2.2");
        Assert.AreEqual(2.2f * 458f / 875f, size.x, 0.05f, "宽度应该按图片比例");

        PlayerController controller = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath)
            .GetComponent<PlayerController>();
        Assert.IsNotNull(controller);
        Assert.AreSame(body, controller.bodyRenderer, "bodyRenderer 要接上，走路翻转才生效");
        Assert.IsTrue(controller.flipWithMovement);
    }

    [Test]
    public void ResidentPrefabs_UseImageArt()
    {
        AssertResidentPrefab(GrandmaPrefabPath, "Grandma_Talking", 2.0f);
        AssertResidentPrefab(UnclePrefabPath, "Uncle_Talking", 2.3f);
        AssertResidentPrefab(GirlPrefabPath, "Girl_Standing", 1.4f);
        AssertResidentPrefab(MotherPrefabPath, "Mother_Standing", 2.1f);
    }

    [Test]
    public void ResolveWorldBottom_IsFootLineInPrefabSpace()
    {
        SpriteRenderer body = FindBody(PlayerPrefabPath);
        float bottom = CharacterArtApplier.ResolveWorldBottom(body);

        // prefab 里 root 在原点，所以底边就等于 Body 的枢轴位置。
        Assert.AreEqual(-0.5f, bottom, 0.01f);
    }

    private static SpriteRenderer FindBody(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.IsNotNull(prefab, "找不到 " + prefabPath);
        return CharacterArtApplier.FindBody(prefab.transform);
    }

    private static void AssertColorWhite(Color color)
    {
        Assert.AreEqual(1f, color.r, 0.001f);
        Assert.AreEqual(1f, color.g, 0.001f);
        Assert.AreEqual(1f, color.b, 0.001f);
        Assert.AreEqual(1f, color.a, 0.001f);
    }

    private static void AssertSittingImporter(string path)
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

    private static void AssertResidentPrefab(string prefabPath, string spriteName, float height)
    {
        SpriteRenderer body = FindBody(prefabPath);
        Assert.IsNotNull(body, prefabPath);
        Assert.IsNotNull(body.sprite, prefabPath + " 不应再使用空 sprite");
        StringAssert.Contains(spriteName, body.sprite.name);
        Assert.AreEqual(height, CharacterArtApplier.ResolveWorldSize(body).y, 0.02f);
        AssertColorWhite(body.color);
    }

    private static void AssertPose(CharacterArtConfig config, ResidentId id, NpcVisualPose pose, string spriteName, float height)
    {
        CharacterArtEntry entry = pose == NpcVisualPose.Walking
            ? config.ForResidentWalking(id)
            : config.ForResidentTalking(id);
        Assert.IsNotNull(entry);
        Assert.IsNotNull(entry.sprite);
        StringAssert.Contains(spriteName, entry.sprite.name);
        Assert.AreEqual(height, entry.height, 0.001f);
        Assert.IsFalse(entry.tintByWarmth);
    }
}
