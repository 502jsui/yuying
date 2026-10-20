using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>二楼原型：楼梯往返、母亲物品、门光、以及二楼视角下的楼下 NPC 行动演出。</summary>
public class SecondFloorPlayModeTests
{
    [TearDown]
    public void TearDown()
    {
        if (GameRoot.Instance != null) Object.Destroy(GameRoot.Instance.gameObject);
        PauseSnapshot.Clear();
        NarrationState.Reset();
    }

    [UnityTest]
    public IEnumerator SecondFloor_Stairs_GoUpAndDown()
    {
        yield return GoUpstairs();

        Assert.AreEqual("SecondFloor", SceneManager.GetActiveScene().name);
        PlayerController upstairs = Object.FindObjectOfType<PlayerController>();
        Assert.IsNotNull(upstairs, "二楼应该有玩家");
        Assert.IsNotNull(upstairs.bodyRenderer, "二楼主角应该有立绘");
        Assert.AreEqual(SceneLayoutMetrics.PlayerFootLineY, upstairs.bodyRenderer.bounds.min.y, 0.05f,
            "二楼主角脚底应该贴住背景地板，不能悬空");

        ScenePortal2D stairs = FindPortalWithTarget("FirstFloor");
        Assert.IsNotNull(stairs, "二楼应该有下楼梯");
        Assert.AreEqual(SceneLayoutMetrics.StairsX, stairs.transform.position.x, 0.01f,
            "二楼传送点应该对齐全景里的楼梯");
        Assert.Less(upstairs.transform.position.x, stairs.transform.position.x,
            "上二楼后应该出生在楼梯左侧");
        upstairs.transform.position = stairs.transform.position + Vector3.left * 1f;
        yield return null;
        Assert.IsTrue(stairs.TryUsePortal(), "按 G 应该能下楼");

        yield return WaitUntil(
            delegate { return SceneManager.GetActiveScene().name == "FirstFloor"; },
            8f,
            "回到一楼");
        Assert.AreEqual("FirstFloor", SceneManager.GetActiveScene().name);
    }

    [UnityTest]
    public IEnumerator SecondFloor_MotherItem_Touch_ConfusesResidents()
    {
        yield return GoUpstairs();

        ItemData motherItem = GameRoot.Instance.Session.GetItem("mother_01");
        Assert.IsNotNull(motherItem, "母亲衣服应该在本局数据里");
        Assert.IsTrue(motherItem.IsInScene, "母亲衣服应该已经摆在二楼");

        ItemView view = FindItemView("mother_01");
        Assert.IsNotNull(view, "二楼应该有母亲衣服的 View");
        Assert.IsTrue(view.IsTouchable, "母亲衣服应该可触碰");

        Assert.IsTrue(GameRoot.Instance.Items.Touch("mother_01").IsSuccess);
        GameRoot.Instance.Narration.FinishTouchNarration();
        yield return null;

        Assert.IsTrue(motherItem.hasTemperature, "触碰后母亲衣服应该有温度");
        bool someoneConfused =
            GameRoot.Instance.Session.GetResident(ResidentId.Grandma).state == NpcState.Confused
            || GameRoot.Instance.Session.GetResident(ResidentId.Uncle).state == NpcState.Confused
            || GameRoot.Instance.Session.GetResident(ResidentId.Girl).state == NpcState.Confused;
        Assert.IsTrue(someoneConfused, "碰母亲物品应该让 1~2 位住户进入疑惑");
    }

