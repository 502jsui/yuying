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
    private const string GirlPrefabPath = "Assets/Prefabs/Resident_Girl.prefab";
    private const string PlayerSpritePath = "Assets/Art/Characters/Player.png";

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
    public void ResidentPrefabs_StayOnPlaceholderBars()
    {
        // 住户还没给美术，占位方块的尺寸必须和 YuyinNpcSceneTool 的规格一致。
        SpriteRenderer girl = FindBody(GirlPrefabPath);
        Assert.IsNotNull(girl);

        Vector2 size = CharacterArtApplier.ResolveWorldSize(girl);
        Assert.AreEqual(0.6f, size.x, 0.01f);
        Assert.AreEqual(1.4f, size.y, 0.01f);

        ResidentView view = AssetDatabase.LoadAssetAtPath<GameObject>(GirlPrefabPath)
            .GetComponent<ResidentView>();
        Assert.IsTrue(view.tintBodyByWarmth, "占位方块继续用暖意染色做状态提示");
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
}
