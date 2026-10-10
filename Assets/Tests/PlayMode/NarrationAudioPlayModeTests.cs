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
