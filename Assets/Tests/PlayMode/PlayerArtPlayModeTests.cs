using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 主角立绘真的进场景了吗：世界尺寸、脚底线、走路翻转的接线。
/// </summary>
public class PlayerArtPlayModeTests
{
    [TearDown]
    public void TearDown()
    {
        if (GameRoot.Instance != null) Object.Destroy(GameRoot.Instance.gameObject);
    }

    [UnityTest]
    public IEnumerator PlayerArt_IsSizedAndSitsOnItsFootLine()
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

        PlayerController player = Object.FindObjectOfType<PlayerController>();
        Assert.IsNotNull(player, "一楼应该有主角");
        Assert.IsNotNull(player.bodyRenderer, "主角身上要接着立绘渲染器");
        Assert.IsNotNull(player.bodyRenderer.sprite, "立绘 sprite 不能为空");

        Vector2 size = ResolveWorldSize(player.bodyRenderer);
        Assert.AreEqual(2.2f, size.y, 0.02f, "主角立绘应该高 2.2 世界单位");
        Assert.Greater(size.x, 1f, "宽度应该按图片比例展开");

        Assert.AreEqual(SceneLayoutMetrics.PlayerFootLineY, ResolveWorldBottom(player.bodyRenderer), 0.05f,
            "立绘底边要贴合二楼背景地板");
        Assert.AreEqual(5, player.bodyRenderer.sortingOrder, "立绘要在门前面");
    }

    [UnityTest]
    public IEnumerator PlayerArt_FlipsWhenFacingChanges()
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

        PlayerController player = Object.FindObjectOfType<PlayerController>();
        Assert.IsNotNull(player);
        Assert.IsNotNull(player.bodyRenderer);

        player.ApplyFacing(true);
        Assert.IsTrue(player.bodyRenderer.flipX, "朝左要翻转立绘");
        Assert.IsTrue(player.FacingLeft);

        player.ApplyFacing(false);
        Assert.IsFalse(player.bodyRenderer.flipX, "朝右不翻转");
        Assert.IsFalse(player.FacingLeft);
    }

    private static Vector2 ResolveWorldSize(SpriteRenderer body)
    {
        Vector3 scale = body.transform.lossyScale;
        return new Vector2(
            Mathf.Abs(body.sprite.bounds.size.x * scale.x),
            Mathf.Abs(body.sprite.bounds.size.y * scale.y));
    }

    private static float ResolveWorldBottom(SpriteRenderer body)
    {
        return body.transform.position.y + body.sprite.bounds.min.y * body.transform.lossyScale.y;
    }
}
