using UnityEditor;
using UnityEngine;

/// <summary>
/// 把 CharacterArtConfig 里的一条设置刷到角色的 Body（SpriteRenderer）上。
///
/// 场景重建器、NPC 补齐工具、应用美术工具都走这里，保证「角色长什么样」只有一个出口。
/// 逻辑与枢轴无关：立绘底边按 sprite.bounds.min.y 反算，所以图片是 Bottom-Center 还是 Center 枢轴都对。
/// </summary>
public static class CharacterArtApplier
{
    private const string BodyChildName = "Body";
    private static Sprite placeholderSprite;

    /// <summary>现在占位用的内置 1x1 单位方块（UISprite，PPU 1）。</summary>
    public static Sprite PlaceholderSprite
    {
        get
        {
            if (placeholderSprite == null)
            {
                placeholderSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            }
            return placeholderSprite;
        }
    }

    /// <summary>主角：按配置刷 Body，并把 bodyRenderer 接到 PlayerController（走路翻转要用）。</summary>
    public static bool ApplyPlayer(GameObject instance, CharacterArtConfig config)
    {
        if (instance == null) return false;

        SpriteRenderer body = FindBody(instance.transform);
        if (body == null) return false;

        CharacterArtEntry entry = config != null ? config.PlayerEntry : new CharacterArtEntry();
        Apply(body, entry, 1f);

        PlayerController controller = instance.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.bodyRenderer = body;
            controller.flipWithMovement = entry.flipWithMovement;
            EditorUtility.SetDirty(controller);
        }

        EditorUtility.SetDirty(body);
        return true;
    }

    /// <summary>
    /// 住户：有美术按美术，没有就按占位条的宽高拉方块（保持现在的样子）。
    /// 同时把「暖意染色」开关同步到 ResidentView。
    /// </summary>
    public static bool ApplyResident(ResidentView view, NpcPlacementSpec spec, CharacterArtConfig config)
    {
        if (view == null) return false;

        SpriteRenderer body = view.bodyRenderer != null ? view.bodyRenderer : FindBody(view.transform);
        if (body == null) return false;

        CharacterArtEntry entry = config != null
            ? config.ForResident(spec.id)
            : CharacterArtConfig.DefaultResidentEntry(spec.id);

        float placeholderWidth = spec.barSize.x > 0f ? spec.barSize.x : 0.8f;
        float placeholderHeight = spec.barSize.y > 0f ? spec.barSize.y : entry.height;
        if (!entry.HasArt) entry.height = placeholderHeight;

        Apply(body, entry, placeholderWidth);

        if (view.bodyRenderer != body)
        {
            view.bodyRenderer = body;
            EditorUtility.SetDirty(view);
        }
        if (view.tintBodyByWarmth != entry.tintByWarmth)
        {
            view.tintBodyByWarmth = entry.tintByWarmth;
            EditorUtility.SetDirty(view);
        }

        CharacterArtEntry sitting = config != null
            ? config.ForResidentSitting(spec.id)
            : CharacterArtConfig.DefaultSittingEntry(spec.id);
        view.sittingSprite = sitting != null ? sitting.sprite : null;
        view.sittingHeight = sitting != null ? sitting.height : view.sittingHeight;
        view.sittingFootOffsetY = sitting != null ? sitting.footOffsetY : view.sittingFootOffsetY;
        view.sittingSortingOrder = sitting != null ? sitting.sortingOrder : view.sittingSortingOrder;

        CharacterArtEntry walking = config != null
            ? config.ForResidentWalking(spec.id)
            : CharacterArtConfig.DefaultActionEntry(spec.id);
        view.walkingSprite = walking != null ? walking.sprite : null;
        view.walkingHeight = walking != null ? walking.height : view.walkingHeight;
        view.walkingFootOffsetY = walking != null ? walking.footOffsetY : view.walkingFootOffsetY;
        view.walkingSortingOrder = walking != null ? walking.sortingOrder : view.walkingSortingOrder;

        CharacterArtEntry talking = config != null
            ? config.ForResidentTalking(spec.id)
            : CharacterArtConfig.DefaultActionEntry(spec.id);
        view.talkingSprite = talking != null ? talking.sprite : null;
        view.talkingHeight = talking != null ? talking.height : view.talkingHeight;
        view.talkingFootOffsetY = talking != null ? talking.footOffsetY : view.talkingFootOffsetY;
        view.talkingSortingOrder = talking != null ? talking.sortingOrder : view.talkingSortingOrder;
        EditorUtility.SetDirty(view);

        EditorUtility.SetDirty(body);
        return true;
    }

    /// <summary>把一条美术设置刷到 Body 上。entry.sprite 为空时退回占位方块。</summary>
    public static void Apply(SpriteRenderer body, CharacterArtEntry entry, float placeholderWidth)
    {
        if (body == null) return;
        if (entry == null) entry = new CharacterArtEntry();

        // 没配美术时保留原有的占位 sprite（内置方块有好几种，换掉只是无谓的资产改动），
        // 实在没有才退到内置方块。
        Sprite sprite = entry.HasArt
            ? entry.sprite
            : (body.sprite != null ? body.sprite : PlaceholderSprite);
        if (sprite == null) return;

        body.sprite = sprite;

        float spriteHeight = Mathf.Max(0.0001f, sprite.bounds.size.y);
        float spriteWidth = Mathf.Max(0.0001f, sprite.bounds.size.x);
        float height = Mathf.Max(0.05f, entry.height);

        float scaleY = height / spriteHeight;
        float scaleX = entry.HasArt
            ? scaleY
            : Mathf.Max(0.02f, placeholderWidth) / spriteWidth;

        body.transform.localScale = new Vector3(scaleX, scaleY, 1f);

        // 让立绘底边落在 footOffsetY：底边相对枢轴的偏移是 sprite.bounds.min.y。
        float bottomInBody = sprite.bounds.min.y * scaleY;
        body.transform.localPosition = new Vector3(0f, entry.footOffsetY - bottomInBody, 0f);

        body.sortingOrder = entry.sortingOrder;

        // 有真美术时不要再叠暖意色；占位方块的颜色留给 ResidentView 的染色逻辑。
        if (entry.HasArt) body.color = Color.white;

        EditorUtility.SetDirty(body);
    }

    /// <summary>立绘在世界里的尺寸（给检查工具 / 测试用）。</summary>
    public static Vector2 ResolveWorldSize(SpriteRenderer body)
    {
        if (body == null || body.sprite == null) return Vector2.zero;
        Vector3 scale = body.transform.lossyScale;
        return new Vector2(
            Mathf.Abs(body.sprite.bounds.size.x * scale.x),
            Mathf.Abs(body.sprite.bounds.size.y * scale.y));
    }

    /// <summary>底边在世界里的 Y（给检查工具 / 测试用）。</summary>
    public static float ResolveWorldBottom(SpriteRenderer body)
    {
        if (body == null || body.sprite == null) return 0f;
        float scaleY = body.transform.lossyScale.y;
        return body.transform.position.y + body.sprite.bounds.min.y * scaleY;
    }

    public static SpriteRenderer FindBody(Transform root)
    {
        if (root == null) return null;

        Transform child = root.Find(BodyChildName);
        if (child != null)
        {
            SpriteRenderer named = child.GetComponent<SpriteRenderer>();
            if (named != null) return named;
        }

        SpriteRenderer[] all = root.GetComponentsInChildren<SpriteRenderer>(true);
        return all.Length > 0 ? all[0] : null;
    }
}
