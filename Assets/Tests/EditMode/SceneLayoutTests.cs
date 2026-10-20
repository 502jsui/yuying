using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLayoutTests
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/FirstFloor.unity",
        "Assets/Scenes/SecondFloor.unity",
        "Assets/Scenes/Backyard.unity"
    };

    [TearDown]
    public void TearDown()
    {
        if (System.IO.File.Exists("Assets/Scenes/FirstFloor.unity"))
            EditorSceneManager.OpenScene("Assets/Scenes/FirstFloor.unity", OpenSceneMode.Single);
    }

    [Test]
    public void PanoramaSprite_UsesBackgroundImportSettings()
    {
        TextureImporter importer = AssetImporter.GetAtPath(SceneLayoutMetrics.PanoramaAssetPath) as TextureImporter;
        Assert.IsNotNull(importer, "全景图没有导入");
        Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
        Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
        Assert.AreEqual(100f, importer.spritePixelsPerUnit, 0.001f);
        Assert.AreEqual(FilterMode.Bilinear, importer.filterMode);
        Assert.IsFalse(importer.mipmapEnabled);
        Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
        Assert.GreaterOrEqual(importer.maxTextureSize, 2048);
    }

    [Test]
    public void Config_GirlLivesOnSecondFloor()
    {
        GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Resources/GameConfig.asset");
        Assert.IsNotNull(config);
        Assert.AreEqual(AreaId.SecondFloor, config.GetResidentConfig(ResidentId.Girl).homeArea);
        Assert.AreEqual(AreaId.SecondFloor, config.GetResidentConfig(ResidentId.Mother).homeArea);
    }

    [Test]
    public void Scenes_HaveSingleAlignedPanoramaBackdrop()
    {
        for (int i = 0; i < ScenePaths.Length; i++)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePaths[i], OpenSceneMode.Single);
            AreaId area = AreaIdUtil.FromSceneName(scene.name);
            PanoramaBackdropView[] views = Object.FindObjectsOfType<PanoramaBackdropView>(true);
            Assert.AreEqual(1, views.Length, scene.name + " 应该只有一个全景背景");
            Assert.IsNotNull(views[0].spriteRenderer.sprite, scene.name + " 全景 Sprite 为空");
            Assert.IsTrue(views[0].spriteRenderer.enabled);
            Assert.AreEqual(SceneLayoutMetrics.BackdropSortingOrder, views[0].spriteRenderer.sortingOrder);
            Assert.AreEqual(area, views[0].area);
            Vector3 expected = SceneLayoutMetrics.NormalBackdropPosition(area);
            Assert.AreEqual(expected.x, views[0].transform.position.x, 0.01f);
            Assert.AreEqual(expected.y, views[0].transform.position.y, 0.01f);

            GameObject oldBackdrop = GameObject.Find("Backdrop");
            SpriteRenderer oldRenderer = oldBackdrop != null ? oldBackdrop.GetComponent<SpriteRenderer>() : null;
            if (oldRenderer != null) Assert.IsFalse(oldRenderer.enabled, scene.name + " 旧背景仍可见");
        }
    }

    [Test]
    public void PlacementLayout_MatchDocumentedDistribution()
    {
        Assert.AreEqual(13, ScenePlacementLayout.Count);
        HashSet<string> ids = new HashSet<string>();
        for (int i = 0; i < ScenePlacementLayout.Count; i++)
        {
            ScenePlacementSpec spec = ScenePlacementLayout.Get(i);
            Assert.IsTrue(ids.Add(spec.id), "放置点 ID 重复：" + spec.id);
            Assert.AreNotEqual(AreaId.None, spec.area, "放置点区域不能为空：" + spec.id);
            Assert.AreNotEqual(PlacementSurface.None, spec.surface, "放置面不能为空：" + spec.id);
        }

        AssertPlacementSpecs(AreaId.FirstFloor, 5, 0, 3, 2);
        AssertPlacementSpecs(AreaId.SecondFloor, 4, 2, 1, 1);
        AssertPlacementSpecs(AreaId.Backyard, 4, 2, 1, 1);

        Assert.IsFalse(ScenePlacementLayout.Contains(AreaId.FirstFloor, "first_rack"));
        Assert.IsFalse(ScenePlacementLayout.Contains(AreaId.Backyard, "backyard_ground_b"));
        Assert.IsFalse(ScenePlacementLayout.Contains(AreaId.Backyard, "backyard_ground_c"));
    }

    [Test]
    public void DoorsPortalsAndResidents_MatchPanoramaAnchors()
    {
        Scene first = EditorSceneManager.OpenScene(ScenePaths[0], OpenSceneMode.Single);
        AssertDoor("Door_Grandma", SceneLayoutMetrics.GrandmaDoorX);
        AssertDoor("Door_Aunt", SceneLayoutMetrics.UncleDoorX);
        Assert.IsNull(GameObject.Find("Door_Girl"), "一楼的女孩门应该已移除");
        AssertPortal("Portal_To_Backyard", "SecondFloor");
        AssertResidents(first, ResidentId.Grandma, ResidentId.Uncle);
        AssertPlacementLayout(first);

        Scene second = EditorSceneManager.OpenScene(ScenePaths[1], OpenSceneMode.Single);
        AssertDoor("Door_Mother", SceneLayoutMetrics.MotherDoorX);
        AssertDoor("Door_Girl", SceneLayoutMetrics.GirlDoorX);
        AssertPortal("Stair_To_FirstFloor", "FirstFloor");
        AssertResidents(second, ResidentId.Mother, ResidentId.Girl);
        AssertPlayerOnFloor(second);
        AssertPlacementLayout(second);

        Scene backyard = EditorSceneManager.OpenScene(ScenePaths[2], OpenSceneMode.Single);
        AssertResidents(backyard, ResidentId.Grandma, ResidentId.Uncle, ResidentId.Girl);
        AssertPlacementLayout(backyard);
    }

    private static void AssertDoor(string name, float expectedX)
    {
        GameObject door = GameObject.Find(name);
        Assert.IsNotNull(door, "找不到 " + name);
        Assert.AreEqual(expectedX, door.transform.position.x, 0.01f, name + " 没有对齐全景门位");
        SpriteRenderer[] renderers = door.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].gameObject.name == "Seam Light") continue;
            Assert.IsFalse(renderers[i].enabled, name + " 的旧色块应该隐藏");
        }
    }

    private static void AssertPortal(string name, string targetScene)
    {
        GameObject go = GameObject.Find(name);
        Assert.IsNotNull(go, "找不到 " + name);
        Assert.AreEqual(SceneLayoutMetrics.StairsX, go.transform.position.x, 0.01f,
            name + " 应该对齐全景楼梯");
        ScenePortal2D portal = go.GetComponent<ScenePortal2D>();
        Assert.IsNotNull(portal);
        Assert.IsTrue(
            portal.sceneName == targetScene || portal.alternateSceneName == targetScene,
            name + " 没有连接 " + targetScene);
        Assert.AreEqual(SceneLayoutMetrics.StairsArrivalOffset, portal.arrivalOffset, 0.01f);
        Assert.AreEqual(-1f, portal.arrivalDirection, 0.01f);
    }

    private static void AssertResidents(Scene scene, params ResidentId[] expected)
    {
        ResidentView[] views = Object.FindObjectsOfType<ResidentView>(true);
        Assert.AreEqual(expected.Length, views.Length, scene.name + " 住户数量不对");
        for (int i = 0; i < expected.Length; i++)
        {
            bool found = false;
            for (int v = 0; v < views.Length; v++)
            {
                if (views[v] != null && views[v].id == expected[i] && views[v].gameObject.scene == scene)
                {
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, scene.name + " 缺住户 " + expected[i]);
        }
    }

    private static void AssertPlayerOnFloor(Scene scene)
    {
        PlayerController player = Object.FindObjectOfType<PlayerController>();
        Assert.IsNotNull(player, scene.name + " 缺主角");
        Assert.AreEqual(SceneLayoutMetrics.PlayerY, player.transform.position.y, 0.01f);
        SceneBounds2D bounds = player.GetComponent<SceneBounds2D>();
        Assert.IsNotNull(bounds);
        Assert.AreEqual(SceneLayoutMetrics.PlayerY, bounds.fixedY, 0.01f);
    }

    private static void AssertPlacementLayout(Scene scene)
    {
        List<ScenePlacementSpec> specs = ScenePlacementLayout.ForArea(AreaIdUtil.FromSceneName(scene.name));
        PlacementPoint[] points = Object.FindObjectsOfType<PlacementPoint>(true);
        int ground = 0;
        int tables = 0;
        int racks = 0;
        int total = 0;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null || points[i].gameObject.scene != scene) continue;
            total++;
            if ((points[i].surface & PlacementSurface.Ground) != 0) ground++;
            if ((points[i].surface & PlacementSurface.OnItem) != 0) tables++;
            if ((points[i].surface & PlacementSurface.HangingRack) != 0) racks++;

            ScenePlacementSpec spec = default(ScenePlacementSpec);
            bool found = false;
            for (int s = 0; s < specs.Count; s++)
            {
                if (specs[s].id != points[i].placementId) continue;
                spec = specs[s];
                found = true;
                break;
            }

            Assert.IsTrue(found, scene.name + " 出现了配置外的放置点：" + points[i].placementId);
            Assert.AreEqual(spec.area, points[i].area, points[i].placementId + " 区域不对");
            Assert.AreEqual(spec.surface, points[i].surface, points[i].placementId + " 放置面不对");
            Assert.AreEqual(spec.position.x, points[i].transform.position.x, 0.01f,
                points[i].placementId + " X 坐标不对");
            Assert.AreEqual(spec.position.y, points[i].transform.position.y, 0.01f,
                points[i].placementId + " Y 坐标不对");
        }

        Assert.AreEqual(specs.Count, total, scene.name + " 放置点总数与统一布局不一致");

        if (scene.name == "FirstFloor")
        {
            Assert.AreEqual(3, ground);
            Assert.AreEqual(2, tables);
            Assert.AreEqual(0, racks, "一楼不设衣服点");
            Assert.AreEqual(5, total);
        }
        else if (scene.name == "SecondFloor")
        {
            Assert.AreEqual(1, ground);
            Assert.AreEqual(1, tables);
            Assert.AreEqual(2, racks);
            Assert.AreEqual(4, total);
        }
        else if (scene.name == "Backyard")
        {
            Assert.AreEqual(1, ground);
            Assert.AreEqual(1, tables);
            Assert.AreEqual(2, racks);
            Assert.AreEqual(4, total);
        }
    }

    private static void AssertPlacementSpecs(AreaId area, int total, int racks, int ground, int onItem)
    {
        System.Collections.Generic.List<ScenePlacementSpec> specs = ScenePlacementLayout.ForArea(area);
        int actualRacks = 0;
        int actualGround = 0;
        int actualOnItem = 0;
        for (int i = 0; i < specs.Count; i++)
        {
            if ((specs[i].surface & PlacementSurface.HangingRack) != 0) actualRacks++;
            if ((specs[i].surface & PlacementSurface.Ground) != 0) actualGround++;
            if ((specs[i].surface & PlacementSurface.OnItem) != 0) actualOnItem++;
        }

        Assert.AreEqual(total, specs.Count, area + " 放置点总数不对");
        Assert.AreEqual(racks, actualRacks, area + " 衣服点数量不对");
        Assert.AreEqual(ground, actualGround, area + " 地面点数量不对");
        Assert.AreEqual(onItem, actualOnItem, area + " 叠放点数量不对");
    }
}
