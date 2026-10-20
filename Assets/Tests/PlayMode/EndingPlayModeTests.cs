using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 结算演出的运行时冒烟测试：GameOverEvent 数据、已有 UI 临时隐藏 / 恢复、
/// 自动播放并在结束后回主菜单。
/// </summary>
public class EndingPlayModeTests
{
    [TearDown]
    public void TearDown()
    {
        if (GameRoot.Instance != null) UnityEngine.Object.Destroy(GameRoot.Instance.gameObject);
    }

    [UnityTest]
    public IEnumerator GameOver_PlaysEnding_HidesExistingUi_AndReturnsToMenu()
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

        GameRoot root = GameRoot.Instance;
        Arrange(root, 40f, 40f, 80f, 40f);

        EndingDirector director = UnityEngine.Object.FindObjectOfType<EndingDirector>();
        PromptOverlay prompt = UnityEngine.Object.FindObjectOfType<PromptOverlay>();
        DebugOverlay debug = UnityEngine.Object.FindObjectOfType<DebugOverlay>();
        PauseButtonHUD pauseHud = UnityEngine.Object.FindObjectOfType<PauseButtonHUD>();

        Assert.IsNotNull(director);
        bool promptWasEnabled = prompt != null && prompt.enabled;
        bool debugWasEnabled = debug != null && debug.enabled;
        bool pauseWasEnabled = pauseHud != null && pauseHud.enabled;

        director.autoPlayOnGameOver = true;
        director.playbackSpeed = 80f;

        EndingResult observed = default(EndingResult);
        Action<GameOverEvent> capture = evt => observed = evt.ending;
        EventBus.Subscribe(capture);

        root.Flow.EndGame();
        EventBus.Unsubscribe(capture);

        Assert.AreEqual(EndingId.RainInTheCorridor, observed.id);
        yield return WaitUntil(delegate { return director.IsPlaying; }, 10f, "结算演出开始");
        yield return null;
        yield return null;

        if (prompt != null) Assert.IsFalse(prompt.enabled, "结算时已有提示条应临时关闭");
        if (debug != null) Assert.IsFalse(debug.enabled, "结算时已有调试面板应临时关闭");
        if (pauseHud != null) Assert.IsFalse(pauseHud.enabled, "结算时已有暂停 HUD 应临时关闭");

        yield return WaitUntil(
            delegate
            {
                return SceneManager.GetActiveScene().name == "MainMenu"
                    && !director.IsPlaying;
            },
            30f,
            "结算结束回主菜单");

        if (prompt != null) Assert.AreEqual(promptWasEnabled, prompt.enabled, "提示条应在结算后恢复");
        if (debug != null) Assert.AreEqual(debugWasEnabled, debug.enabled, "调试面板应在结算后恢复");
        if (pauseHud != null) Assert.AreEqual(pauseWasEnabled, pauseHud.enabled, "暂停 HUD 应在结算后恢复");
    }

    [UnityTest]
    public IEnumerator WhenSheWasLittle_CompletesWithoutErrors()
    {
        yield return RunEnding(EndingId.WhenSheWasLittle, "她小时候");
    }

    [UnityTest]
    public IEnumerator BeforeSnowStops_CompletesWithoutErrors()
    {
        yield return RunEnding(EndingId.BeforeSnowStops, "雪停之前");
    }

    [UnityTest]
    public IEnumerator RequestSkip_JumpsToFinalTitleAndReturns()
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

        GameRoot root = GameRoot.Instance;
        root.Session.isRunning = false;
        root.Session.isGameOver = true;
        root.Flow.SetState(GameState.Ending);

        EndingDirector director = UnityEngine.Object.FindObjectOfType<EndingDirector>();
        Assert.IsNotNull(director);
        director.autoPlayOnGameOver = false;
        director.playbackSpeed = 20f;
        director.finalTitleSeconds = 0.2f;
        director.Play(new EndingResult
        {
            id = EndingId.WhenSheWasLittle,
            title = "她小时候",
            reason = "Skip 测试"
        });

        yield return WaitUntil(delegate { return director.IsPlaying; }, 10f, "跳过测试开始");
        director.RequestSkipToFinalTitle();
        yield return WaitUntil(delegate { return director.IsFinalTitle; }, 10f, "跳到结束标题");
        yield return WaitUntil(
            delegate { return SceneManager.GetActiveScene().name == "MainMenu" && !director.IsPlaying; },
            30f,
            "跳过测试返回主菜单");
    }

    private static IEnumerator RunEnding(EndingId id, string title)
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

        GameRoot root = GameRoot.Instance;
        root.Session.isRunning = false;
        root.Session.isGameOver = true;
        root.Flow.SetState(GameState.Ending);

        EndingDirector director = UnityEngine.Object.FindObjectOfType<EndingDirector>();
        Assert.IsNotNull(director);
        director.autoPlayOnGameOver = false;
        director.playbackSpeed = 120f;
        director.finalTitleSeconds = 0.2f;
        director.Play(new EndingResult
        {
            id = id,
            title = title,
            reason = "PlayMode 测试"
        });

        yield return WaitUntil(delegate { return director.IsPlaying; }, 10f, title + " 开始");
        Assert.AreEqual(id, director.CurrentEnding);

        yield return WaitUntil(
            delegate
            {
                return SceneManager.GetActiveScene().name == "MainMenu"
                    && !director.IsPlaying;
            },
            30f,
            title + " 播放完毕");
    }

    private static void Arrange(GameRoot root, float motherWarmth, float motherAwareness, float othersWarmth, float othersAwareness)
    {
        root.Session.isRunning = true;
        root.Session.isGameOver = false;
        Set(root.Session, ResidentId.Mother, motherWarmth, motherAwareness);
        Set(root.Session, ResidentId.Grandma, othersWarmth, othersAwareness);
        Set(root.Session, ResidentId.Uncle, othersWarmth, othersAwareness);
        Set(root.Session, ResidentId.Girl, 50f, 0f);
    }

    private static void Set(GameSession session, ResidentId id, float warmth, float awareness)
    {
        ResidentData resident = session.GetResident(id);
        resident.warmth = warmth;
        resident.awareness = awareness;
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
}
