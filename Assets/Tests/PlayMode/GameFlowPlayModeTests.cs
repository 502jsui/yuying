using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 运行时冒烟测试：验证 GameRoot 会自动启动、场景绑定会开局、
/// 一轮五个阶段能按顺序走完并进入下一轮。
/// </summary>
public class GameFlowPlayModeTests
{
    private GameObject sceneBinding;

    [TearDown]
    public void TearDown()
    {
        if (sceneBinding != null) UnityEngine.Object.Destroy(sceneBinding);
        if (GameRoot.Instance != null) UnityEngine.Object.Destroy(GameRoot.Instance.gameObject);
    }

    [UnityTest]
    public IEnumerator GameRoot_Boots_And_GameManager_StartsGame()
    {
        yield return null;

        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        Assert.IsNotNull(GameRoot.Instance, "GameRoot 应该自动创建。");
        Assert.IsNotNull(GameRoot.Instance.Config, "Config 应该加载成功。");
        Assert.AreEqual(4, GameRoot.Instance.Session.residents.Count, "开局应该有 4 位住户。");

        // 放一个场景绑定器，触发 RegisterScene → 自动开局。
        sceneBinding = new GameObject("TestSceneBinding");
        sceneBinding.AddComponent<GameManager>();

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.State == GameState.Playing; },
            3f,
            "进入 Playing 状态");

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; },
            3f,
            "进入 PlayerInput 阶段");

        Assert.AreEqual(1, GameRoot.Instance.Session.round, "第一轮 Round 应该是 1。");
        Assert.IsTrue(GameRoot.Instance.Flow.CanPlayerMove, "玩家输入阶段应该可以移动。");
        Assert.IsTrue(GameRoot.Instance.Flow.CanPlayerInteract, "玩家输入阶段应该可以触碰。");

        // 模拟玩家碰一下，应该推进到 NPC 行动阶段。
        bool accepted = GameRoot.Instance.Flow.RegisterPlayerTouch("test_item");
        Assert.IsTrue(accepted, "第一次触碰应该被接受。");
        Assert.IsFalse(GameRoot.Instance.Flow.RegisterPlayerTouch("test_item"), "同一轮第二次触碰应该被拒绝。");
        GameRoot.Instance.Narration.FinishTouchNarration();

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.NpcAction; },
            3f,
            "进入 NpcAction 阶段");

        Assert.IsFalse(GameRoot.Instance.Flow.CanPlayerMove, "NPC 行动阶段玩家不能移动。");

        // NPC 系统汇报演出结束，应该进入下一轮。
        GameRoot.Instance.Flow.NotifyNpcActionDone();

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Session.round == 2; },
            3f,
            "进入第 2 轮");

        Assert.AreEqual(2, GameRoot.Instance.Session.round, "轮次应该推进到 2。");
    }

    private static IEnumerator WaitUntil(Func<bool> condition, float timeout, string label)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - start < timeout)
        {
            yield return null;
        }
        Assert.IsTrue(condition(), label + " 超时。");
    }

    [UnityTest]
    public IEnumerator GameFlow_Completes_After_Six_Rounds()
    {
        yield return null;

        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        sceneBinding = new GameObject("TestSceneBinding");
        sceneBinding.AddComponent<GameManager>();

        for (int round = 1; round <= 6; round++)
        {
            yield return WaitUntil(
                delegate
                {
                    return GameRoot.Instance.Session.round == round
                        && GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput;
                },
                3f,
                "进入第 " + round + " 轮玩家输入");

            Assert.AreEqual(round, GameRoot.Instance.Session.round, "应该在第 " + round + " 轮。");
            Assert.IsTrue(GameRoot.Instance.Flow.RegisterPlayerTouch("test_item_" + round), "第 " + round + " 轮触碰应该被接受。");
            GameRoot.Instance.Narration.FinishTouchNarration();

            yield return WaitUntil(
                delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.NpcAction; },
                3f,
                "第 " + round + " 轮进入 NPC 行动");

            GameRoot.Instance.Flow.NotifyNpcActionDone();
        }

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.State == GameState.Ending; },
            3f,
            "进入结束状态");

        Assert.AreEqual(TurnPhase.GameOver, GameRoot.Instance.Flow.Phase, "应该进入 GameOver 阶段。");
        Assert.IsTrue(GameRoot.Instance.Session.isGameOver, "Session 应该标记为结束。");
    }

    [UnityTest]
    public IEnumerator SceneBinding_Swap_KeepsSession_And_IgnoresStaleUnregister()
    {
        yield return null;
        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        GameObject sceneA = new GameObject("SceneA");
        sceneA.AddComponent<GameManager>();

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; },
            3f,
            "场景 A 进入玩家输入阶段");

        // 走完一轮，让进度变成第 2 轮。
        GameRoot.Instance.Flow.RegisterPlayerTouch("a_01");
        GameRoot.Instance.Narration.FinishTouchNarration();
        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.NpcAction; },
            3f,
            "场景 A 进入 NPC 行动阶段");
        GameRoot.Instance.Flow.NotifyNpcActionDone();
        yield return WaitUntil(
            delegate { return GameRoot.Instance.Session.round == 2; },
            3f,
            "进入第 2 轮");

        // 新场景先注册，旧场景后卸载（真实切场景的时序）。
        GameObject sceneB = new GameObject("SceneB");
        GameManager managerB = sceneB.AddComponent<GameManager>();
        Assert.AreSame(managerB, GameRoot.Instance.Scene, "新场景应该成为当前场景。");

        UnityEngine.Object.Destroy(sceneA);
        yield return null;

        Assert.AreSame(managerB, GameRoot.Instance.Scene, "旧场景注销不应该清掉新场景。");
        Assert.AreEqual(2, GameRoot.Instance.Session.round, "切场景后回合进度不应该丢失。");

        UnityEngine.Object.Destroy(sceneB);
    }

    [UnityTest]
    public IEnumerator FirstFloorScene_Loads_And_FrameworkBinds()
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

        Assert.IsNotNull(GameManager.Instance, "FirstFloor 场景里的 GameManager 应该加载成功。");
        Assert.AreSame(GameManager.Instance, GameRoot.Instance.Scene, "GameRoot 应该记录当前场景绑定。");
        Assert.AreEqual(AreaId.FirstFloor, GameManager.Instance.Area, "区域应该自动识别为一楼。");

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.State == GameState.Playing; },
            3f,
            "真实场景自动开局");
    }

    [UnityTest]
    public IEnumerator Backyard_RainCoversWholeScreen()
    {
        yield return null;
        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        AsyncOperation load = SceneManager.LoadSceneAsync("Backyard", LoadSceneMode.Single);
        while (load != null && !load.isDone) yield return null;
        yield return null;
        yield return null;

        RainController rain = UnityEngine.Object.FindObjectOfType<RainController>();
        Assert.IsNotNull(rain, "后院场景应该有 RainController。");

        Camera camera = Camera.main;
        Assert.IsNotNull(camera, "应该有主相机。");

        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;
        float viewMinX = camera.transform.position.x - halfWidth;
        float viewMaxX = camera.transform.position.x + halfWidth;
        float viewMinY = camera.transform.position.y - halfHeight;
        float viewMaxY = camera.transform.position.y + halfHeight;

        Vector3 rainCenter = rain.transform.position;
        float rainMinX = rainCenter.x - rain.width * 0.5f;
        float rainMaxX = rainCenter.x + rain.width * 0.5f;
        float rainMinY = rainCenter.y - rain.height * 0.5f;
        float rainMaxY = rainCenter.y + rain.height * 0.5f;

        Assert.LessOrEqual(rainMinX, viewMinX + 0.001f, "雨场要覆盖屏幕左边。");
        Assert.GreaterOrEqual(rainMaxX, viewMaxX - 0.001f, "雨场要覆盖屏幕右边。");
        Assert.LessOrEqual(rainMinY, viewMinY + 0.001f, "雨场要覆盖屏幕下边。");
        Assert.GreaterOrEqual(rainMaxY, viewMaxY - 0.001f, "雨场要覆盖屏幕上边。");
    }

    [UnityTest]
    public IEnumerator GameRoot_Boots_WithItemSystem()
    {
        yield return null;

        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        sceneBinding = new GameObject("TestSceneBinding");
        sceneBinding.AddComponent<GameManager>();

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.State == GameState.Playing; },
            3f,
            "进入 Playing 状态");

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; },
            3f,
            "进入 PlayerInput 阶段");

        Assert.IsNotNull(GameRoot.Instance.Items, "GameRoot 应该持有物品系统。");
        Assert.AreEqual(9, GameRoot.Instance.Session.items.Count, "开局应该按物品表建好 9 件物品。");

        ItemData clothes = GameRoot.Instance.Items.GetItem("grandma_01");
        Assert.IsNotNull(clothes);
        Assert.IsTrue(clothes.IsInScene, "grandma_01 开局应该在场景里。");
        Assert.IsFalse(clothes.hasTemperature);

        ItemActionResult result = GameRoot.Instance.Items.Touch("grandma_01");
        Assert.IsTrue(result.IsSuccess, "玩家输入阶段应该能碰到场景里的物品。");
        Assert.IsTrue(clothes.hasTemperature, "碰过之后物品应该获得温度。");
        Assert.AreEqual(1, GameRoot.Instance.Session.touchesThisRound, "本轮触碰次数应该记为 1。");
    }

    [UnityTest]
    public IEnumerator NpcPhaseDriver_RunsActions_AndFinishesPhase()
    {
        yield return null;

        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        sceneBinding = new GameObject("TestSceneBinding");
        sceneBinding.AddComponent<GameManager>();

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; },
            3f,
            "进入 PlayerInput 阶段");

        int actions = 0;
        System.Action<NpcActionEvent> handler = delegate { actions++; };
        EventBus.Subscribe(handler);

        GameRoot.Instance.Flow.RegisterPlayerTouch("npc_driver_test");
        GameRoot.Instance.Narration.FinishTouchNarration();

        // 不手动调 NotifyNpcActionDone：必须由 NpcPhaseDriver 自己跑完并推进到第 2 轮。
        yield return WaitUntil(
            delegate { return GameRoot.Instance.Session.round == 2; },
            45f,
            "NPC 驱动自动跑完行动阶段并进入第 2 轮");

        EventBus.Unsubscribe(handler);
        Assert.Greater(actions, 0, "NPC 行动阶段应该至少广播一个 NpcActionEvent。");
    }

    [UnityTest]
    public IEnumerator FirstFloor_HasPortalToBackyard_ForNpcTravel()
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

        NpcPhaseDriver driver = GameRoot.Instance != null ? GameRoot.Instance.GetComponent<NpcPhaseDriver>() : null;
        Assert.IsNotNull(driver, "GameRoot 上应该有 NpcPhaseDriver。");

        Vector3 portalPosition;
        Assert.IsTrue(driver.TryGetPortalPosition(AreaId.Backyard, out portalPosition),
            "一楼场景应该能找到通往后院的传送点，NPC 才能走过去再自动传送。");

        // 传送点在地面高度附近，NPC 只会沿 X 走过去。
        Assert.Greater(portalPosition.x, 5f, "通往后院的传送点应该在一楼右侧。");
    }

    [UnityTest]
    public IEnumerator AbsentJudgement_RunsAfterActions_AndBlocksAreaNextRound()
    {
        yield return null;

        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        sceneBinding = new GameObject("TestSceneBinding");
        sceneBinding.AddComponent<GameManager>();

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; },
            5f,
            "进入第 1 轮玩家输入");

        Assert.IsFalse(GameRoot.Instance.Session.hasAbsentResident, "第 1 轮开始时住户应该都在门里（全员在家）。");

        // 让本轮行动结束后一定判定出「有人不回家」。
        float originalChance = GameRoot.Instance.Config.absentRoundChance;
        GameRoot.Instance.Config.absentRoundChance = 1f;

        GameRoot.Instance.Flow.RegisterPlayerTouch("absent_judge_test");
        GameRoot.Instance.Narration.FinishTouchNarration();

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Session.round == 2; },
            30f,
            "住户行动结束并判定完回家 / 不回家");

        GameRoot.Instance.Config.absentRoundChance = originalChance; // 还原，别影响其它测试

        Assert.IsTrue(GameRoot.Instance.Session.hasAbsentResident, "行动结束后应该判定出有人不回家。");
        Assert.AreNotEqual(ResidentId.Mother, GameRoot.Instance.Session.absentResident, "母亲不行动，不会被留下。");

        ResidentData absent = GameRoot.Instance.Session.GetResident(GameRoot.Instance.Session.absentResident);
        Assert.IsNotNull(absent);
        Assert.AreEqual(NpcState.StayOut, absent.state, "不回家的人应该进入「区域持续」。");
        Assert.AreEqual(absent.stayOutArea, absent.currentArea, "他应该停在自己当时所在的区域。");
        Assert.IsTrue(GameRoot.Instance.Items.IsAreaBlocked(absent.stayOutArea), "该区域在下一轮应该不可用。");
    }
}
