using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景放置点的唯一布局定义。
/// 编辑器生成工具和结局模拟器都从这里读取，避免两套坐标 / 区域规则逐渐漂移。
/// </summary>
public struct ScenePlacementSpec
{
    public readonly string id;
    public readonly AreaId area;
    public readonly PlacementSurface surface;
    public readonly Vector3 position;

    public ScenePlacementSpec(string id, AreaId area, PlacementSurface surface, float x, float y)
    {
        this.id = id;
        this.area = area;
        this.surface = surface;
        position = new Vector3(x, y, 0f);
    }
}

public static class ScenePlacementLayout
{
    private static readonly ScenePlacementSpec[] entries =
    {
        new ScenePlacementSpec("first_ground_a", AreaId.FirstFloor, PlacementSurface.Ground, -6.64f, -3.14f),
        new ScenePlacementSpec("first_ground_b", AreaId.FirstFloor, PlacementSurface.Ground, -4.16f, -3.05f),
        new ScenePlacementSpec("first_ground_c", AreaId.FirstFloor, PlacementSurface.Ground, 5.5f, -2.9f),
        new ScenePlacementSpec("first_on_item", AreaId.FirstFloor, PlacementSurface.OnItem, 0.67f, -3.52f),
        new ScenePlacementSpec("first_table_b", AreaId.FirstFloor, PlacementSurface.OnItem, -2.31f, -1.8f),

        new ScenePlacementSpec("second_rack", AreaId.SecondFloor, PlacementSurface.HangingRack, -0.04f, -2.14f),
        new ScenePlacementSpec("second_rack_b", AreaId.SecondFloor, PlacementSurface.HangingRack, 2.49f, -2.07f),
        new ScenePlacementSpec("second_ground", AreaId.SecondFloor, PlacementSurface.Ground, -2f, -3.27f),
        new ScenePlacementSpec("second_on_item", AreaId.SecondFloor, PlacementSurface.OnItem, 3.91f, -3.39f),

        new ScenePlacementSpec("backyard_rack_a", AreaId.Backyard, PlacementSurface.HangingRack, -1.86f, -1.26f),
        new ScenePlacementSpec("backyard_rack_b", AreaId.Backyard, PlacementSurface.HangingRack, 4f, -1.44f),
        new ScenePlacementSpec("backyard_ground_a", AreaId.Backyard, PlacementSurface.Ground, -2f, -2.9f),
        new ScenePlacementSpec("backyard_on_item", AreaId.Backyard, PlacementSurface.OnItem, -5.97f, -3.56f)
    };

    public static int Count
    {
        get { return entries.Length; }
    }

    public static ScenePlacementSpec Get(int index)
    {
        return entries[index];
    }

    public static List<ScenePlacementSpec> ForArea(AreaId area)
    {
        List<ScenePlacementSpec> result = new List<ScenePlacementSpec>();
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].area == area) result.Add(entries[i]);
        }
        return result;
    }

    public static bool Contains(AreaId area, string placementId)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].area == area && entries[i].id == placementId) return true;
        }
        return false;
    }
}
