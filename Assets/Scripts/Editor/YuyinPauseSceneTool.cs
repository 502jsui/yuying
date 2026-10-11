using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 生成 / 重建暂停场景（Assets/Scenes/Pause.unity）。
///
/// 里面只有程序要的部分：相机 + 背景（运行时用 PauseSnapshot 那张截图铺满）+ PauseScreen 回调。
/// 按钮、设置面板这些 UI 由 UI 同学往 "UI" 根节点下面加，按钮挂
/// PauseScreen.OnResumeClicked / OnSettingsClicked / OnQuitClicked 三个方法就行。
///
/// 用法：Tools → 余音 → 重建暂停场景；
/// 批处理：-executeMethod YuyinPauseSceneTool.RebuildFromCommandLine
/// </summary>
public static class YuyinPauseSceneTool
{
    public const string PauseScenePath = "Assets/Scenes/Pause.unity";

    private const float SceneHeight = 10.8f;

    [MenuItem("Tools/余音/重建暂停场景")]
    public static void RebuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Rebuild();
    }

    public static void RebuildFromCommandLine()
    {
        try
        {
            Rebuild();
            EditorApplication.Exit(0);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void Rebuild()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        CreatePauseScreen();

        // 给 UI 同学留的位置：按钮 / 设置面板都摆这里面。
        new GameObject("UI");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, PauseScenePath);
        AddToBuildSettings(PauseScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[余音·暂停] 暂停场景已重建：" + PauseScenePath);
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = SceneHeight * 0.5f;   // 和游戏里一屏 19.2 × 10.8 对齐
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.03f, 0.04f, 0.06f);
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        camera.allowHDR = false;
        camera.allowMSAA = false;

        // 游戏场景卸载后，全场景只能有这一个 AudioListener。
        cameraObject.AddComponent<AudioListener>();
    }

    private static void CreatePauseScreen()
    {
        GameObject controller = new GameObject("PauseScreen");
        PauseScreen screen = controller.AddComponent<PauseScreen>();

        GameObject background = new GameObject("Background");
        background.transform.SetParent(controller.transform, false);
        background.transform.position = Vector3.zero;

        SpriteRenderer renderer = background.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = -100;   // 垫在所有 UI 后面

        screen.background = renderer;
    }

    private static void AddToBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i] != null && scenes[i].path == scenePath) return;
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[余音·暂停] 已加入 Build Settings：" + scenePath);
    }
}
