using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 往当前场景补齐「物品 View + 放置点」的编辑器工具。
///
/// 特点：
///   1. 幂等：重复运行只会补缺失的对象，不会重复创建；
///   2. 权威布局：运行后会按 ScenePlacementLayout 同步放置点的坐标、区域和放置面；
///   3. 放置点就是普通场景物体（挂 PlacementPoint），后期随便拖。
///
/// 用法：打开 FirstFloor / SecondFloor / Backyard，
/// 菜单 Tools → 余音 → 在当前场景补齐物品与放置点。
/// </summary>
public static class YuyinItemSceneTool
{
    private const string ConfigPath = "Assets/Resources/GameConfig.asset";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string RootName = "ItemSystem";
    private const string PointsRootName = "PlacementPoints";
    private const string ItemsRootName = "Items";
    private const string StagingRootName = "PoolStaging";

    /// <summary>物品池里的物品暂时放在这个位置（画面外），运行时会被吸附到真正的放置点。</summary>
    private static readonly Vector3 StagingOrigin = new Vector3(-40f, -40f, 0f);

    [MenuItem("Tools/余音/在当前场景补齐物品与放置点")]
    public static void EnsureInCurrentScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        AreaId area = AreaIdUtil.FromSceneName(scene.name);
        if (area == AreaId.None)
        {
            Notify("当前场景不是 FirstFloor / SecondFloor / Backyard，无法判断区域。");
            return;
        }

        GameManager manager = FindFirstInScene<GameManager>(scene);
        if (manager == null)
        {
            Notify("当前场景没有 GameManager。先跑 Tools → 余音 → 重建基础场景。");
            return;
        }

        GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
        if (config == null)
        {
            Notify("找不到 " + ConfigPath + "。先跑 Tools → 余音 → 创建或选中默认配置。");
            return;
        }
        config.EnsureDefaults();

        GameObject root = FindOrCreateRoot(scene, RootName);
        Transform pointsRoot = FindOrCreateChild(root.transform, PointsRootName);
        Transform itemsRoot = FindOrCreateChild(root.transform, ItemsRootName);
        Transform stagingRoot = FindOrCreateChild(itemsRoot, StagingRootName);

