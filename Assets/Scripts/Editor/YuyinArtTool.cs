using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 角色美术接入工具：按 CharacterArtConfig 把立绘刷进 prefab 与场景，并提供检查 / 预览渲染。
///
/// 用法：
///   1. 把立绘放进 Assets/Art/Characters/（透明底、紧裁切、脚底贴图片底边）；
///   2. Tools → 余音 → 创建或选中角色美术配置，把 sprite 拖进对应角色；
///   3. Tools → 余音 → 应用角色美术（全部场景）。
///
/// 批处理：
///   Unity.exe -batchmode -nographics -quit -projectPath &lt;项目&gt; -executeMethod YuyinArtTool.SetupFromCommandLine
///   Unity.exe -batchmode -nographics -quit -projectPath &lt;项目&gt; -executeMethod YuyinArtTool.ApplyAllScenesFromCommandLine
///   Unity.exe -executeMethod YuyinArtTool.RenderPreviewFromCommandLine -artScene FirstFloor   （要真显卡，不能带 -nographics）
/// </summary>
public static class YuyinArtTool
{
    private const string ResourceFolder = "Assets/Resources";
    private const string ConfigPath = ResourceFolder + "/CharacterArt.asset";
    private const string CharacterArtFolder = "Assets/Art/Characters";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string PreviewFolder = "Logs";

