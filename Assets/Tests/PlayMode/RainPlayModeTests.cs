using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 验证后院雨场覆盖整个屏幕（含最底部与左右边缘）。
/// </summary>
public class RainPlayModeTests
{
    [TearDown]
    public void TearDown()
    {
        if (GameRoot.Instance != null) UnityEngine.Object.Destroy(GameRoot.Instance.gameObject);
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

        RainController rain = Object.FindObjectOfType<RainController>();
        Assert.IsNotNull(rain, "后院场景应该有 RainController。");

        Camera camera = Camera.main;
        Assert.IsNotNull(camera, "后院场景应该有主相机。");

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
        Assert.LessOrEqual(rainMinY, viewMinY + 0.001f, "雨场要覆盖屏幕下边（这是之前的漏雨处）。");
        Assert.GreaterOrEqual(rainMaxY, viewMaxY - 0.001f, "雨场要覆盖屏幕上边。");
    }
}