        int createdPoints = EnsurePlacementPoints(scene, area, pointsRoot);
        int createdItems = EnsureItemViews(scene, area, config, itemsRoot, stagingRoot);
        EnsureManagerWiring(scene, manager);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(string.Format(
            "[余音·物品] {0}：新增放置点 {1} 个、物品 View {2} 个（放置点已按统一布局同步）。",
            scene.name, createdPoints, createdItems));
    }

    /// <summary>批处理入口：<c>unity -executeMethod YuyinItemSceneTool.EnsureAllScenesFromCommandLine</c></summary>
    public static void EnsureAllScenesFromCommandLine()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FirstFloor.unity", OpenSceneMode.Single);
            EnsureInCurrentScene();

            EditorSceneManager.OpenScene("Assets/Scenes/Backyard.unity", OpenSceneMode.Single);
            EnsureInCurrentScene();

            EditorSceneManager.OpenScene("Assets/Scenes/SecondFloor.unity", OpenSceneMode.Single);
            EnsureInCurrentScene();

            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    // ------------------------------------------------------------------
    // 放置点
    // ------------------------------------------------------------------

    private static int EnsurePlacementPoints(Scene scene, AreaId area, Transform parent)
    {
        int created = 0;
        List<ScenePlacementSpec> specs = ScenePlacementLayout.ForArea(area);
        List<PlacementPoint> existingPoints = FindAllInScene<PlacementPoint>(scene);
        for (int i = existingPoints.Count - 1; i >= 0; i--)
        {
            PlacementPoint point = existingPoints[i];
            if (point != null && !ScenePlacementLayout.Contains(area, point.placementId))
                Object.DestroyImmediate(point.gameObject);
        }

        for (int i = 0; i < specs.Count; i++)
        {
            ScenePlacementSpec spec = specs[i];
            PlacementPoint point = FindPlacementPoint(scene, spec.id);
            if (point == null)
            {
                GameObject go = new GameObject(spec.id);
                go.transform.SetParent(parent, false);
                point = go.AddComponent<PlacementPoint>();
                created++;
            }

            point.transform.position = spec.position;
            point.placementId = spec.id;
            point.area = spec.area;
            point.surface = spec.surface;
            point.anchor = null;
            EditorUtility.SetDirty(point);
        }

        return created;
    }

    // ------------------------------------------------------------------
    // 物品 View
    // ------------------------------------------------------------------

    private static int EnsureItemViews(Scene scene, AreaId area, GameConfig config, Transform itemsRoot, Transform stagingRoot)
    {
        int created = 0;
        int skippedMother = 0;

        for (int i = 0; i < config.items.Count; i++)
        {
            ItemConfig definition = config.items[i];
            if (definition == null || string.IsNullOrEmpty(definition.itemId)) continue;

            if (definition.owner == ItemOwner.Mother && area != AreaId.SecondFloor)
            {
                skippedMother++;
                continue;
            }

            ItemView existingView = FindItemView(scene, definition.itemId);
            if (existingView != null)
            {
                if (definition.startsInScene && definition.startArea == area
                    && !string.IsNullOrEmpty(definition.startPlacementId))
                {
                    PlacementPoint startPoint = FindPlacementPoint(scene, definition.startPlacementId);
                    if (startPoint != null)
                    {
                        existingView.transform.SetParent(itemsRoot, true);
                        existingView.transform.position = startPoint.AnchorPosition;
                        EditorUtility.SetDirty(existingView);
                    }
                }
                continue; // 已有：不重复创建
            }

            string prefabPath = PrefabPathFor(definition.owner);
            GameObject prefab = string.IsNullOrEmpty(prefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[余音·物品] 找不到 " + prefabPath + "，跳过物品 " + definition.itemId);
                continue;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, itemsRoot) as GameObject;
            if (instance == null) continue;
            instance.name = "Item_" + definition.itemId;
            instance.transform.localScale = Vector3.one;

            ItemView view = instance.GetComponent<ItemView>();
            if (view == null)
            {
                Debug.LogWarning("[余音·物品] " + prefabPath + " 上没有 ItemView，跳过。");
                Object.DestroyImmediate(instance);
                continue;
            }

            view.itemId = definition.itemId;
            view.owner = definition.owner;
            view.type = definition.type;
            view.area = area;

            if (definition.startsInScene && definition.startArea == area && !string.IsNullOrEmpty(definition.startPlacementId))
            {
                // 开局就在场景里的物品：直接摆到它对应的放置点上。
                PlacementPoint point = FindPlacementPoint(scene, definition.startPlacementId);
                instance.transform.position = point != null
                    ? point.AnchorPosition
                    : StagingOrigin + new Vector3(created * 1.5f, 0f, 0f);
            }
            else
            {
                // 物品池里的物品：先放到画面外，运行时被「拿出东西」吸附到放置点。
                instance.transform.SetParent(stagingRoot, true);
                instance.transform.position = StagingOrigin + new Vector3(created * 1.5f, 0f, 0f);
            }

            created++;
        }

        if (skippedMother > 0)
        {
            Debug.Log("[余音·物品] 母亲物品只在 SecondFloor 生成，本场景跳过 " + skippedMother + " 件。");
        }

        return created;
    }

    private static string PrefabPathFor(ItemOwner owner)
    {
        switch (owner)
        {
            case ItemOwner.Grandma: return PrefabFolder + "/Item_Grandma.prefab";
            case ItemOwner.Uncle: return PrefabFolder + "/Item_Uncle.prefab";
            case ItemOwner.Mother: return PrefabFolder + "/Item_Mother.prefab";
            default: return null;
        }
    }

    // ------------------------------------------------------------------
    // 场景绑定
    // ------------------------------------------------------------------

    private static void EnsureManagerWiring(Scene scene, GameManager manager)
    {
        List<ItemView> views = FindAllInScene<ItemView>(scene);
        views.Sort(delegate (ItemView a, ItemView b) { return string.CompareOrdinal(a.name, b.name); });
        manager.itemViews = views.ToArray();
        EditorUtility.SetDirty(manager);
    }

    // ------------------------------------------------------------------
    // 小工具
    // ------------------------------------------------------------------

    private static GameObject FindOrCreateRoot(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].name == name) return roots[i];
        }

        GameObject created = new GameObject(name);
        SceneManager.MoveGameObjectToScene(created, scene);
        return created;
    }

    private static Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing;

        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static PlacementPoint FindPlacementPoint(Scene scene, string placementId)
    {
        List<PlacementPoint> points = FindAllInScene<PlacementPoint>(scene);
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] != null && points[i].placementId == placementId) return points[i];
        }
        return null;
    }

    private static ItemView FindItemView(Scene scene, string itemId)
    {
        List<ItemView> views = FindAllInScene<ItemView>(scene);
        for (int i = 0; i < views.Count; i++)
        {
            if (views[i] != null && views[i].itemId == itemId) return views[i];
        }
        return null;
    }

    private static T FindFirstInScene<T>(Scene scene) where T : Component
    {
        List<T> all = FindAllInScene<T>(scene);
        return all.Count > 0 ? all[0] : null;
    }

    private static List<T> FindAllInScene<T>(Scene scene) where T : Component
    {
        List<T> result = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            result.AddRange(roots[i].GetComponentsInChildren<T>(true));
        }
        return result;
    }

    private static void Notify(string message)
    {
        Debug.LogWarning("[余音·物品] " + message);
        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("余音 · 物品", message, "知道了");
        }
    }
}