    private static readonly string[] ResidentPrefabPaths =
    {
        "Assets/Prefabs/Resident_Grandma.prefab",
        "Assets/Prefabs/Resident_Uncle.prefab",
        "Assets/Prefabs/Resident_Girl.prefab",
        "Assets/Prefabs/Resident_Mother.prefab"
    };

    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/FirstFloor.unity",
        "Assets/Scenes/SecondFloor.unity",
        "Assets/Scenes/Backyard.unity"
    };

    // ------------------------------------------------------------------
    // 菜单
    // ------------------------------------------------------------------

    [MenuItem("Tools/余音/创建或选中角色美术配置")]
    public static void CreateOrSelectConfig()
    {
        CharacterArtConfig config = EnsureConfigAsset();
        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
    }

    [MenuItem("Tools/余音/应用角色美术（当前场景）")]
    public static void ApplyCurrentScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureConfigAsset();
        EnsureSpriteImportSettings();
        AutoBindSprites(EnsureConfigAsset());
        AssetDatabase.SaveAssets();

        Scene scene = EditorSceneManager.GetActiveScene();
        int changed = ApplySceneObjects(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[余音·美术] " + scene.name + "：刷新了 " + changed + " 个角色。");
    }

    [MenuItem("Tools/余音/应用角色美术（全部场景）")]
    public static void ApplyAllScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ApplyEverything();
    }

    [MenuItem("Tools/余音/角色美术检查")]
    public static void ReportFromMenu()
    {
        Report();
    }

    [MenuItem("Tools/余音/渲染一张场景预览图")]
    public static void RenderPreviewFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string path;
        RenderPreview(ScenePaths[0], out path);
        Debug.Log("[余音·美术] 预览图：" + path);
    }

    // ------------------------------------------------------------------
    // 批处理入口
    // ------------------------------------------------------------------

    /// <summary>建配置 + 刷导入设置 + 刷 prefab 和全部场景。</summary>
    public static void SetupFromCommandLine()
    {
        try
        {
            ApplyEverything();
            EditorApplication.Exit(0);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void ApplyAllScenesFromCommandLine()
    {
        try
        {
            ApplyEverything();
            EditorApplication.Exit(0);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>只刷新图片导入设置和 CharacterArt 配置，不改任何场景 / Prefab。</summary>
    public static void BindConfigFromCommandLine()
    {
        try
        {
            EnsureSpriteImportSettings();
            CharacterArtConfig config = EnsureConfigAsset();
            AutoBindSprites(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[余音·美术] 角色美术配置已刷新。");
            EditorApplication.Exit(0);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void RenderPreviewFromCommandLine()
    {
        try
        {
            Dictionary<string, string> args = ParseArguments(System.Environment.GetCommandLineArgs());
            string sceneName = GetString(args, "-artScene", "FirstFloor");
            string scenePath = FindScenePath(sceneName);

            string path;
            bool ok = RenderPreview(scenePath, out path);
            EditorApplication.Exit(ok ? 0 : 1);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    // ------------------------------------------------------------------
    // 主流程
    // ------------------------------------------------------------------

    private static void ApplyEverything()
    {
        EnsureConfigAsset();
        AssetDatabase.SaveAssets();

        EnsureSpriteImportSettings();
        AutoBindSprites(EnsureConfigAsset());
        AssetDatabase.SaveAssets();

        ApplyPrefabs();

        for (int i = 0; i < ScenePaths.Length; i++)
        {
            if (!File.Exists(ScenePaths[i])) continue;

            Scene scene = EditorSceneManager.OpenScene(ScenePaths[i], OpenSceneMode.Single);
            int changed = ApplySceneObjects(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[余音·美术] " + ScenePaths[i] + "：刷新了 " + changed + " 个角色。");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Report();
    }

    private static void ApplyPrefabs()
    {
        CharacterArtConfig config = EnsureConfigAsset();
        ApplyPlayerPrefab(config);

        for (int i = 0; i < ResidentPrefabPaths.Length; i++)
        {
            string path = ResidentPrefabPaths[i];
            if (!File.Exists(path)) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ResidentView view = root.GetComponent<ResidentView>();
                if (view == null) continue;

                NpcPlacementSpec spec = FindSpecAnyArea(view.id);
                if (spec.id != view.id)
                {
                    Debug.LogWarning("[余音·美术] 找不到住户预制体规格：" + path);
                    continue;
                }
                CharacterArtApplier.ApplyResident(view, spec, config);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void ApplyPlayerPrefab(CharacterArtConfig config)
    {
        if (!File.Exists(PlayerPrefabPath)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            CharacterArtApplier.ApplyPlayer(root, config);

            // prefab 放在原点：位置由场景实例各自覆盖（场景重建器也会显式设位置）。
            // 这里顺手清掉历史上留下的坏坐标，避免直接实例化时跑到地下。
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>刷一个场景里的主角 + 全部住户，返回改了几个。</summary>
    private static int ApplySceneObjects(Scene scene)
    {
        // 注意：打开 / 保存场景会让之前抓到的资产引用失效（UnityEngine.Object 的 fake null），
        // 所以每一步都重新从 AssetDatabase 拿一次配置。
        CharacterArtConfig config = EnsureConfigAsset();
        int changed = 0;

        PlayerController[] players = Object.FindObjectsOfType<PlayerController>(true);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].gameObject.scene != scene) continue;
            if (CharacterArtApplier.ApplyPlayer(players[i].gameObject, config)) changed++;
        }

        ResidentView[] residents = Object.FindObjectsOfType<ResidentView>(true);
        for (int i = 0; i < residents.Length; i++)
        {
            ResidentView view = residents[i];
            if (view.gameObject.scene != scene) continue;

            List<NpcPlacementSpec> specs = YuyinNpcSceneTool.GetSpecs(view.area);
            CharacterArtApplier.ApplyResident(view, FindSpec(specs, view.id), config);
            changed++;
        }

        return changed;
    }

    private static NpcPlacementSpec FindSpec(List<NpcPlacementSpec> specs, ResidentId id)
    {
        for (int i = 0; i < specs.Count; i++)
        {
            if (specs[i].id == id) return specs[i];
        }
        return specs.Count > 0 ? specs[0] : default(NpcPlacementSpec);
    }

    private static NpcPlacementSpec FindSpecAnyArea(ResidentId id)
    {
        AreaId[] areas = { AreaId.FirstFloor, AreaId.SecondFloor, AreaId.Backyard };
        for (int a = 0; a < areas.Length; a++)
        {
            List<NpcPlacementSpec> specs = YuyinNpcSceneTool.GetSpecs(areas[a]);
            for (int i = 0; i < specs.Count; i++)
            {
                if (specs[i].id == id) return specs[i];
            }
        }
        return default(NpcPlacementSpec);
    }

    // ------------------------------------------------------------------
    // 配置资产 + 导入设置
    // ------------------------------------------------------------------

    public static CharacterArtConfig EnsureConfigAsset()
    {
        CharacterArtConfig existing = AssetDatabase.LoadAssetAtPath<CharacterArtConfig>(ConfigPath);
        if (existing != null)
        {
            existing.EnsureDefaults();
            EditorUtility.SetDirty(existing);
            return existing;
        }

        if (!AssetDatabase.IsValidFolder(ResourceFolder)) AssetDatabase.CreateFolder("Assets", "Resources");

        CharacterArtConfig created = ScriptableObject.CreateInstance<CharacterArtConfig>();
        created.EnsureDefaults();
        AssetDatabase.CreateAsset(created, ConfigPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("已创建角色美术配置：" + ConfigPath);
        return created;
    }

    /// <summary>
    /// 按约定文件名自动绑定立绘（只在配置里留空时生效，不覆盖手动指定的）：
    /// 站立：Player / Grandma / Uncle / Girl / Mother；坐姿：Grandma_Sitting / Uncle_Sitting。
    /// </summary>
    private static void AutoBindSprites(CharacterArtConfig config)
    {
        if (config == null || !AssetDatabase.IsValidFolder(CharacterArtFolder)) return;

        AutoBind(config, config.PlayerEntry, "Player");

        ResidentId[] ids = { ResidentId.Grandma, ResidentId.Uncle, ResidentId.Girl, ResidentId.Mother };
        for (int i = 0; i < ids.Length; i++)
        {
            CharacterArtEntry standing = config.ForResident(ids[i]);
            AutoBind(config, standing, FileNameFor(ids[i]));
            if (standing.sprite == null)
                AutoBind(config, standing, FileNameFor(ids[i]) + "_Standing");

            AutoBindPose(config, config.ForResidentSitting(ids[i]), FileNameFor(ids[i]) + "_Sitting");
            AutoBindPose(config, config.ForResidentWalking(ids[i]), FileNameFor(ids[i]) + "_Walking");
            CharacterArtEntry talking = config.ForResidentTalking(ids[i]);
            AutoBindPose(config, talking, FileNameFor(ids[i]) + "_Talking");

            if (talking.sprite == null && standing.sprite != null)
                CopyPose(standing, talking, config);
            if (standing.sprite == null && talking.sprite != null)
                CopyPose(talking, standing, config);
        }
    }

    private static void CopyPose(CharacterArtEntry source, CharacterArtEntry target, CharacterArtConfig config)
    {
        if (source == null || target == null) return;
        target.sprite = source.sprite;
        target.height = source.height;
        target.footOffsetY = source.footOffsetY;
        target.sortingOrder = source.sortingOrder;
        target.tintByWarmth = false;
        EditorUtility.SetDirty(config);
    }

    private static void AutoBindPose(CharacterArtConfig config, CharacterArtEntry entry, string fileName)
    {
        if (entry == null || entry.sprite != null) return;

        string path = CharacterArtFolder + "/" + fileName + ".png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) return;

        entry.sprite = sprite;
        EditorUtility.SetDirty(config);
        Debug.Log("[余音·美术] 自动绑定动作立绘：" + path);
    }

    private static void AutoBind(CharacterArtConfig config, CharacterArtEntry entry, string fileName)
    {
        if (entry == null || entry.sprite != null) return;

        string path = CharacterArtFolder + "/" + fileName + ".png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) return;

        entry.sprite = sprite;
        EditorUtility.SetDirty(config);
        Debug.Log("[余音·美术] 自动绑定立绘：" + path);
    }

    private static string FileNameFor(ResidentId id)
    {
        switch (id)
        {
            case ResidentId.Grandma: return "Grandma";
            case ResidentId.Uncle: return "Uncle";
            case ResidentId.Girl: return "Girl";
            case ResidentId.Mother: return "Mother";
            default: return id.ToString();
        }
    }

    /// <summary>把 Assets/Art/Characters 下的贴图统一成 2D 立绘的导入设置（幂等）。</summary>
    public static void EnsureSpriteImportSettings()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art")) return;
        if (!AssetDatabase.IsValidFolder(CharacterArtFolder)) return;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { CharacterArtFolder });
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
            if (importer.textureCompression != TextureImporterCompression.Compressed) { importer.textureCompression = TextureImporterCompression.Compressed; dirty = true; }
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

            if (dirty)
            {
                importer.SaveAndReimport();
                Debug.Log("[余音·美术] 已统一导入设置：" + path);
            }
        }
    }

    // ------------------------------------------------------------------
    // 检查 / 预览
    // ------------------------------------------------------------------

    private static void Report()
    {
        CharacterArtConfig config = EnsureConfigAsset();
        Debug.Log("==== 角色美术检查 ====");
        LogEntry("主角", config != null ? config.PlayerEntry : null);
        for (int i = 0; i < ScenePaths.Length; i++)
        {
            if (!File.Exists(ScenePaths[i])) continue;

            Scene scene = EditorSceneManager.OpenScene(ScenePaths[i], OpenSceneMode.Single);
            Debug.Log("-- 场景 " + scene.name + " --");

            PlayerController[] players = Object.FindObjectsOfType<PlayerController>(true);
            for (int p = 0; p < players.Length; p++)
            {
                if (players[p].gameObject.scene != scene) continue;
                SpriteRenderer body = CharacterArtApplier.FindBody(players[p].transform);
                Debug.Log("   主角  sprite=" + SpriteName(body)
                    + "  世界尺寸=" + CharacterArtApplier.ResolveWorldSize(body)
                    + "  底边Y=" + CharacterArtApplier.ResolveWorldBottom(body).ToString("0.00")
                    + "  order=" + (body != null ? body.sortingOrder : 0));
            }

            ResidentView[] residents = Object.FindObjectsOfType<ResidentView>(true);
            for (int r = 0; r < residents.Length; r++)
            {
                ResidentView view = residents[r];
                if (view.gameObject.scene != scene) continue;

                CharacterArtEntry entry = config != null ? config.ForResident(view.id) : null;
                Debug.Log("   " + view.id.ToDisplayName() + "  sprite=" + SpriteName(view.bodyRenderer)
                    + "  世界尺寸=" + CharacterArtApplier.ResolveWorldSize(view.bodyRenderer)
                    + "  底边Y=" + CharacterArtApplier.ResolveWorldBottom(view.bodyRenderer).ToString("0.00")
                    + "  order=" + (view.bodyRenderer != null ? view.bodyRenderer.sortingOrder : 0)
                    + "  染色=" + view.tintBodyByWarmth
                    + (entry != null && !entry.HasArt ? "  （占位方块）" : ""));
            }
        }
        Debug.Log("==== 检查结束 ====");
    }

    private static void LogEntry(string label, CharacterArtEntry entry)
    {
        if (entry == null) { Debug.Log(label + "：没有配置"); return; }
        Debug.Log(label + "：sprite=" + (entry.sprite != null ? entry.sprite.name : "（空 → 占位方块）")
            + "  目标高度=" + entry.height
            + "  底边偏移=" + entry.footOffsetY
            + "  order=" + entry.sortingOrder
            + "  翻转=" + entry.flipWithMovement
            + "  染色=" + entry.tintByWarmth);
    }

    private static string SpriteName(SpriteRenderer renderer)
    {
        if (renderer == null) return "（没有 Body）";
        return renderer.sprite != null ? renderer.sprite.name : "（空）";
    }

    /// <summary>把一个场景按相机取景渲染成 PNG（Edit 模式，需要真显卡）。</summary>
    public static bool RenderPreview(string scenePath, out string outputPath, int width = 1920, int height = 1080)
    {
        outputPath = null;

        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.LogError("[余音·美术] 当前没有渲染设备（-nographics），渲染预览图需要真显卡。");
            return false;
        }
        if (!File.Exists(scenePath))
        {
            Debug.LogError("[余音·美术] 找不到场景：" + scenePath);
            return false;
        }

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        Camera camera = null;
        Camera[] cameras = Object.FindObjectsOfType<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i].CompareTag("MainCamera")) { camera = cameras[i]; break; }
        }
        if (camera == null && cameras.Length > 0) camera = cameras[0];
        if (camera == null)
        {
            Debug.LogError("[余音·美术] 场景里没有相机。");
            return false;
        }

        string fileName = Path.GetFileNameWithoutExtension(scenePath) + "-preview.png";
        outputPath = Path.GetFullPath(Path.Combine(PreviewFolder, fileName));

        RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        Texture2D texture = null;
        try
        {
            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply();

            string folder = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);
            File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            return true;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            if (texture != null) Object.DestroyImmediate(texture);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }

    // ------------------------------------------------------------------
    // 小工具
    // ------------------------------------------------------------------

    private static string FindScenePath(string sceneName)
    {
        for (int i = 0; i < ScenePaths.Length; i++)
        {
            if (Path.GetFileNameWithoutExtension(ScenePaths[i]) == sceneName) return ScenePaths[i];
        }
        return ScenePaths[0];
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        Dictionary<string, string> result = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        if (args == null) return result;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.IsNullOrEmpty(arg) || arg[0] != '-') continue;

            string value = "1";
            if (i + 1 < args.Length && (string.IsNullOrEmpty(args[i + 1]) || args[i + 1][0] != '-'))
            {
                value = args[i + 1];
                i++;
            }
            result[arg] = value;
        }
        return result;
    }

    private static string GetString(Dictionary<string, string> args, string key, string fallback)
    {
        string value;
        return args.TryGetValue(key, out value) && !string.IsNullOrEmpty(value) ? value : fallback;
    }
}
