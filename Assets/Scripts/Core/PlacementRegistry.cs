using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景里一个可用的放置点（纯数据，不含 Transform）。
/// 逻辑层（NPC 行为 AI）用它来挑放置点，不看具体场景物体。
/// </summary>
public struct PlacementOption
{
    public string placementId;
    public AreaId area;
    public PlacementSurface surface;
    public string allowedItemId;

    public bool IsValid
    {
        get { return !string.IsNullOrEmpty(placementId); }
    }

    public bool Accepts(ItemData item)
    {
        if (item == null) return false;
        if (!string.IsNullOrEmpty(allowedItemId) && item.itemId != allowedItemId) return false;
        return ItemPlacementRules.CanPlace(item.type, surface);
    }
}

/// <summary>
/// 放置点的运行时注册表。PlacementPoint（表现层）在 OnEnable / OnDisable 里登记，
/// 逻辑层只通过这里查询「有哪些点、能不能放」。
/// </summary>
public static class PlacementRegistry
{
    private static readonly Dictionary<string, PlacementOption> options = new Dictionary<string, PlacementOption>();
    private static readonly HashSet<string> occupied = new HashSet<string>();

    public static void Register(PlacementOption option)
    {
        if (!option.IsValid) return;
        options[option.placementId] = option;
    }

    public static void Unregister(string placementId)
    {
        if (string.IsNullOrEmpty(placementId)) return;
        options.Remove(placementId);
        occupied.Remove(placementId);
    }

    public static bool TryGet(string placementId, out PlacementOption option)
    {
        if (string.IsNullOrEmpty(placementId))
        {
            option = default(PlacementOption);
            return false;
        }
        return options.TryGetValue(placementId, out option);
    }

    public static List<PlacementOption> GetAll()
    {
        List<PlacementOption> result = new List<PlacementOption>();
        foreach (KeyValuePair<string, PlacementOption> pair in options)
        {
            result.Add(pair.Value);
        }
        return result;
    }

    public static List<PlacementOption> GetInArea(AreaId area)
    {
        List<PlacementOption> result = new List<PlacementOption>();
        foreach (KeyValuePair<string, PlacementOption> pair in options)
        {
            if (pair.Value.area == area) result.Add(pair.Value);
        }
        return result;
    }

    public static bool IsOccupied(string placementId)
    {
        return !string.IsNullOrEmpty(placementId) && occupied.Contains(placementId);
    }

    public static void MarkOccupied(string placementId)
    {
        if (!string.IsNullOrEmpty(placementId)) occupied.Add(placementId);
    }

    public static void Release(string placementId)
    {
        if (!string.IsNullOrEmpty(placementId)) occupied.Remove(placementId);
    }

    /// <summary>只清占用状态，保留场景注册的放置点（重开一局时用）。</summary>
    public static void ClearOccupancy()
    {
        occupied.Clear();
    }

    public static void Clear()
    {
        options.Clear();
        occupied.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        Clear();
    }
}
