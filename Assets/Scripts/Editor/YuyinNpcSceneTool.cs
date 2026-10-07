using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 一个住户在场景里的摆放参数（占位用长条形）。
/// </summary>
public struct NpcPlacementSpec
{
    public ResidentId id;
    public AreaId area;
    public Vector3 position;

    /// <summary>占位长条的宽 × 高（世界单位）。</summary>
    public Vector2 barSize;

    public float patrolMinX;
    public float patrolMaxX;
    public float patrolSpeed;
}

/// <summary>
/// 往当前场景补齐 NPC 的编辑器工具（和物品那套一样是幂等的）。
///
/// 现在还没有角色美术，先用「长条形」占位：
///   - 用 Assets/Prefabs/Resident_*.prefab 实例化（里面已经带 ResidentView + BoxCollider2D）；
///   - 把 Body 子物体拉成竖长条，体型按角色区分（小女孩更矮更快）；
///   - 老婆婆 / 大叔 站在原地缓慢走动，小女孩在较大范围里快速跑动；
///   - 住户按区域显隐：正常在自己家（homeArea），「区域持续」时出现在 stayOutArea。
///
/// 用法：打开 FirstFloor 或 Backyard，菜单 Tools → 余音 → 在当前场景补齐 NPC。
/// 重复运行只会补缺失的 NPC，不会重建 / 移动已有的。
/// </summary>
public static class YuyinNpcSceneTool
{
    private const string PrefabFolder = "Assets/Prefabs";
    private const string RootName = "NPCs";
    private const int ResidentLayer = 7;

    /// <summary>母亲本次不做：先不生成母亲。要启用改成 true 再跑一次菜单。</summary>
    private const bool IncludeMother = false;

    [MenuItem("Tools/余音/在当前场景补齐 NPC")]
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

        GameObject root = FindOrCreateRoot(scene, RootName);
        List<NpcPlacementSpec> specs = GetSpecs(area);
        int created = 0;

        for (int i = 0; i < specs.Count; i++)
        {
            NpcPlacementSpec spec = specs[i];
            if (!IncludeMother && spec.id == ResidentId.Mother) continue;

            ResidentView existing = FindResidentView(scene, spec.id);
            if (existing != null)
            {
                // 已有：不重建、不挪位置，只补可能缺的绑定 / 长条尺寸。
                RepairExisting(existing, spec);
                continue;
            }

            if (SpawnPlaceholder(root.transform, spec) != null) created++;
        }

        EnsureManagerWiring(scene, manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(string.Format(
            "[余音·NPC] {0}：新增 NPC {1} 个（重复运行不会重复创建，已有 NPC 位置保持不变）。",
            scene.name, created));
    }

    /// <summary>批处理入口：<c>unity -executeMethod YuyinNpcSceneTool.EnsureAllScenesFromCommandLine</c></summary>
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
    // 摆放
    // ------------------------------------------------------------------

    /// <summary>在一楼 / 后院各自的默认位置和巡逻范围。x 对应三扇门：-6 老婆婆、0 大叔、6 小女孩。</summary>
    public static List<NpcPlacementSpec> GetSpecs(AreaId area)
    {
        if (area == AreaId.Backyard)
        {
            return new List<NpcPlacementSpec>
            {
                Spec(ResidentId.Grandma, area, -5.0f, -1.6f, 0.80f, 2.00f, -6.0f, -3.0f, 0.30f),
                Spec(ResidentId.Uncle, area, 0.0f, -1.6f, 0.90f, 2.30f, -2.0f, 2.0f, 0.30f),
                Spec(ResidentId.Girl, area, 5.0f, -1.9f, 0.60f, 1.40f, 2.0f, 8.0f, 1.40f),
                Spec(ResidentId.Mother, area, -8.0f, -1.6f, 0.85f, 2.10f, -8.6f, -7.0f, 0.20f)
            };
        }

        return new List<NpcPlacementSpec>
        {
            Spec(ResidentId.Grandma, area, -6.0f, -1.5f, 0.80f, 2.00f, -7.0f, -5.0f, 0.30f),
            Spec(ResidentId.Uncle, area, 0.0f, -1.5f, 0.90f, 2.30f, -1.0f, 1.0f, 0.30f),
            Spec(ResidentId.Girl, area, 6.0f, -1.7f, 0.60f, 1.40f, 2.5f, 8.0f, 1.40f),
            Spec(ResidentId.Mother, area, -8.6f, -1.5f, 0.85f, 2.10f, -9.0f, -8.0f, 0.20f)
        };
    }

    private static NpcPlacementSpec Spec(ResidentId id, AreaId area, float x, float y, float width, float height, float patrolMinX, float patrolMaxX, float speed)
    {
        NpcPlacementSpec spec = new NpcPlacementSpec();
        spec.id = id;
        spec.area = area;
        spec.position = new Vector3(x, y, 0f);
        spec.barSize = new Vector2(width, height);
        spec.patrolMinX = patrolMinX;
        spec.patrolMaxX = patrolMaxX;
        spec.patrolSpeed = speed;
        return spec;
    }

