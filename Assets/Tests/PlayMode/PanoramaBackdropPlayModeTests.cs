using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class PanoramaBackdropPlayModeTests
{
    [TearDown]
    public void TearDown()
    {
        if (GameRoot.Instance != null) UnityEngine.Object.Destroy(GameRoot.Instance.gameObject);
        NarrationState.Reset();
    }

    [UnityTest]
    public IEnumerator EveryGameScene_ShowsItsPanoramaRegion()
    {
        yield return EnsureRoot();
        string[] scenes = { "FirstFloor", "SecondFloor", "Backyard" };
        for (int i = 0; i < scenes.Length; i++)
        {
            yield return LoadScene(scenes[i]);
            PanoramaBackdropView[] views = UnityEngine.Object.FindObjectsOfType<PanoramaBackdropView>(true);
            Assert.AreEqual(1, views.Length, scenes[i] + " 应该只有一份全景背景");
            Assert.IsNotNull(views[0].spriteRenderer.sprite);
            Assert.IsTrue(views[0].spriteRenderer.enabled);
            Assert.AreEqual(-100, views[0].spriteRenderer.sortingOrder);

            GameObject oldBackdrop = GameObject.Find("Backdrop");
            SpriteRenderer oldRenderer = oldBackdrop != null ? oldBackdrop.GetComponent<SpriteRenderer>() : null;
            if (oldRenderer != null) Assert.IsFalse(oldRenderer.enabled, scenes[i] + " 旧背景仍可见");
        }
    }

    [UnityTest]
    public IEnumerator StitchedPhase_ShowsOneFullPanorama_ThenRestoresRegion()
    {
        yield return EnsureRoot();
        yield return LoadScene("FirstFloor");
        yield return WaitUntil(delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; }, 10f);

        IntroNarration intro = UnityEngine.Object.FindObjectOfType<IntroNarration>();
        if (intro != null) intro.RequestSkip();
        yield return WaitUntil(delegate { return !NarrationState.IsBlockingInput; }, 5f);

        GameRoot.Instance.Flow.RegisterPlayerTouch("panorama_stitch_test");
        GameRoot.Instance.Narration.FinishTouchNarration();
        yield return WaitUntil(delegate { return SceneStitcher.IsActive; }, 15f);

        SceneCameraFollow2D framing = Camera.main.GetComponent<SceneCameraFollow2D>();
        yield return WaitUntil(delegate { return framing != null && !framing.IsAnimating; }, 5f);

        PanoramaBackdropView[] views = UnityEngine.Object.FindObjectsOfType<PanoramaBackdropView>(true);
        Assert.AreEqual(3, views.Length, "拼接后应该有三份场景背景组件");
        int visible = CountVisible(views);
        Assert.AreEqual(1, visible, "宽镜头只能显示一份完整全景");

        PanoramaBackdropView shown = FindVisible(views);
        Assert.IsNotNull(shown);
        Assert.AreEqual(SceneLayoutMetrics.StitchedBackdropPosition.x, shown.transform.position.x, 0.01f);
        Assert.AreEqual(SceneLayoutMetrics.StitchedBackdropPosition.y, shown.transform.position.y, 0.01f);

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Session.round == 2 && !SceneStitcher.IsActive; },
            45f);

        views = UnityEngine.Object.FindObjectsOfType<PanoramaBackdropView>(true);
        Assert.AreEqual(1, CountVisible(views), "拆场景后应该恢复单份区域背景");
        shown = FindVisible(views);
        Assert.IsNotNull(shown);
        Vector3 expected = SceneLayoutMetrics.NormalBackdropPosition(AreaId.FirstFloor);
        Assert.AreEqual(expected.x, shown.transform.position.x, 0.01f);
        Assert.AreEqual(expected.y, shown.transform.position.y, 0.01f);
    }

    private static IEnumerator EnsureRoot()
    {
        yield return null;
        if (GameRoot.Instance == null)
        {
            GameObject root = new GameObject("[GameRoot-Test]");
            root.AddComponent<GameRoot>();
            yield return null;
        }
    }

    private static IEnumerator LoadScene(string sceneName)
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (load != null && !load.isDone) yield return null;
        yield return null;
        yield return null;
    }

    private static int CountVisible(PanoramaBackdropView[] views)
    {
        int visible = 0;
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null && views[i].spriteRenderer != null && views[i].spriteRenderer.enabled) visible++;
        }
        return visible;
    }

    private static PanoramaBackdropView FindVisible(PanoramaBackdropView[] views)
    {
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null && views[i].spriteRenderer != null && views[i].spriteRenderer.enabled)
                return views[i];
        }
        return null;
    }

    private static IEnumerator WaitUntil(Func<bool> condition, float timeout)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - start < timeout) yield return null;
        Assert.IsTrue(condition(), "等待超时");
    }
}
