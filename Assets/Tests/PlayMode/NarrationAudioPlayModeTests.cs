using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 文案 / 音效接线的运行时测试：开场阻断与跳过、物品独白、音效映射、雨声淡入淡出。
/// </summary>
public class NarrationAudioPlayModeTests
{
    private GameObject sceneBinding;

    [TearDown]
    public void TearDown()
    {
        if (sceneBinding != null) UnityEngine.Object.Destroy(sceneBinding);
        if (GameRoot.Instance != null) UnityEngine.Object.Destroy(GameRoot.Instance.gameObject);
        GameObject testCamera = GameObject.Find("IntroTestCamera");
        if (testCamera != null) UnityEngine.Object.Destroy(testCamera);
        GameObject testPlayer = GameObject.Find("IntroTestPlayer");
        if (testPlayer != null) UnityEngine.Object.Destroy(testPlayer);
    }

    private static void EnsureRoot()
    {
        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
        }
    }

    private IEnumerator BootToPlayerInput()
    {
        yield return null;
        EnsureRoot();
        yield return null;

        sceneBinding = new GameObject("TestSceneBinding");
        sceneBinding.AddComponent<GameManager>();

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; },
            8f,
            "进入玩家输入阶段");
    }

    private IEnumerator SkipIntro()
    {
        IntroNarration intro = UnityEngine.Object.FindObjectOfType<IntroNarration>();
        Assert.IsNotNull(intro, "GameRoot 上应该有 IntroNarration");

        intro.RequestSkip();
        yield return WaitUntil(delegate { return !NarrationState.IsBlockingInput; }, 5f, "开场结束");
    }

    [UnityTest]
    public IEnumerator Intro_BlocksInput_AndCanBeSkipped()
    {
        yield return BootToPlayerInput();

        Assert.IsTrue(NarrationState.IsBlockingInput, "开场独白期间应该挡住输入");
        Assert.IsTrue(NarrationState.IsIntroPlaying, "开场序列应该在跑");
        Assert.IsFalse(GameRoot.Instance.Session.openingDone, "还没播完，不该标记结束");

        yield return SkipIntro();

        Assert.IsTrue(GameRoot.Instance.Session.openingDone, "跳过后应该标记开场结束");
        Assert.IsFalse(NarrationState.IsBlockingInput, "跳过后应该放开输入");
    }

    [UnityTest]
    public IEnumerator Intro_Advance_EndsOnlyCurrentLine()
    {
        yield return BootToPlayerInput();

        IntroNarration intro = UnityEngine.Object.FindObjectOfType<IntroNarration>();
        PromptOverlay overlay = UnityEngine.Object.FindObjectOfType<PromptOverlay>();
        Assert.IsNotNull(intro, "GameRoot 上应该有 IntroNarration");
        Assert.IsNotNull(overlay, "GameRoot 上应该有 PromptOverlay");

        string first = overlay.CurrentMessage;
        Assert.IsFalse(string.IsNullOrEmpty(first), "开场第一句应该已经显示");

        intro.RequestAdvance();
        yield return null;
        yield return null;

        Assert.IsTrue(NarrationState.IsBlockingInput, "只推进一句，不该结束整段开场");
        Assert.IsFalse(GameRoot.Instance.Session.openingDone, "还没播完，不该标记结束");
        Assert.AreNotEqual(first, overlay.CurrentMessage, "一次按键只应换到下一句");

        intro.RequestSkip();
        yield return WaitUntil(delegate { return !NarrationState.IsBlockingInput; }, 5f, "跳过开场");
    }

    [UnityTest]
    public IEnumerator Intro_ZoomsInOnPlayer_ThenReturnsToScene()
    {
        // 前面的测试可能把 FirstFloor / Backyard 留在场景里，先关掉它们的相机，
        // 保证 Camera.main 一定拿到这个测试相机。
        Camera[] otherCameras = UnityEngine.Object.FindObjectsOfType<Camera>();
        for (int i = 0; i < otherCameras.Length; i++) otherCameras[i].enabled = false;

        GameObject cameraObject = new GameObject("IntroTestCamera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        SceneCameraFollow2D framing = cameraObject.AddComponent<SceneCameraFollow2D>();

        // 测试场景没有真实玩家：给 IntroNarration 的镜头推近一个落点。
        GameObject playerObject = new GameObject("IntroTestPlayer");
        playerObject.AddComponent<PlayerController>();

        yield return BootToPlayerInput();

        yield return WaitUntil(delegate { return camera.orthographicSize < 3.5f; }, 3f, "开场镜头推近");
        Assert.Less(camera.orthographicSize, 3.5f, "开场独白期间镜头应该贴近主角");

        IntroNarration intro = UnityEngine.Object.FindObjectOfType<IntroNarration>();
        Assert.IsNotNull(intro, "GameRoot 上应该有 IntroNarration");
        intro.RequestSkip();

        yield return WaitUntil(delegate { return !NarrationState.IsBlockingInput; }, 5f, "跳过开场");
        yield return WaitUntil(delegate { return !framing.IsAnimating; }, 3f, "镜头回到场景");
        Assert.Greater(camera.orthographicSize, 3.5f, "开场结束后镜头应该回到单场景取景");
    }

    [UnityTest]
    public IEnumerator TouchNarration_GatesSceneStitch()
    {
        yield return BootToPlayerInput();

        IntroNarration intro = UnityEngine.Object.FindObjectOfType<IntroNarration>();
        Assert.IsNotNull(intro, "GameRoot 上应该有 IntroNarration");
        intro.RequestSkip();
        yield return WaitUntil(delegate { return !NarrationState.IsBlockingInput; }, 5f, "跳过开场");

        // 测试绑定没有真实场景名，手动给一个区域，拼接才会发生。
        GameManager manager = GameRoot.Instance.Scene;
        Assert.IsNotNull(manager, "应该有场景绑定");
        manager.area = AreaId.FirstFloor;

        Assert.IsTrue(GameRoot.Instance.Items.Touch("grandma_01").IsSuccess, "第一轮应该能碰到 grandma_01");
        yield return null;

        Assert.IsTrue(NarrationState.IsTouchNarrationPlaying, "触碰后应该进入触碰旁白");
        Assert.IsFalse(SceneStitcher.IsActive, "旁白还没播完，不该先拼接拉镜头");

        GameRoot.Instance.Narration.FinishTouchNarration();
        yield return WaitUntil(delegate { return SceneStitcher.IsActive; }, 15f, "旁白结束后应该拼接");
        Assert.IsFalse(NarrationState.IsTouchNarrationPlaying);

        // 收尾：等 NPC 行动结束、场景拆开，别把拼接状态留给后面的测试。
        yield return WaitUntil(delegate { return !SceneStitcher.IsActive; }, 40f, "NPC 行动结束后拆场景");
    }

    [UnityTest]
    public IEnumerator FirstTouch_ShowsTutorialB1()
    {
        yield return BootToPlayerInput();
        yield return SkipIntro();

        GameRoot.Instance.Items.Touch("grandma_01");
        yield return null;

        PromptOverlay overlay = UnityEngine.Object.FindObjectOfType<PromptOverlay>();
        Assert.IsNotNull(overlay);
        StringAssert.Contains("我好像能碰到", overlay.CurrentMessage);
    }

    [UnityTest]
    public IEnumerator Touch_ShowsItemMonologue_OnEveryTouch()
    {
        yield return BootToPlayerInput();
        yield return SkipIntro();

        // 假装第一次触碰的教学已经放过了
        GameRoot.Instance.Session.tutorialFirstTouchDone = true;

        GameRoot.Instance.Items.Touch("grandma_01");
        yield return null;

        PromptOverlay overlay = UnityEngine.Object.FindObjectOfType<PromptOverlay>();
        Assert.IsNotNull(overlay);
        StringAssert.Contains("针线活", overlay.CurrentMessage);
    }

    [UnityTest]
    public IEnumerator Events_MapToSfxIds()
    {
        yield return BootToPlayerInput();

        AudioManager audio = GameRoot.Instance.Audio;
        Assert.IsNotNull(audio);

        EventBus.Publish(new ItemTouchedEvent { itemId = "grandma_01" });
        Assert.AreEqual(SfxId.Touch, audio.LastPlayedSfxId);

        EventBus.Publish(new ItemAppearedEvent { itemId = "grandma_02", area = AreaId.FirstFloor, placementId = "p" });
        Assert.AreEqual(SfxId.ItemPlace, audio.LastPlayedSfxId);

        EventBus.Publish(new ItemTakenEvent { itemId = "grandma_01", by = ResidentId.Grandma });
        Assert.AreEqual(SfxId.ItemTake, audio.LastPlayedSfxId);

        EventBus.Publish(new ForceTouchWarnedEvent { itemId = "grandma_01", warningsRemaining = 1 });
        Assert.AreEqual(SfxId.Warn, audio.LastPlayedSfxId);

        EventBus.Publish(new NpcDoorEvent { id = ResidentId.Grandma, opening = true });
        Assert.AreEqual(SfxId.DoorOpen, audio.LastPlayedSfxId);

        EventBus.Publish(new NpcDoorEvent { id = ResidentId.Grandma, opening = false });
        Assert.AreEqual(SfxId.DoorClose, audio.LastPlayedSfxId);
    }

    [UnityTest]
    public IEnumerator Rain_FadesInBackyard_AndOutInFirstFloor()
    {
        yield return BootToPlayerInput();

        AudioManager audio = GameRoot.Instance.Audio;
        Assert.IsNotNull(audio);

        GameRoot.Instance.Session.playerArea = AreaId.Backyard;
        yield return null;
        yield return null;
        Assert.AreEqual(1f, audio.RainTargetVolume, 0.001f, "进后院应该淡入雨声");

        GameRoot.Instance.Session.playerArea = AreaId.FirstFloor;
        yield return null;
        yield return null;
        Assert.AreEqual(0f, audio.RainTargetVolume, 0.001f, "回一楼应该淡出雨声");
    }

    [UnityTest]
    public IEnumerator ForceTouchWarning_ShowsDeliveredCopy()
    {
        yield return BootToPlayerInput();
        yield return SkipIntro();

        GameRoot.Instance.Session.forceTouchWarningsUsed = 1;
        EventBus.Publish(new ForceTouchWarnedEvent { itemId = "grandma_01", warningsRemaining = 1 });
        yield return null;

        PromptOverlay overlay = UnityEngine.Object.FindObjectOfType<PromptOverlay>();
        Assert.IsNotNull(overlay);
        StringAssert.Contains("好重", overlay.CurrentMessage);
    }

    private static IEnumerator WaitUntil(Func<bool> condition, float timeout, string label)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - start < timeout)
        {
            yield return null;
        }
        Assert.IsTrue(condition(), label + " 超时");
    }
}