    [UnityTest]
    public IEnumerator SecondFloor_MotherDoorLight_ReflectsStats()
    {
        yield return GoUpstairs();

        DoorLightView doorLight = Object.FindObjectOfType<DoorLightView>();
        Assert.IsNotNull(doorLight, "二楼母亲门应该有门缝光");

        ResidentData mother = GameRoot.Instance.Session.GetResident(ResidentId.Mother);
        Assert.IsNotNull(mother, "本局数据里应该有母亲");
        mother.warmth = 55f;
        mother.awareness = 60f;
        yield return null;

        Assert.AreEqual(DoorLightView.OpenState.HalfOpen, doorLight.CurrentOpenState,
            "暖意达到 55 时门应该半开");
        Assert.AreEqual(doorLight.orangeLight, doorLight.lightRenderer.color,
            "知晓高于 54 时门光应该是暖橙色");

        mother.warmth = 20f;
        mother.awareness = 20f;
        yield return null;
        Assert.AreEqual(DoorLightView.OpenState.Closed, doorLight.CurrentOpenState,
            "暖意低于 30 时门应该关着");
        Assert.AreEqual(doorLight.whiteLight, doorLight.lightRenderer.color,
            "知晓低于 30 时门光应该是白色");
    }

    [UnityTest]
    public IEnumerator SecondFloor_NpcPhase_PlaysLowerFloorActions_AndAdvances()
    {
        yield return GoUpstairs();

        int actionCount = 0;
        System.Action<NpcActionEvent> onAction = delegate { actionCount++; };
        EventBus.Subscribe(onAction);
        try
        {
            GameRoot.Instance.Flow.RegisterPlayerTouch("second_floor_action_test");
            GameRoot.Instance.Narration.FinishTouchNarration();

            yield return WaitUntil(
                delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.NpcAction; },
                5f,
                "进入 NPC 行动阶段");
            yield return WaitUntil(
                delegate { return SceneStitcher.IsActive; },
                10f,
                "二楼 NPC 阶段应该拼接整栋楼");
            Assert.IsTrue(SceneStitcher.IsActive, "玩家在二楼时应该拉远看整栋楼");
            Assert.IsTrue(SceneManager.GetSceneByName("FirstFloor").isLoaded, "宽镜头应该包含一楼");
            Assert.IsTrue(SceneManager.GetSceneByName("Backyard").isLoaded, "宽镜头应该包含后院");

            SceneCameraFollow2D framing = Camera.main != null ? Camera.main.GetComponent<SceneCameraFollow2D>() : null;
            yield return WaitUntil(
                delegate { return framing != null && !framing.IsAnimating; },
                5f,
                "整栋楼镜头拉远完成");
            Assert.Greater(Camera.main.orthographicSize, 8f, "二楼 NPC 阶段镜头应该拉远");

            RainController rain = Object.FindObjectOfType<RainController>();
            Assert.IsNotNull(rain, "二楼 NPC 阶段应该有雨");
            Assert.Greater(rain.height, 20f, "雨场应该覆盖整栋楼");
            Assert.AreEqual(1.4f, rain.BackgroundRain.exclusionTopY, 0.01f,
                "以二楼为基准时，雨应该停在二楼屋顶");
            Assert.IsNotNull(GameObject.Find("Roof"), "二楼应该有屋顶罩");
            AssertSinglePanoramaBackdrop("二楼宽镜头应该只显示一份完整全景");
            PanoramaBackdropView backdrop = FindSinglePanoramaBackdrop();
            Assert.IsNotNull(backdrop);
            Vector3 expectedBackdrop = SceneLayoutMetrics.StitchedBackdropPosition + new Vector3(
                SceneStitcher.OffsetFor(AreaId.FirstFloor),
                SceneStitcher.OffsetYFor(AreaId.FirstFloor),
                0f);
            Assert.AreEqual(expectedBackdrop.x, backdrop.transform.position.x, 0.01f,
                "二楼宽镜头的全景应跟随一楼横向偏移");
            Assert.AreEqual(expectedBackdrop.y, backdrop.transform.position.y, 0.01f,
                "二楼宽镜头的全景应跟随一楼楼层偏移");

            ResidentView visibleActor = null;
            yield return WaitUntil(
                delegate
                {
                    visibleActor = FindVisibleLowerFloorActor();
                    return actionCount > 0 && visibleActor != null;
                },
                15f,
                "二楼 NPC 阶段应该播放楼下住户行动");

            Assert.Greater(actionCount, 0, "楼下住户应该实际执行行动");
            Assert.IsNotNull(visibleActor, "楼下住户应该在宽镜头里可见");
            Assert.AreEqual(
                visibleActor.groundY + SceneStitcher.OffsetYFor(visibleActor.area),
                visibleActor.transform.position.y,
                0.02f,
                "楼下住户走位必须跟着所属场景的楼层偏移");
            AssertLowerFloorActorStandsOnBackdrop(visibleActor);

            yield return WaitUntil(
                delegate { return GameRoot.Instance.Session.round == 2; },
                45f,
                "播放楼下 NPC 行动后应该推进到第 2 轮");

            Assert.AreEqual("SecondFloor", SceneManager.GetActiveScene().name, "玩家应该留在二楼");
            yield return WaitUntil(
                delegate { return !SceneStitcher.IsActive; },
                5f,
                "NPC 阶段结束后拆场景");
        }
        finally
        {
            EventBus.Unsubscribe(onAction);
        }
    }

    [UnityTest]
    public IEnumerator SecondFloor_GirlWander_ProducesVisibleMovement()
    {
        yield return GoUpstairs();
        GameRoot root = GameRoot.Instance;
        float originalMoveWeight = root.Config.girlMoveWeight;
        float originalAbsentChance = root.Config.absentRoundChance;
        root.Config.girlMoveWeight = 0f;
        root.Config.absentRoundChance = 0f;

        try
        {
            ResidentView girl = FindResidentView(ResidentId.Girl, AreaId.SecondFloor);
            Assert.IsNotNull(girl, "二楼应该有女孩 View");
            float startX = girl.transform.position.x;

            root.Flow.RegisterPlayerTouch("girl_wander_visibility_test");
            root.Narration.FinishTouchNarration();
            yield return WaitUntil(
                delegate { return root.Flow.Phase == TurnPhase.NpcAction; },
                10f,
                "进入 NPC 行动阶段");

            bool moved = false;
            float start = Time.realtimeSinceStartup;
            while (root.Flow.Phase == TurnPhase.NpcAction && Time.realtimeSinceStartup - start < 45f)
            {
                ResidentView current = FindResidentView(ResidentId.Girl, AreaId.SecondFloor);
                if (current != null && current.bodyRenderer != null && current.bodyRenderer.enabled
                    && Mathf.Abs(current.transform.position.x - startX) > 0.75f)
                {
                    moved = true;
                    break;
                }
                yield return null;
            }

            Assert.IsTrue(moved, "小女孩抽到闲逛时必须产生可见位移");
        }
        finally
        {
            root.Config.girlMoveWeight = originalMoveWeight;
            root.Config.absentRoundChance = originalAbsentChance;
        }
    }

    private static IEnumerator GoUpstairs()
    {
        yield return null;
        yield return EnsureGameRoot();
        yield return LoadScene("FirstFloor");
        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; },
            10f,
            "一楼进入玩家输入");
        yield return SkipIntro();

        ScenePortal2D exitHub = FindPortalWithTarget("SecondFloor");
        Assert.IsNotNull(exitHub, "一楼右侧应该有上二楼的出口");

        PlayerController player = Object.FindObjectOfType<PlayerController>();
        Assert.IsNotNull(player, "一楼应该有玩家");
        player.transform.position = exitHub.transform.position + Vector3.left * 1f;
        yield return null;
        Assert.IsTrue(exitHub.TryUseAlternatePortal(), "按 G 应该能上二楼");

        yield return WaitUntil(
            delegate { return SceneManager.GetActiveScene().name == "SecondFloor"; },
            8f,
            "上到二楼");
        yield return WaitUntil(
            delegate { return !SceneTransitionState.IsLoading; },
            5f,
            "二楼淡入完成");
    }

    private static IEnumerator EnsureGameRoot()
    {
        if (GameRoot.Instance != null) yield break;
        GameObject root = new GameObject("[GameRoot-Test]");
        root.AddComponent<GameRoot>();
        yield return null;
    }

    private static IEnumerator LoadScene(string sceneName)
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (load != null && !load.isDone) yield return null;
        yield return null;
        yield return null;
    }

    private static IEnumerator SkipIntro()
    {
        IntroNarration intro = Object.FindObjectOfType<IntroNarration>();
        if (intro != null) intro.RequestSkip();
        yield return WaitUntil(delegate { return !NarrationState.IsBlockingInput; }, 5f, "跳过开场");
    }

    private static ScenePortal2D FindPortalWithTarget(string targetScene)
    {
        ScenePortal2D[] portals = Object.FindObjectsOfType<ScenePortal2D>();
        for (int i = 0; i < portals.Length; i++)
        {
            if (portals[i] == null) continue;
            if (portals[i].sceneName == targetScene || portals[i].alternateSceneName == targetScene)
                return portals[i];
        }
        return null;
    }

    private static ItemView FindItemView(string itemId)
    {
        ItemView[] views = Object.FindObjectsOfType<ItemView>();
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null && views[i].itemId == itemId) return views[i];
        }
        return null;
    }

    private static ResidentView FindResidentView(ResidentId id, AreaId area)
    {
        ResidentView[] views = Object.FindObjectsOfType<ResidentView>();
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null && views[i].id == id && views[i].area == area) return views[i];
        }
        return null;
    }

    private static ResidentView FindVisibleLowerFloorActor()
    {
        ResidentView[] views = Object.FindObjectsOfType<ResidentView>();
        for (int i = 0; i < views.Length; i++)
        {
            ResidentView view = views[i];
            if (view == null || view.data == null) continue;
            if (view.area != AreaId.FirstFloor && view.area != AreaId.Backyard) continue;
            if (!view.data.IsOutside) continue;
            if (view.bodyRenderer == null || !view.bodyRenderer.enabled) continue;
            if (!view.gameObject.scene.isLoaded) continue;
            return view;
        }
        return null;
    }

    private static void AssertSceneObjectVisible(string objectName, string message)
    {
        GameObject target = GameObject.Find(objectName);
        Assert.IsNotNull(target, message + "：找不到 " + objectName);

        Camera camera = Camera.main;
        Assert.IsNotNull(camera, message + "：找不到主相机");
        Vector3 min = camera.ViewportToWorldPoint(new Vector3(0f, 0f, camera.nearClipPlane));
        Vector3 max = camera.ViewportToWorldPoint(new Vector3(1f, 1f, camera.nearClipPlane));
        Vector3 position = target.transform.position;
        Assert.IsTrue(
            position.x >= Mathf.Min(min.x, max.x) && position.x <= Mathf.Max(min.x, max.x) &&
            position.y >= Mathf.Min(min.y, max.y) && position.y <= Mathf.Max(min.y, max.y),
            message + "：" + objectName + " 不在镜头范围内");
    }

    private static void AssertSinglePanoramaBackdrop(string message)
    {
        PanoramaBackdropView[] views = Object.FindObjectsOfType<PanoramaBackdropView>(true);
        int visible = 0;
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null && views[i].spriteRenderer != null && views[i].spriteRenderer.enabled) visible++;
        }
        Assert.AreEqual(1, visible, message);
    }

    private static PanoramaBackdropView FindSinglePanoramaBackdrop()
    {
        PanoramaBackdropView[] views = Object.FindObjectsOfType<PanoramaBackdropView>(true);
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null && views[i].spriteRenderer != null && views[i].spriteRenderer.enabled)
                return views[i];
        }
        return null;
    }

    private static void AssertLowerFloorActorStandsOnBackdrop(ResidentView actor)
    {
        Assert.IsNotNull(actor.bodyRenderer, "楼下住户应该有立绘");
        float backdropGroundY = -3.4f + SceneStitcher.OffsetYFor(AreaId.FirstFloor);
        float actorFeetY = actor.bodyRenderer.bounds.min.y;
        Assert.Greater(actorFeetY, backdropGroundY - 0.2f,
            "NPC 不应该落到全景一楼地板下面");
        Assert.Less(actorFeetY, backdropGroundY + 1.2f,
            "NPC 应该站在全景一楼地板附近");
    }

    private static IEnumerator WaitUntil(System.Func<bool> condition, float timeout, string label)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - start < timeout) yield return null;
        Assert.IsTrue(condition(), label + " 超时");
    }
}