    /// <summary>实例化一个长条形占位 NPC。场景工具和场景重建器共用。</summary>
    public static ResidentView SpawnPlaceholder(Transform parent, NpcPlacementSpec spec)
    {
        string prefabPath = PrefabPathFor(spec.id);
        if (string.IsNullOrEmpty(prefabPath)) return null;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[余音·NPC] 找不到 " + prefabPath + "，跳过 " + spec.id);
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null) return null;

        instance.name = "NPC_" + spec.id;
        instance.transform.position = spec.position;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        SetLayerRecursively(instance, ResidentLayer);

        ResidentView view = instance.GetComponent<ResidentView>();
        if (view == null)
        {
            Debug.LogWarning("[余音·NPC] " + prefabPath + " 上没有 ResidentView。");
            Object.DestroyImmediate(instance);
            return null;
        }

        view.id = spec.id;
        view.area = spec.area;

        // 长条形占位：只拉身体 Sprite，不影响碰撞体和逻辑。
        Transform body = EnsureBody(view);
        if (body != null) body.localScale = new Vector3(spec.barSize.x, spec.barSize.y, 1f);

        BoxCollider2D box = instance.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.size = spec.barSize;
            box.isTrigger = true;
        }

        NpcPatrol2D patrol = instance.GetComponent<NpcPatrol2D>();
        if (patrol == null) patrol = instance.AddComponent<NpcPatrol2D>();
        patrol.minX = spec.patrolMinX;
        patrol.maxX = spec.patrolMaxX;
        patrol.speed = spec.patrolSpeed;
        patrol.direction = 1f;
        // 现在住户默认待在门里 / 门口，只有行动时才由 NpcPhaseDriver 带着走。
        patrol.enabled = false;

        return view;
    }

    /// <summary>
    /// 保证 ResidentView 有 bodyRenderer（有的 prefab 装配漏了），返回要拉伸的身体 Transform。
    /// </summary>
    public static Transform EnsureBody(ResidentView view)
    {
        if (view == null) return null;

        if (view.bodyRenderer == null)
        {
            SpriteRenderer sprite = view.GetComponentInChildren<SpriteRenderer>(true);
            if (sprite != null)
            {
                view.bodyRenderer = sprite;
                EditorUtility.SetDirty(view);
            }
        }

        return view.bodyRenderer != null ? view.bodyRenderer.transform : null;
    }

    /// <summary>修复已经摆好的 NPC：补 bodyRenderer、补长条尺寸、补巡逻组件。</summary>
    private static void RepairExisting(ResidentView view, NpcPlacementSpec spec)
    {
        if (view == null) return;

        view.area = spec.area;

        Transform body = EnsureBody(view);
        if (body != null)
        {
            Vector3 scale = body.localScale;
            // 之前因为 prefab 装配问题没被拉成长条的，补上（已经拉过的保持用户调整）。
            if (Mathf.Approximately(scale.x, 1f) && Mathf.Approximately(scale.y, 1f))
            {
                body.localScale = new Vector3(spec.barSize.x, spec.barSize.y, 1f);
                EditorUtility.SetDirty(body);
            }
        }

        NpcPatrol2D patrol = view.GetComponent<NpcPatrol2D>();
        if (patrol == null)
        {
            patrol = view.gameObject.AddComponent<NpcPatrol2D>();
            patrol.minX = spec.patrolMinX;
            patrol.maxX = spec.patrolMaxX;
            patrol.speed = spec.patrolSpeed;
            patrol.direction = 1f;
            EditorUtility.SetDirty(patrol);
        }
        if (patrol.enabled) patrol.enabled = false;
    }

    private static string PrefabPathFor(ResidentId id)
    {
        switch (id)
        {
            case ResidentId.Grandma: return PrefabFolder + "/Resident_Grandma.prefab";
            case ResidentId.Uncle: return PrefabFolder + "/Resident_Uncle.prefab";
            case ResidentId.Girl: return PrefabFolder + "/Resident_Girl.prefab";
            case ResidentId.Mother: return PrefabFolder + "/Resident_Mother.prefab";
            default: return null;
        }
    }

    // ------------------------------------------------------------------
    // 场景绑定
    // ------------------------------------------------------------------

    private static void EnsureManagerWiring(Scene scene, GameManager manager)
    {
        List<ResidentView> views = FindAllInScene<ResidentView>(scene);
        views.Sort(delegate (ResidentView a, ResidentView b) { return ((int)a.id).CompareTo((int)b.id); });
        manager.residentViews = views.ToArray();
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

    private static ResidentView FindResidentView(Scene scene, ResidentId id)
    {
        List<ResidentView> views = FindAllInScene<ResidentView>(scene);
        for (int i = 0; i < views.Count; i++)
        {
            if (views[i] != null && views[i].id == id) return views[i];
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

    private static void SetLayerRecursively(GameObject gameObject, int layer)
    {
        gameObject.layer = layer;
        foreach (Transform child in gameObject.transform) SetLayerRecursively(child.gameObject, layer);
    }

    private static void Notify(string message)
    {
        Debug.LogWarning("[余音·NPC] " + message);
        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("余音 · NPC", message, "知道了");
        }
    }
}
