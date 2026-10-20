using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 一键体检（只读，不改任何东西）：把"配错了就会出怪问题"的地方过一遍，打印 ✓ / ⚠️ / ❌ 清单。
///
/// 菜单：Tools → 余音 → 自检
/// 批处理：-executeMethod YuyinSelfCheckTool.RunFromCommandLine（有 ❌ 时退出码 1）
///
/// 会打开 FirstFloor / SecondFloor / Backyard 场景来数对象（和美术工具一个套路），跑完不会自动切回原场景。
/// </summary>
public static class YuyinSelfCheckTool
{
    private const string ResourceFolder = "Assets/Resources";

    private static readonly string[] RequiredScenes =
    {
        "MainMenu", "FirstFloor", "SecondFloor", "Backyard", "Pause", "Settings", "Credits"
    };

    private static readonly string[] GameScenes = { "FirstFloor", "SecondFloor", "Backyard" };

    private static readonly ResidentId[] ExpectedResidents =
    {
        ResidentId.Grandma, ResidentId.Uncle, ResidentId.Girl
    };

    private static readonly string[] ResidentPrefabs =
    {
        "Assets/Prefabs/Resident_Grandma.prefab",
        "Assets/Prefabs/Resident_Uncle.prefab",
        "Assets/Prefabs/Resident_Girl.prefab",
        "Assets/Prefabs/Resident_Mother.prefab"
    };

    private sealed class Report
    {
        private readonly StringBuilder text = new StringBuilder();

        public int Errors { get; private set; }
        public int Warnings { get; private set; }

        public void Ok(string message)
        {
            text.AppendLine("  ✓ " + message);
        }

        public void Warn(string message)
        {
            Warnings++;
            text.AppendLine("  ⚠️ " + message);
        }

        public void Fail(string message)
        {
            Errors++;
            text.AppendLine("  ❌ " + message);
        }

        public override string ToString()
        {
            return text.ToString();
        }
    }

    [MenuItem("Tools/余音/自检")]
    public static void RunFromMenu()
    {
        int errors = Run();
        Debug.Log(errors == 0
            ? "[余音·自检] 没有 ❌，可以开工。"
            : "[余音·自检] 有 " + errors + " 项 ❌，看上面的清单。");
    }

    public static void RunFromCommandLine()
    {
        int errors = Run();
        EditorApplication.Exit(errors == 0 ? 0 : 1);
    }

    private static int Run()
    {
        Report report = new Report();
        StringBuilder output = new StringBuilder();
        output.AppendLine("==== 余音 · 自检 ====");

        CheckBuildSettings(report);
        CheckConfigs(report);
        CheckPrefabs(report);
        CheckScenes(report);
        CheckMotherConsistency(report);

        output.Append(report.ToString());
        output.AppendLine("==== 结束：❌ " + report.Errors + " 项，⚠️ " + report.Warnings + " 项 ====");

        Debug.Log(output.ToString());
        if (Application.isBatchMode) System.Console.WriteLine(output.ToString());
        return report.Errors;
    }

    // ------------------------------------------------------------------
    // 各项检查
    // ------------------------------------------------------------------

