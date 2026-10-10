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
///   2. 不破坏已有内容：已经有同 placementId 的放置点会直接跳过，
///      所以你手动调过位置之后再跑一次，位置不会被重置；
///   3. 放置点就是普通场景物体（挂 PlacementPoint），后期随便拖。
///
/// 用法：打开 FirstFloor 或 Backyard，菜单 Tools → 余音 → 在当前场景补齐物品与放置点。
/// </summary>
public static class YuyinItemSceneTool
{
    private const string ConfigPath = "Assets/Resources/GameConfig.asset";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string RootName = "ItemSystem";
    private const string PointsRootName = "PlacementPoints";
    private const string ItemsRootName = "Items";
    private const string StagingRootName = "PoolStaging";

    /// <summary>母亲机制本次不做：先不生成母亲的物品 View。要做母亲线时改成 true 再跑一次菜单即可。</summary>
    private const bool IncludeMotherItems = false;

    /// <summary>物品池里的物品暂时放在这个位置（画面外），运行时会被吸附到真正的放置点。</summary>
    private static readonly Vector3 StagingOrigin = new Vector3(-40f, -40f, 0f);

    private struct PlacementSpec
    {
        public string id;
        public PlacementSurface surface;
        public Vector3 position;
    }

    [MenuItem("Tools/余音/在当前场景补齐物品与放置点")]
    public static void EnsureInCurrentScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        AreaId area = AreaIdUtil.FromSceneName(scene.name);
        if (area == AreaId.None)
        {
            Notify("当前场景不是 FirstFloor / Backyard，无法判断区域。");
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
            "[余音·物品] {0}：新增放置点 {1} 个、物品 View {2} 个（重复运行不会重复创建，已有放置点位置保持不变）。",
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
        List<PlacementSpec> specs = GetPlacementSpecs(area);

        for (int i = 0; i < specs.Count; i++)
        {
            PlacementSpec spec = specs[i];
            if (FindPlacementPoint(scene, spec.id) != null) continue; // 已有：保留用户改过的位置

            GameObject go = new GameObject(spec.id);
            go.transform.SetParent(parent, false);
            go.transform.position = spec.position;

            PlacementPoint point = go.AddComponent<PlacementPoint>();
            point.placementId = spec.id;
            point.area = area;
            point.surface = spec.surface;
            created++;
        }

        return created;
    }

    private static List<PlacementSpec> GetPlacementSpecs(AreaId area)
    {
        if (area == AreaId.FirstFloor)
        {
            return new List<PlacementSpec>
            {
                Spec("first_rack", PlacementSurface.HangingRack, -8.0f, 1.8f),
                Spec("first_ground_a", PlacementSurface.Ground, -6.5f, -2.6f),
                Spec("first_ground_b", PlacementSurface.Ground, 0.0f, -2.6f),
                Spec("first_ground_c", PlacementSurface.Ground, 6.5f, -2.6f),
                Spec("first_on_item", PlacementSurface.OnItem, 6.0f, -1.2f)
            };
        }

        return new List<PlacementSpec>
        {
            Spec("backyard_ground_a", PlacementSurface.Ground, -5.5f, -2.6f),
            Spec("backyard_ground_b", PlacementSurface.Ground, 0.0f, -2.6f),
            Spec("backyard_ground_c", PlacementSurface.Ground, 5.5f, -2.6f),
            Spec("backyard_on_item", PlacementSurface.OnItem, 2.5f, -1.0f)
        };
    }

    private static PlacementSpec Spec(string id, PlacementSurface surface, float x, float y)
    {
        PlacementSpec spec = new PlacementSpec();
        spec.id = id;
        spec.surface = surface;
        spec.position = new Vector3(x, y, 0f);
        return spec;
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

            if (!IncludeMotherItems && definition.owner == ItemOwner.Mother)
            {
                skippedMother++;
                continue;
            }

            if (FindItemView(scene, definition.itemId) != null) continue; // 已有：不重复创建

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
            Debug.Log("[余音·物品] 母亲物品本次不做，已跳过 " + skippedMother + " 件（要启用把 IncludeMotherItems 改成 true）。");
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
