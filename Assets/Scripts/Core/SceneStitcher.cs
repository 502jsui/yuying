using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// NPC 行动阶段的整栋楼宽镜头：
///   一楼在底层，二楼与一楼同宽并叠在正上方，后院接在一楼右侧。
/// 楼梯在 x=8.6 对齐；整栋楼宽镜头的 16:9 画幅为 38.4 × 21.6。
/// 行动结束再拆开、相机恢复。
/// </summary>
public class SceneStitcher : MonoBehaviour
{
    /// <summary>每个场景的宽度（19.2 = 2 × 5.4 × 16/9），见相机规范。</summary>
    public const float SceneWidth = 19.2f;
    public const float HalfWidth = SceneWidth * 0.5f;
    public const float SceneHeight = 10.8f;
    public const float HalfHeight = SceneHeight * 0.5f;
    private const float FloorOffsetY = 4.8f;

    public static SceneStitcher Instance { get; private set; }
    public static bool IsStitching { get; private set; }

    /// <summary>只在「正在加载拼接副本」这一瞬间为 true：用来让副本的 GameManager 不要接管。</summary>
    public static bool IsLoadingDuplicate { get; private set; }

    /// <summary>拼接是否真的生效（两个副本都已加载完）。</summary>
    public static bool IsActive
    {
        get { return IsStitching && Instance != null && Instance.loaded; }
    }

    /// <summary>玩家所在的那个场景（偏移 0）。</summary>
    public static AreaId BaseArea { get; private set; }

    /// <summary>兼容旧调用：主副本（一楼时是后院，二楼/后院时是一楼）。</summary>
    public static AreaId OtherArea { get; private set; }

    /// <summary>兼容旧调用：主副本的 X 偏移。</summary>
    public static float OtherOffset { get; private set; }

    private const int MaxStitched = 2;
    private static readonly AreaId[] stitchedAreas = new AreaId[MaxStitched];
    private static readonly Vector2[] stitchedOffsets = new Vector2[MaxStitched];
    private static int stitchedCount;

    private bool loaded;
    private readonly List<AreaId> loadedAreas = new List<AreaId>();
    private SceneCameraFollow2D framing;
    private float savedMinX, savedMaxX, savedMinY, savedMaxY;

    /// <summary>二楼屋顶的本地高度：拼接时雨落到这条世界高度以下就隐藏。</summary>
    private const float RoofTopY = 1.4f;
    private const float GroundY = -3.4f;

