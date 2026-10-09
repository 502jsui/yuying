using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 切场景不能丢状态：来回切一楼 / 后院，NPC 与物品的数据必须一模一样。
/// </summary>
public class SceneSwitchTests
{
    private GameObject sceneBinding;

    [TearDown]
    public void TearDown()
    {
        if (sceneBinding != null) Object.Destroy(sceneBinding);
        if (GameRoot.Instance != null) Object.Destroy(GameRoot.Instance.gameObject);
    }

    [UnityTest]
    public IEnumerator SceneSwitch_KeepsNpcAndItemState()
    {
        yield return null;

        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        AsyncOperation load = SceneManager.LoadSceneAsync("FirstFloor", LoadSceneMode.Single);
        while (load != null && !load.isDone) yield return null;
        yield return null;
        yield return null;

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; },
            10f,
            "一楼进入玩家输入");

        string before = Snapshot(GameRoot.Instance.Session);

        AsyncOperation toBackyard = SceneManager.LoadSceneAsync("Backyard", LoadSceneMode.Single);
        while (toBackyard != null && !toBackyard.isDone) yield return null;
        yield return null;
        yield return null;

        string after = Snapshot(GameRoot.Instance.Session);
        Assert.AreEqual(before, after, "切到后院不应该改变 NPC / 物品状态。");

        AsyncOperation back = SceneManager.LoadSceneAsync("FirstFloor", LoadSceneMode.Single);
        while (back != null && !back.isDone) yield return null;
        yield return null;
        yield return null;

        string backHome = Snapshot(GameRoot.Instance.Session);
        Assert.AreEqual(before, backHome, "切回一楼状态也不应该变。");

        // 顺便确认：一楼那份 NPC View 还在一楼的位置上（位置也保留了）。
        ResidentView grandma = null;
        ResidentView[] views = Object.FindObjectsOfType<ResidentView>();
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null && views[i].id == ResidentId.Grandma) { grandma = views[i]; break; }
        }
        Assert.IsNotNull(grandma, "一楼应该有老婆婆的 View。");
    }

    [UnityTest]
    public IEnumerator WalkRightFromFirstFloor_ArrivesAtBackyardLeft()
    {
        yield return null;

        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        AsyncOperation load = SceneManager.LoadSceneAsync("FirstFloor", LoadSceneMode.Single);
        while (load != null && !load.isDone) yield return null;
        yield return null;
        yield return null;

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; },
            10f,
            "一楼进入玩家输入");

        PlayerController player = Object.FindObjectOfType<PlayerController>();
        Assert.IsNotNull(player, "一楼应该有玩家。");

        // 先站远一点，让传送门重新武装。
        player.transform.position = new Vector3(3f, -2.4f, 0f);
        yield return null;
        yield return null;

        // 一直往右走，走到一楼右边的传送门。
        float elapsed = 0f;
        while (SceneManager.GetActiveScene().name != "Backyard" && elapsed < 8f)
        {
            player.transform.position += new Vector3(0.12f, 0f, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Assert.AreEqual("Backyard", SceneManager.GetActiveScene().name, "从一楼右边走进去应该切到后院。");

        yield return WaitUntil(
            delegate { return !SceneTransitionState.IsLoading; },
            5f,
            "淡入结束（不能卡在黑屏）");

        PlayerController inBackyard = Object.FindObjectOfType<PlayerController>();
        Assert.IsNotNull(inBackyard, "后院应该有玩家。");
        Assert.Less(inBackyard.transform.position.x, -4f, "应该落在后院左边（和一楼右边连在一起）。");
        Assert.Greater(inBackyard.transform.position.x, -9.5f, "落点要在场景内。");
    }

    [UnityTest]
    public IEnumerator NpcPhase_StitchesBothScenes_AndShowsTheirNpcs()
    {
        yield return null;

        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        AsyncOperation load = SceneManager.LoadSceneAsync("FirstFloor", LoadSceneMode.Single);
        while (load != null && !load.isDone) yield return null;
        yield return null;
        yield return null;

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; },
            10f,
            "一楼进入玩家输入");

        GameRoot.Instance.Flow.RegisterPlayerTouch("stitch_test");

        yield return WaitUntil(
            delegate { return SceneStitcher.IsActive; },
            15f,
            "NPC 行动阶段把两个场景拼起来");

        // 相机应该拉远到能同时看见两个场景（单场景是 5.4，两个是 ~10.8）。
        Camera camera = Camera.main;
        Assert.IsNotNull(camera, "应该有主相机。");
        Assert.Greater(camera.orthographicSize, 8f, "NPC 行动时相机要拉远看全两个场景。");

        // 拼接进来的后院场景里的 NPC View 必须绑到本局数据，否则看不到对面的行动。
        Scene backyard = SceneManager.GetSceneByName("Backyard");
        Assert.IsTrue(backyard.IsValid() && backyard.isLoaded, "后院应该被拼接进来。");

        ResidentView copyView = null;
        GameObject[] roots = backyard.GetRootGameObjects();
        for (int i = 0; i < roots.Length && copyView == null; i++)
        {
            ResidentView[] views = roots[i].GetComponentsInChildren<ResidentView>(true);
            for (int v = 0; v < views.Length; v++)
            {
                if (views[v] != null && views[v].id == ResidentId.Grandma) { copyView = views[v]; break; }
            }
        }

        Assert.IsNotNull(copyView, "后院拼接副本里应该有老婆婆的 View。");
        Assert.IsNotNull(copyView.data, "拼接副本的 View 必须绑到本局数据（否则会像之前一样全部隐藏）。");

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Session.round == 2; },
            40f,
            "本轮 NPC 行动结束");
    }

    [UnityTest]
    public IEnumerator Diagnostic_LogNpcPositions()
    {
        yield return null;

        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        AsyncOperation load = SceneManager.LoadSceneAsync("FirstFloor", LoadSceneMode.Single);
        while (load != null && !load.isDone) yield return null;
        yield return null;
        yield return null;

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; },
            10f,
            "玩家输入");

        LogNpc("[诊断] 行动前");
        GameRoot.Instance.Flow.RegisterPlayerTouch("diag");

        yield return WaitUntil(delegate { return SceneStitcher.IsActive; }, 15f, "拼接生效");
        yield return null;
        LogNpc("[诊断] 拼接中");

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Session.round == 2 && !SceneStitcher.IsActive; },
            40f,
            "本轮结束");
        yield return null;
        LogNpc("[诊断] 结算后");

        Assert.Pass();
    }

    private static void LogNpc(string tag)
    {
        ResidentView[] views = Object.FindObjectsOfType<ResidentView>();
        for (int i = 0; i < views.Length; i++)
        {
            ResidentView v = views[i];
            if (v == null) continue;
            ResidentData d = v.data;
            Debug.Log(string.Format(
                "{0} {1} area={2} worldY={3:0.###} homeY={4:0.###} dataArea={5} dataX={6:0.###} dataGroundY={7:0.###} out={8}",
                tag, v.id, v.area, v.transform.position.y, v.HomePosition.y,
                d != null ? d.currentArea.ToString() : "-",
                d != null ? d.currentX : 0f,
                d != null ? d.groundY : 0f,
                d != null ? d.isOut.ToString() : "-"));
        }
    }

    [UnityTest]
    public IEnumerator Rain_HasGroundSplash_ButNoGroundWater()
    {
        yield return null;

        AsyncOperation load = SceneManager.LoadSceneAsync("Backyard", LoadSceneMode.Single);
        while (load != null && !load.isDone) yield return null;
        yield return null;
        yield return null;

        RainController rain = Object.FindObjectOfType<RainController>();
        Assert.IsNotNull(rain, "后院应该有雨。");
        Assert.IsFalse(rain.groundWater, "积水应该默认关闭。");
        Assert.IsTrue(rain.groundSplash, "雨落在地面上的效果应该开启。");
        Assert.IsNotNull(rain.SplashRain, "应该有雨落在地上的溅水。");
        Assert.IsNull(rain.transform.Find("Wet_Ground_Sheen"), "不应该有地面水光。");
        Assert.IsNull(rain.transform.Find("Puddle_0"), "不应该有水洼。");

        // 雨丝本身还在。
        Assert.IsNotNull(rain.BackgroundRain, "雨丝要保留。");
        Assert.IsNotNull(rain.ForegroundRain, "雨丝要保留。");
    }

    [UnityTest]
    public IEnumerator NpcPhase_RainCoversWholeStitchedScene_WithRoofOverFirstFloor()
    {
        yield return null;

        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        AsyncOperation load = SceneManager.LoadSceneAsync("FirstFloor", LoadSceneMode.Single);
        while (load != null && !load.isDone) yield return null;
        yield return null;
        yield return null;

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; },
            10f,
            "玩家输入");

        GameRoot.Instance.Flow.RegisterPlayerTouch("rain_stitch_test");

        yield return WaitUntil(delegate { return SceneStitcher.IsActive; }, 15f, "拼接生效");
        yield return null;

        RainController rain = Object.FindObjectOfType<RainController>();
        Assert.IsNotNull(rain, "拼接时应该有雨。");
        Assert.Greater(rain.width, 30f, "雨要铺满拼接后的整个场景（两个场景宽约 38.4）。");
        Assert.IsNotNull(rain.BackgroundRain, "背景雨要保留。");
        Assert.IsTrue(rain.BackgroundRain.useExclusion, "一楼那半边要有屋盖排除（雨落在盖子上）。");

        // 一楼屋顶下不能有雨丝；地面溅水也不能落到屋顶下。
        float houseCenter = SceneStitcher.OffsetFor(AreaId.FirstFloor);
        float houseMin = houseCenter - SceneStitcher.HalfWidth;
        float houseMax = houseCenter + SceneStitcher.HalfWidth;
        const float roofTopY = 4.35f;

        RainStreakField2D[] fields = Object.FindObjectsOfType<RainStreakField2D>();
        int visibleStreaks = 0;
        for (int f = 0; f < fields.Length; f++)
        {
            SpriteRenderer[] renderers = fields[f].GetComponentsInChildren<SpriteRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                SpriteRenderer sr = renderers[r];
                if (!sr.enabled) continue;
                visibleStreaks++;

                Vector3 p = sr.transform.position;
                bool insideHouse = p.x > houseMin && p.x < houseMax;
                Assert.IsFalse(insideHouse && p.y < roofTopY, "一楼屋顶下面不能有雨丝。");
            }
        }
        Assert.Greater(visibleStreaks, 0, "应该检查到可见雨丝。");

        if (rain.SplashRain != null)
        {
            ParticleSystem.ShapeModule shape = rain.SplashRain.shape;
            float splashCenterX = rain.SplashRain.transform.position.x + shape.position.x;
            float splashHalfWidth = shape.scale.x * 0.5f;
            bool overlapsHouse = splashCenterX + splashHalfWidth > houseMin && splashCenterX - splashHalfWidth < houseMax;
            Assert.IsFalse(overlapsHouse, "地面溅水不能落到一楼屋顶下。");
        }

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Session.round == 2 && !SceneStitcher.IsActive; },
            40f,
            "本轮结束");

        Assert.Less(rain.width, 30f, "结算后雨要恢复成单场景大小。");
    }

    private static string Snapshot(GameSession session)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("round=").Append(session.round).Append('\n');

        for (int i = 0; i < session.residents.Count; i++)
        {
            ResidentData r = session.residents[i];
            sb.Append("NPC ").Append(r.id)
              .Append(" state=").Append(r.state)
              .Append(" area=").Append(r.currentArea)
              .Append(" stayOut=").Append(r.stayOutArea)
              .Append(" warm=").Append(r.warmth.ToString("0.##"))
              .Append(" know=").Append(r.awareness.ToString("0.##"))
              .Append('\n');
        }

        for (int i = 0; i < session.items.Count; i++)
        {
            ItemData item = session.items[i];
            sb.Append("ITEM ").Append(item.itemId)
              .Append(" pos=").Append(item.position)
              .Append(" area=").Append(item.area)
              .Append(" place=").Append(item.placementId)
              .Append(" temp=").Append(item.hasTemperature)
              .Append(" pool=").Append(item.isInPool)
              .Append(" returned=").Append(item.isReturned)
              .Append('\n');
        }

        return sb.ToString();
    }

    private static IEnumerator WaitUntil(System.Func<bool> condition, float timeout, string label)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - start < timeout) yield return null;
        Assert.IsTrue(condition(), label + " 超时");
    }
}
