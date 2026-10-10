using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景里的一个物品放置点。物品系统只认 placementId + area + surface，
/// 不直接认 Transform，所以换场景 / 换美术不影响逻辑。
///
/// 用法：在场景里建空物体，挂上这个组件，填 placementId、area、surface。
/// 需要在同一位置吸附物品时，把 anchor 指向真正要吸附的 Transform（可留空）。
/// </summary>
public class PlacementPoint : MonoBehaviour
{
    [Header("身份")]
    [Tooltip("唯一 ID，要和 ItemConfig.startPlacementId / ItemData.placementId 对应。")]
    public string placementId;

    [Tooltip("留 None 时按所在场景名自动判断。")]
    public AreaId area = AreaId.None;

    [Header("规则")]
    [Tooltip("这个点属于哪种放置面，决定什么物品能放。")]
    public PlacementSurface surface = PlacementSurface.Ground;

    [Tooltip("可选：只允许指定 itemId 放这里。留空表示不限。")]
    public string allowedItemId;

    [Header("吸附")]
    [Tooltip("留空则用自身 Transform 的位置。")]
    public Transform anchor;

    private static readonly Dictionary<string, PlacementPoint> points = new Dictionary<string, PlacementPoint>();

    public Vector3 AnchorPosition
    {
        get { return anchor != null ? anchor.position : transform.position; }
    }

    private void Awake()
    {
        if (area == AreaId.None) area = AreaIdUtil.FromSceneName(gameObject.scene.name);
    }

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(placementId)) return;
        points[placementId] = this;
        PlacementRegistry.Register(new PlacementOption
        {
            placementId = placementId,
            area = area,
            surface = surface,
            allowedItemId = allowedItemId
        });
    }

    private void OnDisable()
    {
        if (string.IsNullOrEmpty(placementId)) return;
        PlacementRegistry.Unregister(placementId);
        PlacementPoint existing;
        if (points.TryGetValue(placementId, out existing) && existing == this)
        {
            points.Remove(placementId);
        }
    }

    /// <summary>这个点能不能放这件物品（同时看放置面和指定 itemId 限制）。</summary>
    public bool Accepts(ItemData item)
    {
        if (item == null) return false;
        if (!string.IsNullOrEmpty(allowedItemId) && item.itemId != allowedItemId) return false;
        return ItemPlacementRules.CanPlace(item.type, surface);
    }

    public static PlacementPoint Find(string placementId)
    {
        if (string.IsNullOrEmpty(placementId)) return null;
        PlacementPoint point;
        return points.TryGetValue(placementId, out point) ? point : null;
    }

    public static void Clear()
    {
        points.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        points.Clear();
    }
}
