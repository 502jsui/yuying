using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 把正式全景接入三个游戏场景，并把门、楼梯和住户锚到图中的真实位置。
/// 工具幂等：重复执行只会修正全景背景与交互锚点，不重建物品、UI 或角色配置。
/// </summary>
public static class YuyinSceneLayoutTool
{
    private const string FirstFloorPath = "Assets/Scenes/FirstFloor.unity";
    private const string SecondFloorPath = "Assets/Scenes/SecondFloor.unity";
    private const string BackyardPath = "Assets/Scenes/Backyard.unity";
    private const string BackdropName = "PanoramaBackdrop";

    private static readonly string[] PlaceholderNames =
    {
        "Backdrop", "Upper Wall", "Lower Wall", "Ceiling", "Floor", "Floor Line", "Wall Seam",
        "Sky", "Ground", "Path", "Back Fence", "Fence Post", "Stair Step", "Roof", "Roof Edge"
    };

    [MenuItem("Tools/余音/套用全景背景与场景锚点")]
    public static void ApplyFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ApplyAll();
    }

    public static void ApplyFromCommandLine()
    {
        try
        {
            ApplyAll();
            EditorApplication.Exit(0);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void ApplyAll()
    {
        EnsurePanoramaImportSettings();
        Sprite panorama = AssetDatabase.LoadAssetAtPath<Sprite>(SceneLayoutMetrics.PanoramaAssetPath);
        if (panorama == null)
            throw new System.InvalidOperationException("找不到全景 Sprite：" + SceneLayoutMetrics.PanoramaAssetPath);

        ApplyScene(FirstFloorPath, AreaId.FirstFloor, panorama);
        ApplyScene(SecondFloorPath, AreaId.SecondFloor, panorama);
        ApplyScene(BackyardPath, AreaId.Backyard, panorama);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(FirstFloorPath, OpenSceneMode.Single);
        Debug.Log("[余音·场景布局] 全景背景、门、楼梯与住户锚点已应用。");
    }

    private static void ApplyScene(string path, AreaId area, Sprite panorama)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        EnsureBackdrop(scene, area, panorama);
        HidePlaceholderVisuals(scene);
        ApplyDoors(scene, area);
        ApplyPortals(scene, area);
        ApplyCameraFraming(scene, area);
        ApplyPlayerFloor(scene);
        YuyinNpcSceneTool.ApplyPanoramaLayout(scene, area);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[余音·场景布局] 已更新 " + scene.name);
    }

    private static void EnsurePanoramaImportSettings()
    {
        AssetDatabase.ImportAsset(
            SceneLayoutMetrics.PanoramaAssetPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(SceneLayoutMetrics.PanoramaAssetPath) as TextureImporter;
        if (importer == null)
            throw new System.InvalidOperationException("全景图没有导入：" + SceneLayoutMetrics.PanoramaAssetPath);

        bool dirty = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
        if (!Mathf.Approximately(importer.spritePixelsPerUnit, SceneLayoutMetrics.PanoramaPixelsPerUnit))
        {
            importer.spritePixelsPerUnit = SceneLayoutMetrics.PanoramaPixelsPerUnit;
            dirty = true;
        }
        if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; dirty = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
        if (importer.alphaIsTransparency) { importer.alphaIsTransparency = false; dirty = true; }
        if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; dirty = true; }
        if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; dirty = true; }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            dirty = true;
        }

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (settings.spriteAlignment != (int)SpriteAlignment.Center || settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            dirty = true;
        }

        if (dirty) importer.SaveAndReimport();
    }

    private static PanoramaBackdropView EnsureBackdrop(Scene scene, AreaId area, Sprite panorama)
    {
        PanoramaBackdropView[] existing = FindAllInScene<PanoramaBackdropView>(scene);
        PanoramaBackdropView view = existing.Length > 0 ? existing[0] : null;
        for (int i = 1; i < existing.Length; i++)
        {
            if (existing[i] != null) Object.DestroyImmediate(existing[i].gameObject);
        }

        if (view == null)
        {
            Transform environment = FindTransform(scene, "Environment");
            GameObject go = new GameObject(BackdropName);
            if (environment != null) go.transform.SetParent(environment, false);
            else SceneManager.MoveGameObjectToScene(go, scene);
            view = go.AddComponent<PanoramaBackdropView>();
        }

        SpriteRenderer renderer = view.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = view.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = panorama;
        renderer.color = Color.white;
        renderer.sortingOrder = SceneLayoutMetrics.BackdropSortingOrder;
        renderer.enabled = true;

        view.transform.rotation = Quaternion.identity;
        view.transform.position = SceneLayoutMetrics.NormalBackdropPosition(area);
        view.transform.localScale = SceneLayoutMetrics.BackdropScale;
        view.Configure(area, renderer);
        EditorUtility.SetDirty(view);
        EditorUtility.SetDirty(renderer);
        return view;
    }

    private static void HidePlaceholderVisuals(Scene scene)
    {
        SpriteRenderer[] renderers = FindAllInScene<SpriteRenderer>(scene);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.GetComponent<PanoramaBackdropView>() != null) continue;
            if (renderer.gameObject.name == "Seam Light") continue;

            if (ShouldHide(renderer.transform))
            {
                renderer.enabled = false;
                EditorUtility.SetDirty(renderer);
            }
        }
    }

    private static bool ShouldHide(Transform transform)
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name.StartsWith("Door_")) return current.name != "Seam Light";
            if (current.name.StartsWith("Portal_") || current.name.StartsWith("Stair_")) return true;
            for (int i = 0; i < PlaceholderNames.Length; i++)
            {
                if (current.name == PlaceholderNames[i]) return true;
            }
            current = current.parent;
        }
        return false;
    }

    private static void ApplyDoors(Scene scene, AreaId area)
    {
        if (area == AreaId.FirstFloor)
        {
            SetPosition(scene, "Door_Grandma", SceneLayoutMetrics.GrandmaDoorX, SceneLayoutMetrics.FirstFloorDoorY);
            SetPosition(scene, "Door_Aunt", SceneLayoutMetrics.UncleDoorX, SceneLayoutMetrics.FirstFloorDoorY);

            GameObject firstFloorGirlDoor = FindObject(scene, "Door_Girl");
            if (firstFloorGirlDoor != null) Object.DestroyImmediate(firstFloorGirlDoor);
            return;
        }

        if (area == AreaId.SecondFloor)
        {
            SetPosition(scene, "Door_Mother", SceneLayoutMetrics.MotherDoorX, SceneLayoutMetrics.SecondFloorDoorY);
            EnsureLogicalDoor(scene, "Door_Girl", SceneLayoutMetrics.GirlDoorX, SceneLayoutMetrics.SecondFloorDoorY);
        }
    }

    private static void ApplyPortals(Scene scene, AreaId area)
    {
        if (area == AreaId.FirstFloor)
        {
            ScenePortal2D hub = FindComponent<ScenePortal2D>(scene, "Portal_To_Backyard");
            if (hub == null) return;

            hub.transform.position = new Vector3(SceneLayoutMetrics.StairsX, SceneLayoutMetrics.PlayerY, 0f);
            hub.radius = 1.45f;
            hub.arrivalOffset = SceneLayoutMetrics.StairsArrivalOffset;
            hub.arrivalDirection = -1f;
            hub.interactKey = KeyCode.F;
            hub.alternateInteractKey = KeyCode.G;
            hub.alternateArrivalDirection = -1f;
            EditorUtility.SetDirty(hub);
            return;
        }

        if (area == AreaId.SecondFloor)
        {
            ScenePortal2D stairs = FindComponent<ScenePortal2D>(scene, "Stair_To_FirstFloor");
            if (stairs == null) return;

            stairs.transform.position = new Vector3(SceneLayoutMetrics.StairsX, SceneLayoutMetrics.PlayerY, 0f);
            stairs.radius = 1.45f;
            stairs.arrivalOffset = SceneLayoutMetrics.StairsArrivalOffset;
            stairs.arrivalDirection = -1f;
            EditorUtility.SetDirty(stairs);
            return;
        }

        if (area == AreaId.Backyard)
        {
            ScenePortal2D portal = FindComponent<ScenePortal2D>(scene, "Portal_To_FirstFloor");
            if (portal == null) return;
            portal.transform.position = new Vector3(-8.6f, SceneLayoutMetrics.PlayerY, 0f);
            EditorUtility.SetDirty(portal);
        }
    }

    private static void ApplyCameraFraming(Scene scene, AreaId area)
    {
        SceneCameraFollow2D framing = FindComponent<SceneCameraFollow2D>(scene, "Main Camera");
        if (framing == null) return;

        Vector2 vertical = SceneLayoutMetrics.CameraVerticalBounds(area);
        framing.sceneMinY = vertical.x;
        framing.sceneMaxY = vertical.y;
        framing.ApplyFraming();
        EditorUtility.SetDirty(framing);
        EditorUtility.SetDirty(framing.GetComponent<Camera>());
    }

    private static void ApplyPlayerFloor(Scene scene)
    {
        PlayerController player = FindComponent<PlayerController>(scene, "Player");
        if (player == null) return;

        Vector3 position = player.transform.position;
        position.y = SceneLayoutMetrics.PlayerY;
        player.transform.position = position;

        SceneBounds2D bounds = player.GetComponent<SceneBounds2D>();
        if (bounds != null)
        {
            bounds.target = player.transform;
            bounds.fixedY = SceneLayoutMetrics.PlayerY;
            EditorUtility.SetDirty(bounds);
        }
        EditorUtility.SetDirty(player);
    }

    private static void EnsureLogicalDoor(Scene scene, string name, float x, float y)
    {
        GameObject door = FindObject(scene, name);
        if (door == null)
        {
            Transform environment = FindTransform(scene, "Environment");
            door = new GameObject(name);
            if (environment != null) door.transform.SetParent(environment, false);
            else SceneManager.MoveGameObjectToScene(door, scene);
        }
        door.transform.position = new Vector3(x, y, 0f);
        EditorUtility.SetDirty(door);
    }

    private static void SetPosition(Scene scene, string name, float x, float y)
    {
        GameObject target = FindObject(scene, name);
        if (target == null) return;
        target.transform.position = new Vector3(x, y, 0f);
        EditorUtility.SetDirty(target);
    }

    public static GameObject FindObject(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindRecursive(roots[i].transform, name);
            if (found != null) return found.gameObject;
        }
        return null;
    }

    private static Transform FindTransform(Scene scene, string name)
    {
        GameObject found = FindObject(scene, name);
        return found != null ? found.transform : null;
    }

    private static Transform FindRecursive(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindRecursive(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private static T FindComponent<T>(Scene scene, string name) where T : Component
    {
        GameObject target = FindObject(scene, name);
        return target != null ? target.GetComponent<T>() : null;
    }

    private static T[] FindAllInScene<T>(Scene scene) where T : Component
    {
        List<T> result = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++) result.AddRange(roots[i].GetComponentsInChildren<T>(true));
        return result.ToArray();
    }
}
