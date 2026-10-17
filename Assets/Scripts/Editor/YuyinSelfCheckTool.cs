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
/// 会打开 FirstFloor / Backyard 两个场景来数对象（和美术工具一个套路），跑完不会自动切回原场景。
/// </summary>
public static class YuyinSelfCheckTool
{
    private const string ResourceFolder = "Assets/Resources";

    private static readonly string[] RequiredScenes =
    {
        "MainMenu", "FirstFloor", "Backyard", "Pause", "Settings", "Credits"
    };

    private static readonly string[] GameScenes = { "FirstFloor", "Backyard" };

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
            ResourceFolder + "/CharacterArt.asset"
        };

        for (int i = 0; i < paths.Length; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(paths[i]) != null) report.Ok("配置就位：" + paths[i]);
            else report.Fail("配置缺失：" + paths[i]);
        }
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
            else report.Ok(prefab.name + " 立绘/占位体就位");
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
        for (int i = 0; i < ExpectedResidents.Length; i++)
        {
            ResidentView found = null;
            for (int r = 0; r < residents.Length; r++)
            {
                if (residents[r] != null && residents[r].id == ExpectedResidents[i]) { found = residents[r]; break; }
            }
            if (found == null) report.Fail(name + " 缺住户 " + ExpectedResidents[i].ToDisplayName());
        }
        if (residents.Length > 0) report.Ok(name + " 住户 View 共 " + residents.Length + " 个");

        int doorLights = Object.FindObjectsOfType<DoorLightView>().Length;
        if (doorLights == 0) report.Warn(name + " 没有挂 DoorLightView（门缝光还没接，属于已知缺口）");
        else report.Ok(name + " 门缝光组件 " + doorLights + " 个");
    }

    private static void CheckMotherConsistency(Report report)
    {
        GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(ResourceFolder + "/GameConfig.asset");
        if (config == null) return;

        string[] paths = { "Assets/Scenes/FirstFloor.unity", "Assets/Scenes/Backyard.unity" };
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