    private RainController stitchedRain;
    private readonly List<GameObject> disabledRainObjects = new List<GameObject>();
    private PanoramaBackdropView stitchedBackdrop;
    private readonly List<PanoramaBackdropView> disabledBackdrops = new List<PanoramaBackdropView>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        IsStitching = false;
        IsLoadingDuplicate = false;
        loaded = false;
        loadedAreas.Clear();
        stitchedCount = 0;
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
        if (Instance != null)
        {
            Instance.loaded = false;
            Instance.loadedAreas.Clear();
        }
        stitchedCount = 0;
    }

    // ------------------------------------------------------------------
    // 坐标换算
    // ------------------------------------------------------------------

    public static float OffsetFor(AreaId area)
    {
        if (!IsStitching || area == BaseArea) return 0f;
        for (int i = 0; i < stitchedCount; i++)
        {
            if (stitchedAreas[i] == area) return stitchedOffsets[i].x;
        }
        return 0f;
    }

    public static float OffsetYFor(AreaId area)
    {
        if (!IsStitching || area == BaseArea) return 0f;
        for (int i = 0; i < stitchedCount; i++)
        {
            if (stitchedAreas[i] == area) return stitchedOffsets[i].y;
        }
        return 0f;
    }

    public static Vector2 OffsetFor2D(AreaId area)
    {
        if (!IsStitching || area == BaseArea) return Vector2.zero;
        for (int i = 0; i < stitchedCount; i++)
        {
            if (stitchedAreas[i] == area) return stitchedOffsets[i];
        }
        return Vector2.zero;
    }

    public static float WorldX(AreaId area, float localX)
    {
        return localX + OffsetFor(area);
    }

    public static float LocalX(AreaId area, float worldX)
    {
        return worldX - OffsetFor(area);
    }

    /// <summary>
    /// 拼接世界里这个横坐标属于哪个下层区域（一楼/后院）。
    /// 二楼在上方，不参与这个映射；二楼行动不依赖走位。
    /// </summary>
    public static AreaId AreaAtWorldX(float worldX)
    {
        if (!IsStitching) return BaseArea;

        float first = OffsetFor(AreaId.FirstFloor);
        float backyard = OffsetFor(AreaId.Backyard);
        float seam = (first + backyard) * 0.5f;
        return worldX >= seam ? AreaId.Backyard : AreaId.FirstFloor;
    }

    /// <summary>整栋楼拼接后的左右边界（兼容旧调用）。</summary>
    public static void WorldBounds(out float minX, out float maxX)
    {
        float minY, maxY;
        WorldBounds(out minX, out maxX, out minY, out maxY);
    }

    /// <summary>整栋楼拼接后的完整边界（一楼 + 二楼 + 后院）。</summary>
    public static void WorldBounds(out float minX, out float maxX, out float minY, out float maxY)
    {
        minX = -HalfWidth;
        maxX = HalfWidth;
        minY = -HalfHeight;
        maxY = HalfHeight;
        if (!IsStitching) return;

        for (int i = 0; i < stitchedCount; i++)
        {
            Vector2 offset = stitchedOffsets[i];
            minX = Mathf.Min(minX, offset.x - HalfWidth);
            maxX = Mathf.Max(maxX, offset.x + HalfWidth);
            minY = Mathf.Min(minY, offset.y - HalfHeight);
            maxY = Mathf.Max(maxY, offset.y + HalfHeight);
        }
    }

    // ------------------------------------------------------------------
    // 开始 / 结束
    // ------------------------------------------------------------------

    public IEnumerator Begin(AreaId baseArea)
    {
        if (IsStitching) yield break;
        if (baseArea == AreaId.None) yield break;

        BaseArea = baseArea;
        BuildLayout(baseArea);
        IsStitching = true;
        loaded = false;
        loadedAreas.Clear();

        for (int i = 0; i < stitchedCount; i++)
        {
            AreaId area = stitchedAreas[i];
            string sceneName = SceneNameOf(area);
            IsLoadingDuplicate = true;
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (operation != null && !operation.isDone) yield return null;

            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                Vector2 offset = stitchedOffsets[i];
                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    roots[r].transform.position += new Vector3(offset.x, offset.y, 0f);
                }

                DisableDuplicates(scene);
                BindCopyViews(scene);
                loadedAreas.Add(area);
            }
        }

        IsLoadingDuplicate = false;
        loaded = loadedAreas.Count == stitchedCount;
        SetupPanoramaBackdrop();
        SetupStitchedRain();
        yield return AnimateWideFraming();
    }

    public IEnumerator End()
    {
        if (!IsStitching) yield break;

        // 先在拼接状态里把镜头动画拉回单场景，再拆场景，避免瞬切。
        yield return AnimateBackToSingleScene();

        TeardownPanoramaBackdrop();
        TeardownStitchedRain();
        IsStitching = false;
        IsLoadingDuplicate = false;
        loaded = false;

        for (int i = 0; i < loadedAreas.Count; i++)
        {
            AsyncOperation operation = SceneManager.UnloadSceneAsync(SceneNameOf(loadedAreas[i]));
            while (operation != null && !operation.isDone) yield return null;
        }

        loadedAreas.Clear();
        stitchedCount = 0;
        OtherArea = AreaId.None;
        OtherOffset = 0f;
    }

    private static void BuildLayout(AreaId baseArea)
    {
        stitchedCount = 0;
        OtherArea = AreaId.None;
        OtherOffset = 0f;

        if (baseArea == AreaId.FirstFloor)
        {
            AddStitched(AreaId.SecondFloor, 0f, FloorOffsetY);
            AddStitched(AreaId.Backyard, 19.2f, 0f);
            OtherArea = AreaId.Backyard;
            OtherOffset = 19.2f;
        }
        else if (baseArea == AreaId.SecondFloor)
        {
            AddStitched(AreaId.FirstFloor, 0f, -FloorOffsetY);
            AddStitched(AreaId.Backyard, 19.2f, -FloorOffsetY);
            OtherArea = AreaId.FirstFloor;
            OtherOffset = 0f;
        }
        else if (baseArea == AreaId.Backyard)
        {
            AddStitched(AreaId.FirstFloor, -19.2f, 0f);
            AddStitched(AreaId.SecondFloor, -19.2f, FloorOffsetY);
            OtherArea = AreaId.FirstFloor;
            OtherOffset = -19.2f;
        }
    }

    private static void AddStitched(AreaId area, float x, float y)
    {
        if (stitchedCount >= MaxStitched) return;
        stitchedAreas[stitchedCount] = area;
        stitchedOffsets[stitchedCount] = new Vector2(x, y);
        stitchedCount++;
    }

    private static string SceneNameOf(AreaId area)
    {
        switch (area)
        {
            case AreaId.SecondFloor: return "SecondFloor";
            case AreaId.Backyard: return "Backyard";
            default: return "FirstFloor";
        }
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
    /// 否则副本里的 NPC / 物品用的是 Prefab 上的旧数据 → 全被隐藏，看不到其他区域。
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

    private IEnumerator AnimateWideFraming()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("[SceneStitcher] 拼接时找不到主相机，镜头不拉远。");
            yield break;
        }

        framing = camera.GetComponent<SceneCameraFollow2D>();
        if (framing == null)
        {
            Debug.LogWarning("[SceneStitcher] 主相机上没有 SceneCameraFollow2D，镜头不拉远。");
            yield break;
        }

        savedMinX = framing.sceneMinX;
        savedMaxX = framing.sceneMaxX;
        savedMinY = framing.sceneMinY;
        savedMaxY = framing.sceneMaxY;

        float minX, maxX, minY, maxY;
        WorldBounds(out minX, out maxX, out minY, out maxY);
        framing.sceneMinX = minX;
        framing.sceneMaxX = maxX;
        framing.sceneMinY = minY;
        framing.sceneMaxY = maxY;
        framing.AnimateToBounds(minX, maxX, minY, maxY, framing.npcZoomOutDuration);
        while (framing != null && framing.IsAnimating) yield return null;
    }

    private IEnumerator AnimateBackToSingleScene()
    {
        if (framing == null) yield break;

        framing.AnimateToBounds(savedMinX, savedMaxX, savedMinY, savedMaxY, framing.npcZoomBackDuration);
        while (framing != null && framing.IsAnimating) yield return null;

        framing.sceneMinX = savedMinX;
        framing.sceneMaxX = savedMaxX;
        framing.sceneMinY = savedMinY;
        framing.sceneMaxY = savedMaxY;
        framing = null;
    }

    /// <summary>拼接宽镜头只显示一份完整全景，普通区域背景留到结束再恢复。</summary>
    private void SetupPanoramaBackdrop()
    {
        disabledBackdrops.Clear();
        stitchedBackdrop = null;

        PanoramaBackdropView[] views = Object.FindObjectsOfType<PanoramaBackdropView>(true);
        Scene active = SceneManager.GetActiveScene();

        for (int i = 0; i < views.Length; i++)
        {
            PanoramaBackdropView view = views[i];
            if (view == null) continue;
            if (stitchedBackdrop == null && view.gameObject.scene == active)
            {
                stitchedBackdrop = view;
            }
        }

        if (stitchedBackdrop == null && views.Length > 0) stitchedBackdrop = views[0];

        for (int i = 0; i < views.Length; i++)
        {
            PanoramaBackdropView view = views[i];
            if (view == null || view == stitchedBackdrop) continue;
            view.SetVisible(false);
            disabledBackdrops.Add(view);
        }

        if (stitchedBackdrop != null)
        {
            Vector3 firstFloorOffset = new Vector3(
                OffsetFor(AreaId.FirstFloor),
                OffsetYFor(AreaId.FirstFloor),
                0f);
            stitchedBackdrop.ApplyStitchedPose(firstFloorOffset);
        }
        else
        {
            Debug.LogWarning("[SceneStitcher] 拼接时找不到 PanoramaBackdropView，宽镜头没有正式背景。");
        }
    }

    private void TeardownPanoramaBackdrop()
    {
        for (int i = 0; i < disabledBackdrops.Count; i++)
        {
            if (disabledBackdrops[i] != null) disabledBackdrops[i].SetVisible(true);
        }
        disabledBackdrops.Clear();

        if (stitchedBackdrop != null) stitchedBackdrop.RestoreNormalPose();
        stitchedBackdrop = null;
    }

    // ------------------------------------------------------------------
    // 雨 / 雷：铺满整栋楼，雨丝落到二楼屋顶线以下就消失
    // ------------------------------------------------------------------

    private void SetupStitchedRain()
    {
        float minX, maxX, minY, maxY;
        WorldBounds(out minX, out maxX, out minY, out maxY);

        float firstX = OffsetFor(AreaId.FirstFloor);
        float lowerY = OffsetYFor(AreaId.FirstFloor);
        float houseMinX = firstX - HalfWidth;
        float houseMaxX = firstX + HalfWidth;
        float worldGroundY = lowerY + GroundY;
        float roofY = OffsetYFor(AreaId.SecondFloor) + RoofTopY;
        float aspect = Camera.main != null ? Mathf.Max(0.1f, Camera.main.aspect) : 16f / 9f;
        float rainHeight = Mathf.Max(maxY - minY, (maxX - minX) / aspect);

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
                    new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f),
                    maxX - minX,
                    rainHeight,
                    worldGroundY,
                    houseMinX,
                    houseMaxX,
                    roofY);
            }
            else
            {
                disabledRainObjects.Add(rain.gameObject);
                rain.gameObject.SetActive(false);
            }
        }
    }

    private void TeardownStitchedRain()
    {
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
