using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// NPC 行动阶段：把「一楼」和「后院」左右无缝拼成一个整场景，
/// 相机拉远看全两边，住户就能自由穿梭两个场景（放物品 / 停留）。
/// 行动阶段结束再拆开、相机恢复。
///
/// 拼接规则：一楼永远在后院左边，接缝在两边相接的那条边上。
/// </summary>
public class SceneStitcher : MonoBehaviour
{
    /// <summary>每个场景的宽度（19.2 = 2 × 5.4 × 16/9），见相机规范。</summary>
    public const float SceneWidth = 19.2f;
    public const float HalfWidth = SceneWidth * 0.5f;

    public static SceneStitcher Instance { get; private set; }
    public static bool IsStitching { get; private set; }

    /// <summary>只在「正在加载拼接副本」这一瞬间为 true：用来让副本的 GameManager 不要接管。</summary>
    public static bool IsLoadingDuplicate { get; private set; }

    /// <summary>拼接是否真的生效（副本已加载完）。</summary>
    public static bool IsActive
    {
        get { return IsStitching && Instance != null && Instance.loaded; }
    }

    /// <summary>玩家所在的那个场景（偏移 0）。</summary>
    public static AreaId BaseArea { get; private set; }

    /// <summary>拼接进来的另一个场景。</summary>
    public static AreaId OtherArea { get; private set; }

    /// <summary>另一个场景整体的 X 偏移。</summary>
    public static float OtherOffset { get; private set; }

    private bool loaded;
    private SceneCameraFollow2D framing;
    private float savedMinX, savedMaxX, savedMinY, savedMaxY;

    /// <summary>一楼天花板（盖子）的高度：雨落到这条线就"消失"。</summary>
    private const float RoofTopY = 4.35f;

