using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ItemArtEntry
{
    public string itemId;
    public Sprite frontSprite;
    public Sprite sideSprite;
    public float worldHeight = 0.8f;
    public bool hangFromTop;
    public int sortingOrder;
}

/// <summary>
/// 物品贴图唯一事实来源。按 itemId 映射正视/侧视图片、世界高度和衣架锚点。
/// </summary>
[CreateAssetMenu(menuName = "余音/物品美术配置", fileName = "ItemArt")]
public class ItemArtConfig : ScriptableObject
{
    public const string ResourceName = "ItemArt";
    public List<ItemArtEntry> items = new List<ItemArtEntry>();

    public static ItemArtConfig Load()
    {
        return Resources.Load<ItemArtConfig>(ResourceName);
    }

    public ItemArtEntry Get(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || items == null) return null;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].itemId == itemId) return items[i];
        }
        return null;
    }

    /// <summary>创建资源时使用的默认高度与挂载方式。</summary>
    public static ItemArtEntry DefaultEntry(string itemId)
    {
        float height = 0.8f;
        bool hang = false;
        switch (itemId)
        {
            case "grandma_01": height = 1.7f; hang = true; break;
            case "grandma_02": height = 0.9f; break;
            case "grandma_03": height = 0.65f; break;
            case "aunt_01": height = 0.9f; break;
            case "aunt_02": height = 1.0f; break;
            case "aunt_03": height = 0.85f; break;
            case "mother_01": height = 1.7f; hang = true; break;
            case "mother_02": height = 0.6f; break;
            case "mother_03": height = 0.75f; break;
        }

        return new ItemArtEntry
        {
            itemId = itemId,
            worldHeight = height,
            hangFromTop = hang,
            sortingOrder = 0
        };
    }
}
