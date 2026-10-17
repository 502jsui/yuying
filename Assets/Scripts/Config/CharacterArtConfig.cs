using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一个角色的美术设置。sprite 留空 = 继续用占位方块（内置 1x1 单位方块拉成的竖条）。
/// </summary>
[Serializable]
public class CharacterArtEntry
{
    [Tooltip("立绘。留空表示继续用占位方块。")]
    public Sprite sprite;

    [Tooltip("立绘在世界里的高度（单位）。宽度按图片比例自动算，不用手填。")]
    public float height = 1.4f;

    [Tooltip("立绘底边相对角色 transform 的 Y 偏移。底边正好贴在 transform 上时填 0。")]
    public float footOffsetY = -0.7f;

    [Tooltip("渲染顺序。要比门（0）大、比雨（22/24）小。")]
    public int sortingOrder = 5;

    [Tooltip("走路时按左右方向翻转立绘（主角用）。")]
    public bool flipWithMovement;

    [Tooltip("按暖意给整个立绘染色。占位方块用它做状态提示；换成真美术后应该关掉。")]
    public bool tintByWarmth = true;

    public bool HasArt { get { return sprite != null; } }

    /// <summary>立绘在世界里的尺寸（宽按图片比例算）。没有美术时返回占位条尺寸。</summary>
    public Vector2 ResolveWorldSize(float placeholderWidth)
    {
        if (!HasArt) return new Vector2(placeholderWidth, height);

        float spriteHeight = Mathf.Max(0.0001f, sprite.bounds.size.y);
        float scale = Mathf.Max(0.0001f, height) / spriteHeight;
        return new Vector2(sprite.bounds.size.x * scale, height);
    }
}

[Serializable]
public class ResidentArtEntry
{
    public ResidentId id;
    public CharacterArtEntry art = new CharacterArtEntry();
}

/// <summary>
/// 角色美术的唯一事实来源：谁长什么样、多高、底边放在哪、渲染顺序多少。
///
/// 编辑器工具（YuyinArtTool / YuyinSceneBuilder / YuyinNpcSceneTool）按它刷 prefab 与场景实例，
/// 所以换美术不用改代码：把图放进 Assets/Art/Characters/，在配置里指一下，跑一次
/// 「Tools → 余音 → 应用角色美术（全部场景）」。
/// </summary>
[CreateAssetMenu(menuName = "余音/角色美术配置", fileName = "CharacterArt")]
public class CharacterArtConfig : ScriptableObject
{
    public const string ResourceName = "CharacterArt";

    [Tooltip("主角。默认：高 2.2、底边在 transform 下 0.5、走路翻转、不染色。")]
    public CharacterArtEntry player = new CharacterArtEntry
    {
        height = 2.2f,
        footOffsetY = -0.5f,
        sortingOrder = 5,
        flipWithMovement = true,
        tintByWarmth = false
    };

    [Tooltip("住户：老婆婆 / 大叔 / 小女孩 / 母亲。")]
    public List<ResidentArtEntry> residents = new List<ResidentArtEntry>();

    public static CharacterArtConfig Load()
    {
        return Resources.Load<CharacterArtConfig>(ResourceName);
    }

    public CharacterArtEntry PlayerEntry
    {
        get
        {
            if (player == null) player = new CharacterArtEntry();
            return player;
        }
    }

    /// <summary>拿某个住户的设置；没有就按默认补一条（不会写回资产）。</summary>
    public CharacterArtEntry ForResident(ResidentId id)
    {
        EnsureDefaults();
        for (int i = 0; i < residents.Count; i++)
        {
            if (residents[i] != null && residents[i].id == id) return residents[i].art;
        }

        ResidentArtEntry created = new ResidentArtEntry { id = id };
        created.art = DefaultResidentEntry(id);
        residents.Add(created);
        return created.art;
    }

    /// <summary>补齐缺失的住户条目；已有的一律不动（策划可能已经调过）。</summary>
    public void EnsureDefaults()
    {
        if (player == null)
        {
            player = new CharacterArtEntry
            {
                height = 2.2f,
                footOffsetY = -0.5f,
                sortingOrder = 5,
                flipWithMovement = true,
                tintByWarmth = false
            };
        }

        if (residents == null) residents = new List<ResidentArtEntry>();

        ResidentId[] ids = { ResidentId.Grandma, ResidentId.Uncle, ResidentId.Girl, ResidentId.Mother };
        for (int i = 0; i < ids.Length; i++)
        {
            bool found = false;
            for (int j = 0; j < residents.Count; j++)
            {
                if (residents[j] != null && residents[j].id == ids[i]) { found = true; break; }
            }
            if (found) continue;

            residents.Add(new ResidentArtEntry { id = ids[i], art = DefaultResidentEntry(ids[i]) });
        }
    }

    /// <summary>
    /// 住户默认值：高 = 占位条高度，底边 = transform 下半个身位（保持现在方块占据的框不动作）。
    /// </summary>
    public static CharacterArtEntry DefaultResidentEntry(ResidentId id)
    {
        float height = DefaultPlaceholderHeight(id);
        return new CharacterArtEntry
        {
            height = height,
            footOffsetY = -height * 0.5f,
            sortingOrder = 5,
            flipWithMovement = false,
            tintByWarmth = true
        };
    }

    /// <summary>占位条高度，和 YuyinNpcSceneTool.GetSpecs 里的 barSize.y 一致。</summary>
    public static float DefaultPlaceholderHeight(ResidentId id)
    {
        switch (id)
        {
            case ResidentId.Grandma: return 2.0f;
            case ResidentId.Uncle: return 2.3f;
            case ResidentId.Girl: return 1.4f;
            case ResidentId.Mother: return 2.1f;
            default: return 1.5f;
        }
    }
}