    private RainController stitchedRain;
    private readonly System.Collections.Generic.List<GameObject> disabledRainObjects =
        new System.Collections.Generic.List<GameObject>();
    private GameObject roofLid;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        // 新的 GameRoot：清掉可能残留的拼接状态（上一轮被打断时可能没 End）。
        IsStitching = false;
        IsLoadingDuplicate = false;
        loaded = false;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        IsStitching = false;
        IsLoadingDuplicate = false;
    }

    /// <summary>场景被 Single 方式重新加载（真正换场景）时兜底清状态。</summary>
    public static void ResetStatics()
    {
        IsStitching = false;
        IsLoadingDuplicate = false;
        if (Instance != null) Instance.loaded = false;
    }

    // ------------------------------------------------------------------
    // 坐标换算
    // ------------------------------------------------------------------

    public static float OffsetFor(AreaId area)
    {
        if (!IsStitching) return 0f;
        if (area == OtherArea) return OtherOffset;
        return 0f;
    }

    public static float WorldX(AreaId area, float localX)
    {
        return localX + OffsetFor(area);
    }

    public static float LocalX(AreaId area, float worldX)
    {
        return worldX - OffsetFor(area);
    }

    /// <summary>拼接世界里这个横坐标属于哪个区域。</summary>
    public static AreaId AreaAtWorldX(float worldX)
    {
        if (!IsStitching) return BaseArea;

        if (OtherOffset > 0f) return worldX >= HalfWidth ? OtherArea : BaseArea;
        return worldX <= -HalfWidth ? OtherArea : BaseArea;
    }

    /// <summary>拼接世界的左右边界。</summary>
    public static void WorldBounds(out float minX, out float maxX)
    {
        float a = Mathf.Min(0f, OtherOffset) - HalfWidth;
        float b = Mathf.Max(0f, OtherOffset) + HalfWidth;
        minX = a;
        maxX = b;
    }

    // ------------------------------------------------------------------
    // 开始 / 结束
    // ------------------------------------------------------------------

    public IEnumerator Begin(AreaId baseArea)
    {
        if (IsStitching) yield break;
        if (baseArea == AreaId.None) yield break;

        BaseArea = baseArea;
        OtherArea = baseArea == AreaId.FirstFloor ? AreaId.Backyard : AreaId.FirstFloor;
        OtherOffset = OtherArea == AreaId.Backyard ? SceneWidth : -SceneWidth;
        IsStitching = true;

        string otherScene = SceneNameOf(OtherArea);
        IsLoadingDuplicate = true;
        AsyncOperation operation = SceneManager.LoadSceneAsync(otherScene, LoadSceneMode.Additive);
        while (operation != null && !operation.isDone) yield return null;

        Scene scene = SceneManager.GetSceneByName(otherScene);
        if (scene.IsValid() && scene.isLoaded)
        {
            loaded = true;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                roots[i].transform.position += new Vector3(OtherOffset, 0f, 0f);
            }

            DisableDuplicates(scene);
            BindCopyViews(scene);
        }

        IsLoadingDuplicate = false;
        SetupStitchedRain();
        ApplyWideFraming();
    }

    public IEnumerator End()
    {
        if (!IsStitching) yield break;

        IsStitching = false;
        IsLoadingDuplicate = false;
        RestoreFraming();
        TeardownStitchedRain();

        if (loaded)
        {
            loaded = false;
            AsyncOperation operation = SceneManager.UnloadSceneAsync(SceneNameOf(OtherArea));
            while (operation != null && !operation.isDone) yield return null;
        }
    }

    private static string SceneNameOf(AreaId area)
    {
        return area == AreaId.Backyard ? "Backyard" : "FirstFloor";
    }

    /// <summary>拼接副本只负责显示：关掉它自己的 GameManager / 相机 / 音频 / 玩家。</summary>
    private static void DisableDuplicates(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameManager[] managers = roots[i].GetComponentsInChildren<GameManager>(true);
            for (int m = 0; m < managers.Length; m++) managers[m].enabled = false;

            Camera[] cameras = roots[i].GetComponentsInChildren<Camera>(true);
            for (int c = 0; c < cameras.Length; c++) cameras[c].enabled = false;

            AudioListener[] listeners = roots[i].GetComponentsInChildren<AudioListener>(true);
            for (int l = 0; l < listeners.Length; l++) listeners[l].enabled = false;

            PlayerController[] players = roots[i].GetComponentsInChildren<PlayerController>(true);
            for (int p = 0; p < players.Length; p++) players[p].gameObject.SetActive(false);

        }
    }

    /// <summary>
    /// 拼接副本自己的 GameManager 被关掉了，这里手动把它的 View 接到本局数据上，
    /// 否则副本里的 NPC / 物品用的是 Prefab 上的旧数据 → 全被隐藏，看不到对面场景。
    /// </summary>
    private static void BindCopyViews(Scene scene)
    {
        GameRoot root = GameRoot.Instance;
        if (root == null || root.Session == null || root.Config == null) return;

        GameSession session = root.Session;
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            ResidentView[] residents = roots[i].GetComponentsInChildren<ResidentView>(true);
            for (int r = 0; r < residents.Length; r++)
            {
                ResidentView view = residents[r];
                if (view == null) continue;
                view.Bind(session.EnsureResident(view.id, root.Config));
            }

            ItemView[] items = roots[i].GetComponentsInChildren<ItemView>(true);
            for (int it = 0; it < items.Length; it++)
            {
                ItemView view = items[it];
                if (view == null) continue;
                view.Bind(session.EnsureItem(view.itemId, view.owner, view.type));
            }
        }
    }

    private void ApplyWideFraming()
    {
        Camera camera = Camera.main;
        if (camera == null) return;

        framing = camera.GetComponent<SceneCameraFollow2D>();
        if (framing == null) return;

        savedMinX = framing.sceneMinX;
        savedMaxX = framing.sceneMaxX;
        savedMinY = framing.sceneMinY;
        savedMaxY = framing.sceneMaxY;

        float minX, maxX;
        WorldBounds(out minX, out maxX);
        framing.sceneMinX = minX;
        framing.sceneMaxX = maxX;
        framing.ApplyFraming();
    }

    private void RestoreFraming()
    {
        if (framing == null) return;

        // 硬恢复成单场景的一屏范围，避免残留的拉远 / 偏移。
        framing.sceneMinX = -HalfWidth;
        framing.sceneMaxX = HalfWidth;
        framing.sceneMinY = -5.4f;
        framing.sceneMaxY = 5.4f;
        framing.ApplyFraming();
        framing = null;
    }

    // ------------------------------------------------------------------
    // 雨 / 雷：铺满整个拼接场景，一楼那半边加「盖子」
    // ------------------------------------------------------------------

    private void SetupStitchedRain()
    {
        float minX, maxX;
        WorldBounds(out minX, out maxX);

        // 一楼在拼接世界里的左右范围（可能在左半边或右半边）。
        float houseMinX = OffsetFor(AreaId.FirstFloor) - HalfWidth;
        float houseMaxX = OffsetFor(AreaId.FirstFloor) + HalfWidth;

        RainController[] rains = Object.FindObjectsOfType<RainController>();
        stitchedRain = null;
        disabledRainObjects.Clear();

        for (int i = 0; i < rains.Length; i++)
        {
            RainController rain = rains[i];
            if (rain == null || !rain.isActiveAndEnabled) continue;

            if (stitchedRain == null)
            {
                stitchedRain = rain;
                rain.ApplyStitchedLayout(
                    new Vector3((minX + maxX) * 0.5f, 0f, 0f),
                    maxX - minX,
                    10.8f,
                    houseMinX,
                    houseMaxX,
                    RoofTopY);
            }
            else
            {
                // 只留一套雨，避免两边叠加成双倍雨量。
                disabledRainObjects.Add(rain.gameObject);
                rain.gameObject.SetActive(false);
            }
        }

        CreateRoofLid(houseMinX, houseMaxX);
    }

    /// <summary>一楼头上的盖子：画在雨的上层，雨丝到这条线就消失。</summary>
    private void CreateRoofLid(float minX, float maxX)
    {
        roofLid = new GameObject("Stitch_RoofLid");
        roofLid.transform.position = new Vector3((minX + maxX) * 0.5f, RoofTopY, 0f);
        roofLid.transform.localScale = new Vector3(Mathf.Max(0.1f, maxX - minX), 0.5f, 1f);

        SpriteRenderer renderer = roofLid.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        renderer.color = new Color(0.09f, 0.10f, 0.12f, 1f);
        renderer.sortingOrder = 60; // 高于前景雨(30)
    }

    private void TeardownStitchedRain()
    {
        if (roofLid != null)
        {
            Object.Destroy(roofLid);
            roofLid = null;
        }

        for (int i = 0; i < disabledRainObjects.Count; i++)
        {
            if (disabledRainObjects[i] != null) disabledRainObjects[i].SetActive(true);
        }
        disabledRainObjects.Clear();

        if (stitchedRain != null)
        {
            stitchedRain.RestoreToCameraFit();
            stitchedRain = null;
        }
    }
}
