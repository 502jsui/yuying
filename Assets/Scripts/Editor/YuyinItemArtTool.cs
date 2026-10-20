using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>生成 ItemArt 配置，并把每件物品的正视图刷进场景。</summary>
public static class YuyinItemArtTool
{
    private const string ArtFolder = "Assets/Art/Items";
    private const string ConfigPath = "Assets/Resources/ItemArt.asset";
    private static readonly string[] ItemIds =
    {
        "grandma_01", "grandma_02", "grandma_03",
        "aunt_01", "aunt_02", "aunt_03",
        "mother_01", "mother_02", "mother_03"
    };
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/FirstFloor.unity",
        "Assets/Scenes/SecondFloor.unity",
        "Assets/Scenes/Backyard.unity"
    };

    [MenuItem("Tools/余音/刷新物品美术配置")]
    public static void RefreshFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Refresh();
    }

    public static void RefreshFromCommandLine()
    {
        try
        {
            Refresh();
            EditorApplication.Exit(0);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void Refresh()
    {
        EnsureSpriteImportSettings();
        ItemArtConfig config = EnsureConfig();
        ApplyScenes(config);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[余音·物品美术] ItemArt 配置与场景物品已刷新。");
    }

    private static ItemArtConfig EnsureConfig()
    {
        ItemArtConfig config = AssetDatabase.LoadAssetAtPath<ItemArtConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<ItemArtConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        if (config.items == null) config.items = new List<ItemArtEntry>();
        for (int i = 0; i < ItemIds.Length; i++)
        {
            ItemArtEntry entry = config.Get(ItemIds[i]);
            if (entry == null)
            {
                entry = ItemArtConfig.DefaultEntry(ItemIds[i]);
                config.items.Add(entry);
            }

            ItemArtEntry defaults = ItemArtConfig.DefaultEntry(ItemIds[i]);
            entry.worldHeight = defaults.worldHeight;
            entry.hangFromTop = defaults.hangFromTop;
            entry.sortingOrder = defaults.sortingOrder;
            entry.frontSprite = LoadSprite(ItemIds[i] + "_Front");
            entry.sideSprite = LoadSprite(ItemIds[i] + "_Side");
        }

        EditorUtility.SetDirty(config);
        return config;
    }

    private static Sprite LoadSprite(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + name + ".png");
    }

    private static void ApplyScenes(ItemArtConfig config)
    {
        for (int i = 0; i < ScenePaths.Length; i++)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePaths[i], OpenSceneMode.Single);
            ItemView[] views = Object.FindObjectsOfType<ItemView>(true);
            for (int v = 0; v < views.Length; v++)
            {
                ItemView view = views[v];
                ItemArtEntry entry = config.Get(view.itemId);
                if (entry == null || entry.frontSprite == null) continue;

                SpriteRenderer body = view.spriteRenderer;
                if (body == null) continue;

                body.sprite = entry.frontSprite;
                body.color = Color.white;
                body.sortingOrder = entry.sortingOrder;
                float scale = entry.worldHeight / Mathf.Max(0.0001f, entry.frontSprite.bounds.size.y);
                body.transform.localScale = new Vector3(scale, scale, 1f);
                body.transform.localPosition = new Vector3(0f, entry.hangFromTop ? -entry.worldHeight : 0f, 0f);

                BoxCollider2D box = view.GetComponent<BoxCollider2D>();
                if (box != null)
                {
                    box.size = new Vector2(entry.frontSprite.bounds.size.x * scale * 0.85f, entry.worldHeight * 0.9f);
                    box.offset = new Vector2(0f, entry.hangFromTop ? -entry.worldHeight * 0.5f : entry.worldHeight * 0.5f);
                    EditorUtility.SetDirty(box);
                }

                EditorUtility.SetDirty(body);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[余音·物品美术] " + ScenePaths[i] + " 已刷新 " + views.Length + " 件物品。");
        }
    }

    private static void EnsureSpriteImportSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f)) { importer.spritePixelsPerUnit = 100f; dirty = true; }
            if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
            if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; dirty = true; }
            if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; dirty = true; }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteAlignment != (int)SpriteAlignment.BottomCenter)
            {
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.spriteMeshType = SpriteMeshType.Tight;
                importer.SetTextureSettings(settings);
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
        }
    }
}
