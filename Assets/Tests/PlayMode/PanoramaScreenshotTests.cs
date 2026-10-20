using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class PanoramaScreenshotTests
{
    [TearDown]
    public void TearDown()
    {
        if (GameRoot.Instance != null) UnityEngine.Object.Destroy(GameRoot.Instance.gameObject);
        NarrationState.Reset();
    }

    [UnityTest]
    public IEnumerator GameScenes_RenderRuntimeScreenshots()
    {
        string[] scenes = { "FirstFloor", "SecondFloor", "Backyard" };
        for (int i = 0; i < scenes.Length; i++)
        {
            yield return FreshRoot();
            yield return LoadScene(scenes[i]);
            yield return WaitForPlayerInput();
            yield return SkipIntro();
            yield return null;

            Capture("Logs/panorama-runtime-" + scenes[i] + "-1920x1080.png", 1920, 1080);
            Capture("Logs/panorama-runtime-" + scenes[i] + "-1280x800.png", 1280, 800);
        }
    }

    [UnityTest]
    public IEnumerator StitchedScene_RendersRuntimeScreenshot()
    {
        yield return FreshRoot();
        yield return LoadScene("FirstFloor");
        yield return WaitForPlayerInput();
        yield return SkipIntro();

        GameRoot.Instance.Flow.RegisterPlayerTouch("panorama_screenshot_test");
        GameRoot.Instance.Narration.FinishTouchNarration();
        yield return WaitUntil(delegate { return SceneStitcher.IsActive; }, 15f);

        SceneCameraFollow2D framing = Camera.main.GetComponent<SceneCameraFollow2D>();
        yield return WaitUntil(delegate { return framing != null && !framing.IsAnimating; }, 5f);
        Capture("Logs/panorama-runtime-Stitched-1920x1080.png", 1920, 1080);
        Capture("Logs/panorama-runtime-Stitched-1280x800.png", 1280, 800);
    }

    [UnityTest]
    public IEnumerator StitchedScene_FromSecondFloor_RendersRuntimeScreenshot()
    {
        yield return FreshRoot();
        yield return LoadScene("SecondFloor");
        yield return WaitForPlayerInput();
        yield return SkipIntro();

        GameRoot.Instance.Flow.RegisterPlayerTouch("panorama_second_floor_screenshot_test");
        GameRoot.Instance.Narration.FinishTouchNarration();
        yield return WaitUntil(delegate { return SceneStitcher.IsActive; }, 15f);

        SceneCameraFollow2D framing = Camera.main.GetComponent<SceneCameraFollow2D>();
        yield return WaitUntil(delegate { return framing != null && !framing.IsAnimating; }, 5f);
        Capture("Logs/panorama-runtime-StitchedFromSecondFloor-1920x1080.png", 1920, 1080);
        Capture("Logs/panorama-runtime-StitchedFromSecondFloor-1280x800.png", 1280, 800);
    }

    private static IEnumerator FreshRoot()
    {
        yield return null;
        if (GameRoot.Instance != null) UnityEngine.Object.Destroy(GameRoot.Instance.gameObject);
        yield return null;
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

    private static IEnumerator WaitForPlayerInput()
    {
        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; },
            10f);
    }

    private static IEnumerator SkipIntro()
    {
        IntroNarration intro = UnityEngine.Object.FindObjectOfType<IntroNarration>();
        if (intro != null) intro.RequestSkip();
        yield return WaitUntil(delegate { return !NarrationState.IsBlockingInput; }, 5f);
    }

    private static void Capture(string path, int width, int height)
    {
        Camera camera = Camera.main;
        Assert.IsNotNull(camera);

        SceneCameraFollow2D framing = camera.GetComponent<SceneCameraFollow2D>();
        float previousAspect = camera.aspect;
        if (framing != null)
        {
            camera.aspect = width / (float)height;
            framing.ApplyFraming();
        }

        RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            camera.aspect = previousAspect;
            if (framing != null) framing.ApplyFraming();
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static IEnumerator WaitUntil(Func<bool> condition, float timeout)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - start < timeout) yield return null;
        Assert.IsTrue(condition(), "等待超时");
    }
}
