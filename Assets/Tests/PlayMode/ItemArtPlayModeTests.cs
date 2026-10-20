using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>物品在场景中按 itemId 和放置面选择正视/侧视贴图。</summary>
public class ItemArtPlayModeTests
{
    [TearDown]
    public void TearDown()
    {
        if (GameRoot.Instance != null) UnityEngine.Object.Destroy(GameRoot.Instance.gameObject);
    }

    [UnityTest]
    public IEnumerator FirstFloor_ItemsUseArtAndGroundUsesSideView()
    {
        yield return LoadSceneAndWait("FirstFloor");

        ItemView pot = FindItem("aunt_01");
        Assert.IsNotNull(pot);

        Assert.IsNotNull(pot.spriteRenderer.sprite);
        StringAssert.Contains("aunt_01_Side", pot.spriteRenderer.sprite.name);

        BoxCollider2D potCollider = pot.GetComponent<BoxCollider2D>();
        Assert.IsNotNull(potCollider);
        Assert.Greater(potCollider.size.x, 0.1f);
        Assert.Greater(potCollider.size.y, 0.1f);
    }

    [UnityTest]
    public IEnumerator Backyard_ClothesUseRackArt()
    {
        yield return LoadSceneAndWait("Backyard");

        ItemView clothes = FindItem("grandma_01");
        Assert.IsNotNull(clothes);
        Assert.IsTrue(clothes.spriteRenderer.enabled);
        StringAssert.Contains("grandma_01_Front", clothes.spriteRenderer.sprite.name);
    }

    [UnityTest]
    public IEnumerator SecondFloor_MotherItemsUseArt()
    {
        yield return LoadSceneAndWait("SecondFloor");

        ItemView clothes = FindItem("mother_01");
        ItemView box = FindItem("mother_02");
        ItemView diary = FindItem("mother_03");
        Assert.IsNotNull(clothes);
        Assert.IsNotNull(box);
        Assert.IsNotNull(diary);
        StringAssert.Contains("mother_01_Front", clothes.spriteRenderer.sprite.name);
        StringAssert.Contains("mother_02_Side", box.spriteRenderer.sprite.name);
        StringAssert.Contains("mother_03_Front", diary.spriteRenderer.sprite.name);
    }

    private static IEnumerator LoadSceneAndWait(string sceneName)
    {
        yield return null;
        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
        }
        yield return null;

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (load != null && !load.isDone) yield return null;
        yield return null;
        yield return null;

        float start = Time.realtimeSinceStartup;
        while (GameRoot.Instance.Flow.Phase != TurnPhase.PlayerInput
            && Time.realtimeSinceStartup - start < 10f)
        {
            yield return null;
        }
    }

    private static ItemView FindItem(string itemId)
    {
        ItemView[] views = UnityEngine.Object.FindObjectsOfType<ItemView>();
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null && views[i].itemId == itemId) return views[i];
        }
        return null;
    }
}
