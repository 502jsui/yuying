using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class YuyinSceneBuilder
{
    private const string SceneRoot = "Assets/Scenes";
    private const string FirstFloorPath = SceneRoot + "/FirstFloor.unity";
    private const string BackyardPath = SceneRoot + "/Backyard.unity";

    // 一屏尺寸：16:9 + 正交尺寸 5.4 → 19.2 × 10.8。
    // 与 SceneCameraFollow2D 的取景公式一致，保证“场景和屏幕等大”。
    private const float SceneWidth = 19.2f;
    private const float SceneHeight = 10.8f;
    private const float HalfSceneWidth = SceneWidth * 0.5f;
    private const float HalfSceneHeight = SceneHeight * 0.5f;
    private const float PlayerMinX = -9f;
    private const float PlayerMaxX = 9f;
    private const float PlayerY = -2.4f;

    private static Sprite white;

    [MenuItem("Tools/余音/重建基础场景")]
    public static void BuildFromMenu()
    {
        if (!EditorUtility.DisplayDialog("重建基础场景", "将重建 FirstFloor.unity 和 Backyard.unity，并移除所有场景表现层。", "重建", "取消")) return;
        BuildScenes();
    }

    public static void BuildFromCommandLine()
    {
        try { BuildScenes(); EditorApplication.Exit(0); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    public static void BuildScenes()
    {
        EnsureFolder(SceneRoot);
        white = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (white == null) throw new InvalidOperationException("无法读取 Unity 内置白色 Sprite。");

        BuildFirstFloorScene();
        BuildBackyardScene();
        UpdateBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(FirstFloorPath, OpenSceneMode.Single);
        Debug.Log("基础场景构建完成：一楼 + 后院，仅保留移动角色和场景骨架。");
    }

    private static void BuildFirstFloorScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject world = CreateRoot("World");
        Transform environment = CreateRoot("Environment", world.transform).transform;
        Transform gameplay = CreateRoot("Gameplay", world.transform).transform;
        Transform cameraRoot = CreateRoot("Camera", world.transform).transform;

        CreateRect("Backdrop", new Vector3(0f, 0f, 0f), new Vector2(SceneWidth, SceneHeight), new Color(0.055f, 0.075f, 0.105f), -50, environment);
        CreateRect("Upper Wall", new Vector3(0f, 3.4f, 0f), new Vector2(SceneWidth, 1.5f), new Color(0.10f, 0.13f, 0.17f), -40, environment);
        CreateRect("Lower Wall", new Vector3(0f, -2.8f, 0f), new Vector2(SceneWidth, 2.4f), new Color(0.11f, 0.14f, 0.17f), -40, environment);
        CreateRect("Ceiling", new Vector3(0f, 4.9f, 0f), new Vector2(SceneWidth, 1.2f), new Color(0.06f, 0.075f, 0.095f), -35, environment);
        CreateRect("Floor", new Vector3(0f, -4.35f, 0f), new Vector2(SceneWidth, 2.0f), new Color(0.22f, 0.21f, 0.20f), -20, environment);
        CreateRect("Floor Line", new Vector3(0f, -3.35f, 0f), new Vector2(SceneWidth, 0.12f), new Color(0.35f, 0.36f, 0.35f), -15, environment);

        for (int x = -8; x <= 8; x += 4) CreateRect("Wall Seam", new Vector3(x, 0f, 0f), new Vector2(0.06f, 7.2f), new Color(0.20f, 0.23f, 0.26f, 0.65f), -10, environment);

        CreateDoor(environment, "Door_Grandma", new Vector3(-6f, 0.2f, 0f), new Color(0.64f, 0.45f, 0.24f));
        CreateDoor(environment, "Door_Aunt", new Vector3(0f, 0.2f, 0f), new Color(0.22f, 0.43f, 0.47f));
        CreateDoor(environment, "Door_Girl", new Vector3(6f, 0.2f, 0f), new Color(0.54f, 0.29f, 0.38f));

        GameObject player = CreatePlayer(gameplay, new Vector3(-7.5f, PlayerY, 0f), PlayerMinX, PlayerMaxX, PlayerY);
        CreateCamera(cameraRoot, -HalfSceneWidth, HalfSceneWidth, -HalfSceneHeight, HalfSceneHeight);

        List<ResidentView> residents = CreateNpcs(gameplay, AreaId.FirstFloor);

        CreateGameManager(gameplay, player.GetComponent<PlayerController>(), residents);
        CreatePortal(environment, "Portal_To_Backyard", player.transform, "Backyard", new Vector3(8.6f, PlayerY, 0f), new Color(0.32f, 0.76f, 0.60f, 0.85f), "FirstFloor_Right", "Backyard_Left", -1f);


        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, FirstFloorPath);
    }

    private static void BuildBackyardScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject world = CreateRoot("World");
        Transform environment = CreateRoot("Environment", world.transform).transform;
        Transform gameplay = CreateRoot("Gameplay", world.transform).transform;
        Transform cameraRoot = CreateRoot("Camera", world.transform).transform;

        CreateRect("Sky", new Vector3(0f, 0f, 0f), new Vector2(SceneWidth, SceneHeight), new Color(0.06f, 0.10f, 0.15f), -50, environment);
        CreateRect("Ground", new Vector3(0f, -3.8f, 0f), new Vector2(SceneWidth, 3.2f), new Color(0.16f, 0.27f, 0.21f), -30, environment);
        CreateRect("Path", new Vector3(0f, -3.45f, 0f), new Vector2(SceneWidth, 1.2f), new Color(0.28f, 0.33f, 0.27f), -25, environment);
        CreateRect("Back Fence", new Vector3(0f, -1.6f, 0f), new Vector2(SceneWidth, 0.18f), new Color(0.36f, 0.28f, 0.20f), -20, environment);

        for (int x = -8; x <= 8; x += 4)
        {
            CreateRect("Fence Post", new Vector3(x, -1.95f, 0f), new Vector2(0.18f, 1.2f), new Color(0.30f, 0.22f, 0.16f), -19, environment);
        }

        GameObject player = CreatePlayer(gameplay, new Vector3(-7.5f, PlayerY, 0f), PlayerMinX, PlayerMaxX, PlayerY);
        CreateCamera(cameraRoot, -HalfSceneWidth, HalfSceneWidth, -HalfSceneHeight, HalfSceneHeight);

        List<ResidentView> residents = CreateNpcs(gameplay, AreaId.Backyard);

        CreateGameManager(gameplay, player.GetComponent<PlayerController>(), residents);
        CreatePortal(environment, "Portal_To_FirstFloor", player.transform, "FirstFloor", new Vector3(-8.6f, PlayerY, 0f), new Color(0.96f, 0.72f, 0.34f, 0.85f), "Backyard_Left", "FirstFloor_Right", 1f);

        CreateRainSystem(environment, 1f, true, 0.14f);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, BackyardPath);
    }

    private static GameObject CreatePlayer(Transform parent, Vector3 position, float minX, float maxX, float fixedY)
    {
        GameObject prefab = LoadRequired<GameObject>("Assets/Prefabs/Player.prefab");
        GameObject player = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (player == null) throw new InvalidOperationException("Player Prefab 实例化失败。");

        player.name = "Player";
        player.transform.position = position;
        Transform body = player.transform.Find("Body");
        if (body != null)
        {
            body.localPosition = Vector3.zero;
            body.localScale = Vector3.one;
        }

        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null) controller.itemLayer = 1 << 8;

        SceneBounds2D bounds = player.AddComponent<SceneBounds2D>();
        bounds.target = player.transform;
        bounds.minX = minX;
        bounds.maxX = maxX;
        bounds.fixedY = fixedY;
        return player;
    }

    /// <summary>按区域生成住户占位（长条形），复用 YuyinNpcSceneTool 的摆放参数。</summary>
    private static List<ResidentView> CreateNpcs(Transform parent, AreaId area)
    {
        List<ResidentView> residents = new List<ResidentView>();
        GameObject root = CreateRoot("NPCs", parent);
        List<NpcPlacementSpec> specs = YuyinNpcSceneTool.GetSpecs(area);

        for (int i = 0; i < specs.Count; i++)
        {
            if (specs[i].id == ResidentId.Mother) continue; // 母亲本次不做

            ResidentView view = YuyinNpcSceneTool.SpawnPlaceholder(root.transform, specs[i]);
            if (view != null) residents.Add(view);
        }

        return residents;
    }

    private static ResidentView CreateResident(Transform parent, string name, string prefabPath, Vector3 position, float scale, float patrolMinX, float patrolMaxX, float speed)
    {
        GameObject prefab = LoadRequired<GameObject>(prefabPath);
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null) throw new InvalidOperationException("Resident Prefab 实例化失败: " + prefabPath);

        instance.name = name;
        instance.transform.position = position;
        instance.transform.localScale = Vector3.one * scale;
        SetLayerRecursively(instance, 7);

        ResidentView view = instance.GetComponent<ResidentView>();
        if (view == null) throw new InvalidOperationException(name + " 上没有 ResidentView。");

        NpcPatrol2D patrol = instance.AddComponent<NpcPatrol2D>();
        patrol.minX = patrolMinX;
        patrol.maxX = patrolMaxX;
        patrol.speed = speed;
        return view;
    }

    private static void CreateCamera(Transform parent, float minX, float maxX, float minY, float maxY)
    {
        GameObject cameraObject = CreateRoot("Main Camera", parent);
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, -10f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = HalfSceneHeight;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.04f, 0.065f);
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        camera.allowHDR = false;
        camera.allowMSAA = false;

        SceneCameraFollow2D framing = cameraObject.AddComponent<SceneCameraFollow2D>();
        framing.sceneMinX = minX;
        framing.sceneMaxX = maxX;
        framing.sceneMinY = minY;
        framing.sceneMaxY = maxY;
        framing.ApplyFraming();
        cameraObject.AddComponent<AudioListener>();
    }

    private static GameManager CreateGameManager(Transform parent, PlayerController player, List<ResidentView> residents)
    {
        GameObject managerObject = CreateRoot("GameManager", parent);
        GameManager manager = managerObject.AddComponent<GameManager>();
        manager.player = player;
        manager.residentViews = residents.ToArray();
        manager.itemViews = new ItemView[0];
        EditorUtility.SetDirty(manager);
        return manager;
    }

    private static void CreateRainSystem(Transform parent, float intensity, bool lightningEnabled, float tintAlpha)
    {
        GameObject root = CreateRoot("Rain", parent);
        RainController rain = root.AddComponent<RainController>();
        // 运行时 RainController 还会按相机可视范围再放大一次，这里只是兜底值。
        rain.width = SceneWidth + 6f;
        rain.height = SceneHeight + 6f;
        rain.groundY = -3.4f;
        rain.intensity = intensity;
        rain.lightningEnabled = lightningEnabled;
        rain.tintColor = new Color(0.06f, 0.14f, 0.28f, tintAlpha);
        rain.foregroundRate = intensity < 0.6f ? 38f : 110f;
        EditorUtility.SetDirty(rain);
    }
    private static void CreateDoor(Transform parent, string name, Vector3 center, Color doorColor)
    {
        GameObject root = CreateRoot(name, parent);
        root.transform.position = center;
        CreateRect("Frame", center, new Vector2(3.6f, 5.7f), new Color(0.12f, 0.10f, 0.09f), -1, root.transform);
        CreateRect("Door", center, new Vector2(3.05f, 5.0f), doorColor, 0, root.transform);
        CreateRect("Handle", center + new Vector3(1.0f, 0f, 0f), new Vector2(0.18f, 0.18f), new Color(0.85f, 0.67f, 0.30f), 2, root.transform);
    }

    private static void CreatePortal(Transform parent, string name, Transform player, string sceneName, Vector3 position, Color color, string portalId, string arrivalPortalId, float arrivalDirection)
    {
        GameObject root = CreateRoot(name, parent);
        root.transform.position = position;
        CreateRect("Portal Frame", position + new Vector3(0f, 2.25f, 0f), new Vector2(3.6f, 5.2f), new Color(0.08f, 0.11f, 0.12f), 10, root.transform);
        CreateRect("Portal Opening", position + new Vector3(0f, 2.25f, 0f), new Vector2(3.0f, 4.6f), new Color(0.04f, 0.07f, 0.08f), 11, root.transform);
        CreateRect("Portal Bar", position + new Vector3(0f, 0.05f, 0f), new Vector2(3.0f, 0.12f), color, 12, root.transform);

        ScenePortal2D portal = root.AddComponent<ScenePortal2D>();
        portal.player = player;
        portal.sceneName = sceneName;
        portal.radius = 1.45f;
        portal.portalId = portalId;
        portal.arrivalPortalId = arrivalPortalId;
        portal.arrivalDirection = arrivalDirection;
        portal.arrivalOffset = 2.6f;
    }

    private static GameObject CreateRoot(string name, Transform parent = null)
    {
        GameObject gameObject = new GameObject(name);
        if (parent != null) gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static GameObject CreateRect(string name, Vector3 position, Vector2 size, Color color, int sortingOrder, Transform parent)
    {
        GameObject gameObject = CreateRoot(name, parent);
        gameObject.transform.position = position;
        gameObject.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = white;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return gameObject;
    }

    private static void SetLayerRecursively(GameObject gameObject, int layer)
    {
        gameObject.layer = layer;
        foreach (Transform child in gameObject.transform) SetLayerRecursively(child.gameObject, layer);
    }

    private static void UpdateBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(FirstFloorPath, true),
            new EditorBuildSettingsScene(BackyardPath, true)
        };

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
        string normalized = path.Replace('\\', '/').TrimEnd('/');
        string parent = Path.GetDirectoryName(normalized).Replace('\\', '/');
        string leaf = Path.GetFileName(normalized);
        if (!string.IsNullOrEmpty(parent) && parent != "Assets" && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(normalized)) AssetDatabase.CreateFolder(parent, leaf);
    }

    private static T LoadRequired<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("资源加载失败: " + path);
        return asset;
    }
}