    private static void CheckBuildSettings(Report report)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        List<string> names = new List<string>();
        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i] == null || !scenes[i].enabled) continue;
            names.Add(System.IO.Path.GetFileNameWithoutExtension(scenes[i].path));
        }

        for (int i = 0; i < RequiredScenes.Length; i++)
        {
            if (names.Contains(RequiredScenes[i])) report.Ok("Build Settings 含场景 " + RequiredScenes[i]);
            else report.Fail("Build Settings 缺场景 " + RequiredScenes[i] + "（LoadScene 会静默失败）");
        }

        if (names.Count > 0 && names[0] == "MainMenu") report.Ok("Build Settings 第 0 个是 MainMenu");
        else report.Fail("Build Settings 第 0 个不是 MainMenu（启动会直接进游戏场景）");
    }

    private static void CheckConfigs(Report report)
    {
        string[] paths =
        {
            ResourceFolder + "/GameConfig.asset",
            ResourceFolder + "/GameText.asset",
            ResourceFolder + "/SfxLibrary.asset",
            ResourceFolder + "/CharacterArt.asset",
            ResourceFolder + "/ItemArt.asset"
        };

        for (int i = 0; i < paths.Length; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(paths[i]) != null) report.Ok("配置就位：" + paths[i]);
            else report.Fail("配置缺失：" + paths[i]);
        }

        CharacterArtConfig art = AssetDatabase.LoadAssetAtPath<CharacterArtConfig>(
            ResourceFolder + "/CharacterArt.asset");
        if (art == null) return;

        CharacterArtEntry grandma = art.ForResidentSitting(ResidentId.Grandma);
        CharacterArtEntry uncle = art.ForResidentSitting(ResidentId.Uncle);
        if (grandma != null && grandma.HasArt) report.Ok("老婆婆坐姿立绘就位");
        else report.Fail("老婆婆坐姿立绘缺失（Grandma_Sitting.png）");
        if (uncle != null && uncle.HasArt) report.Ok("大叔坐姿立绘就位");
        else report.Fail("大叔坐姿立绘缺失（Uncle_Sitting.png）");

        CheckPoseArt(report, art, ResidentId.Grandma, "老婆婆", "Grandma");
        CheckPoseArt(report, art, ResidentId.Uncle, "大叔", "Uncle");
        CheckBaseAndWalking(report, art, ResidentId.Girl, "小女孩", "Girl_Standing", "Girl_Walking");
        CheckBaseAndWalking(report, art, ResidentId.Mother, "母亲", "Mother_Standing", "Mother_Walking");

        GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(ResourceFolder + "/GameConfig.asset");
        if (config != null)
        {
            ResidentConfig girl = config.GetResidentConfig(ResidentId.Girl);
            if (girl.homeArea == AreaId.SecondFloor) report.Ok("小女孩 homeArea 已对齐二楼");
            else report.Fail("小女孩 homeArea 应为 SecondFloor");
        }

        ItemArtConfig itemArt = AssetDatabase.LoadAssetAtPath<ItemArtConfig>(
            ResourceFolder + "/ItemArt.asset");
        if (itemArt == null) return;
        string[] itemIds =
        {
            "grandma_01", "grandma_02", "grandma_03",
            "aunt_01", "aunt_02", "aunt_03",
            "mother_01", "mother_02", "mother_03"
        };
        int readyItems = 0;
        for (int i = 0; i < itemIds.Length; i++)
        {
            ItemArtEntry entry = itemArt.Get(itemIds[i]);
            if (entry != null && entry.frontSprite != null && entry.sideSprite != null) readyItems++;
            else report.Fail("物品贴图缺失：" + itemIds[i]);
        }
        if (readyItems == itemIds.Length) report.Ok("9 件物品正视/侧视贴图就位");
    }

    private static void CheckPoseArt(Report report, CharacterArtConfig art, ResidentId id, string label, string fileStem)
    {
        CharacterArtEntry standing = art.ForResident(id);
        CharacterArtEntry walking = art.ForResidentWalking(id);
        CharacterArtEntry talking = art.ForResidentTalking(id);
        if (standing != null && standing.HasArt) report.Ok(label + "默认立绘就位");
        else report.Fail(label + "默认立绘缺失（" + fileStem + "_Talking.png）");
        if (walking != null && walking.HasArt) report.Ok(label + "走路立绘就位");
        else report.Fail(label + "走路立绘缺失（" + fileStem + "_Walking.png）");
        if (talking != null && talking.HasArt) report.Ok(label + "交流立绘就位");
        else report.Fail(label + "交流立绘缺失（" + fileStem + "_Talking.png）");
    }

    private static void CheckBaseAndWalking(
        Report report,
        CharacterArtConfig art,
        ResidentId id,
        string label,
        string standingFile,
        string walkingFile)
    {
        CharacterArtEntry standing = art.ForResident(id);
        CharacterArtEntry walking = art.ForResidentWalking(id);
        if (standing != null && standing.HasArt) report.Ok(label + "默认立绘就位");
        else report.Fail(label + "默认立绘缺失（" + standingFile + ".png）");
        if (walking != null && walking.HasArt) report.Ok(label + "走路立绘就位");
        else report.Fail(label + "走路立绘缺失（" + walkingFile + ".png）");
    }

    private static void CheckPrefabs(Report report)
    {
        for (int i = 0; i < ResidentPrefabs.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResidentPrefabs[i]);
            if (prefab == null)
            {
                report.Fail("找不到预制体：" + ResidentPrefabs[i]);
                continue;
            }

            ResidentView view = prefab.GetComponent<ResidentView>();
            if (view == null)
            {
                report.Fail(prefab.name + " 上没有 ResidentView");
                continue;
            }
            if (view.bodyRenderer == null) report.Fail(prefab.name + " 的 bodyRenderer 是空的");
            else if (view.bodyRenderer.sprite == null) report.Fail(prefab.name + " 的 Body 没有 sprite");
            else report.Ok(prefab.name + " 立绘就位");
        }

        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        if (player == null) report.Fail("找不到 Assets/Prefabs/Player.prefab");
        else
        {
            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller == null || controller.bodyRenderer == null || controller.bodyRenderer.sprite == null)
                report.Fail("Player.prefab 的 bodyRenderer / sprite 没接好");
            else report.Ok("Player.prefab 立绘就位");
        }
    }

    private static void CheckScenes(Report report)
    {
        for (int i = 0; i < GameScenes.Length; i++)
        {
            string path = "Assets/Scenes/" + GameScenes[i] + ".unity";
            if (!System.IO.File.Exists(path))
            {
                report.Fail("找不到场景：" + path);
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CheckScene(report, scene);
        }
    }

    private static void CheckScene(Report report, Scene scene)
    {
        string name = scene.name;

        GameManager manager = Object.FindObjectOfType<GameManager>();
        if (manager == null) report.Fail(name + " 没有 GameManager");
        else report.Ok(name + " 有 GameManager");

        Camera[] cameras = Object.FindObjectsOfType<Camera>();
        int mainCameras = 0;
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i].CompareTag("MainCamera")) mainCameras++;
        }
        if (mainCameras == 0) report.Fail(name + " 没有 MainCamera");
        else report.Ok(name + " 相机就位（MainCamera x" + mainCameras + "）");

        int listeners = Object.FindObjectsOfType<AudioListener>().Length;
        if (listeners == 1) report.Ok(name + " 只有一个 AudioListener");
        else report.Fail(name + " 的 AudioListener 数量是 " + listeners + "（必须正好 1 个）");

        ResidentView[] residents = Object.FindObjectsOfType<ResidentView>();
        ResidentId[] expected = name == "SecondFloor"
            ? new[] { ResidentId.Mother, ResidentId.Girl }
            : name == "FirstFloor"
                ? new[] { ResidentId.Grandma, ResidentId.Uncle }
                : ExpectedResidents;
        for (int i = 0; i < expected.Length; i++)
        {
            ResidentView found = null;
            for (int r = 0; r < residents.Length; r++)
            {
                if (residents[r] != null && residents[r].id == expected[i]) { found = residents[r]; break; }
            }
            if (found == null) report.Fail(name + " 缺住户 " + expected[i].ToDisplayName());
        }
        if (residents.Length > 0) report.Ok(name + " 住户 View 共 " + residents.Length + " 个");

        int doorLights = Object.FindObjectsOfType<DoorLightView>().Length;
        if (doorLights == 0) report.Warn(name + " 没有挂 DoorLightView（门缝光还没接，属于已知缺口）");
        else report.Ok(name + " 门缝光组件 " + doorLights + " 个");

        CheckPanoramaBackdrop(report, name);
        CheckAnchors(report, name);
        CheckPlacementLayout(report, scene);
    }

    private static void CheckPanoramaBackdrop(Report report, string sceneName)
    {
        PanoramaBackdropView[] backdrops = Object.FindObjectsOfType<PanoramaBackdropView>(true);
        if (backdrops.Length != 1)
        {
            report.Fail(sceneName + " 应该正好有 1 个 PanoramaBackdropView，实际 " + backdrops.Length);
            return;
        }

        PanoramaBackdropView backdrop = backdrops[0];
        if (backdrop.spriteRenderer == null || backdrop.spriteRenderer.sprite == null)
        {
            report.Fail(sceneName + " 全景背景没有 Sprite");
            return;
        }
        if (!backdrop.spriteRenderer.enabled)
            report.Fail(sceneName + " 全景背景渲染器被禁用");
        if (backdrop.spriteRenderer.sortingOrder != SceneLayoutMetrics.BackdropSortingOrder)
            report.Fail(sceneName + " 全景背景 sortingOrder 应为 " + SceneLayoutMetrics.BackdropSortingOrder);

        Vector3 expected = SceneLayoutMetrics.NormalBackdropPosition(backdrop.area);
        if ((backdrop.transform.position - expected).sqrMagnitude > 0.0001f)
            report.Fail(sceneName + " 全景背景位置不对");
        else report.Ok(sceneName + " 全景背景锚点正确");

        GameObject oldBackdrop = GameObject.Find("Backdrop");
        SpriteRenderer oldRenderer = oldBackdrop != null ? oldBackdrop.GetComponent<SpriteRenderer>() : null;
        if (oldRenderer != null && oldRenderer.enabled)
            report.Fail(sceneName + " 旧程序背景仍然可见");
    }

    private static void CheckAnchors(Report report, string sceneName)
    {
        if (sceneName == "FirstFloor")
        {
            CheckX(report, sceneName, "Door_Grandma", SceneLayoutMetrics.GrandmaDoorX);
            CheckX(report, sceneName, "Door_Aunt", SceneLayoutMetrics.UncleDoorX);
            CheckX(report, sceneName, "Portal_To_Backyard", SceneLayoutMetrics.StairsX);
            if (GameObject.Find("Door_Girl") != null) report.Fail("一楼不应再有 Door_Girl");
        }
        else if (sceneName == "SecondFloor")
        {
            CheckX(report, sceneName, "Door_Mother", SceneLayoutMetrics.MotherDoorX);
            CheckX(report, sceneName, "Door_Girl", SceneLayoutMetrics.GirlDoorX);
            CheckX(report, sceneName, "Stair_To_FirstFloor", SceneLayoutMetrics.StairsX);
        }
    }

    private static void CheckX(Report report, string sceneName, string objectName, float expectedX)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null)
        {
            report.Fail(sceneName + " 缺少 " + objectName);
            return;
        }
        if (Mathf.Abs(target.transform.position.x - expectedX) > 0.01f)
            report.Fail(sceneName + " 的 " + objectName + " 没有对齐全景锚点");
    }

    private static void CheckPlacementLayout(Report report, Scene scene)
    {
        AreaId area = AreaIdUtil.FromSceneName(scene.name);
        System.Collections.Generic.List<ScenePlacementSpec> expected = ScenePlacementLayout.ForArea(area);
        PlacementPoint[] actual = Object.FindObjectsOfType<PlacementPoint>(true);
        bool valid = true;

        int scenePointCount = 0;
        for (int i = 0; i < actual.Length; i++)
        {
            if (actual[i] != null && actual[i].gameObject.scene == scene) scenePointCount++;
        }

        if (scenePointCount != expected.Count)
        {
            report.Fail(scene.name + " 放置点数量应为 " + expected.Count + "，实际 " + scenePointCount);
            return;
        }

        for (int i = 0; i < expected.Count; i++)
        {
            ScenePlacementSpec spec = expected[i];
            PlacementPoint point = null;
            for (int p = 0; p < actual.Length; p++)
            {
                if (actual[p] != null && actual[p].gameObject.scene == scene
                    && actual[p].placementId == spec.id)
                {
                    point = actual[p];
                    break;
                }
            }

            if (point == null)
            {
                report.Fail(scene.name + " 缺少放置点 " + spec.id);
                valid = false;
                continue;
            }

            if (point.area != spec.area || point.surface != spec.surface)
            {
                report.Fail(scene.name + " 的 " + spec.id + " 区域或放置面与统一布局不一致");
                valid = false;
            }
            else if ((point.transform.position - spec.position).sqrMagnitude > 0.0001f)
            {
                report.Fail(scene.name + " 的 " + spec.id + " 坐标不一致：实际 "
                    + point.transform.position + "，应为 " + spec.position);
                valid = false;
            }
        }

        if (valid)
            report.Ok(scene.name + " 放置点与 ScenePlacementLayout 一致（" + scenePointCount + " 个）");
    }

    private static void CheckMotherConsistency(Report report)
    {
        GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(ResourceFolder + "/GameConfig.asset");
        if (config == null) return;

        string[] paths = { "Assets/Scenes/FirstFloor.unity", "Assets/Scenes/SecondFloor.unity", "Assets/Scenes/Backyard.unity" };
        int motherCount = 0;
        for (int i = 0; i < paths.Length; i++)
        {
            if (!System.IO.File.Exists(paths[i])) continue;
            string text = System.IO.File.ReadAllText(paths[i]);
            motherCount += CountOccurrences(text, "NPC_Mother");
        }

        if (config.motherEnabled && motherCount == 0)
            report.Warn("GameConfig.motherEnabled = true，但场景里没有母亲的 NPC（demo 口径应设为 false）");
        else if (!config.motherEnabled && motherCount > 0)
            report.Warn("GameConfig.motherEnabled = false，但场景里放了母亲的 NPC");
        else
            report.Ok("母亲开关与场景内容一致（motherEnabled=" + config.motherEnabled + "，场景里母亲 " + motherCount + " 个）");
    }

    private static int CountOccurrences(string text, string token)
    {
        int count = 0;
        int index = text.IndexOf(token);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(token, index + token.Length);
        }
        return count;
    }
}
