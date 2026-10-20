using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>主菜单开始按钮要真正切到一楼并自动开局，覆盖场景内持久化回调接线。</summary>
public class MainMenuPlayModeTests
{
    [TearDown]
    public void TearDown()
    {
        if (GameRoot.Instance != null) UnityEngine.Object.Destroy(GameRoot.Instance.gameObject);
    }

    [UnityTest]
    public IEnumerator StartButton_LoadsFirstFloor_AndSpawnsPlayer()
    {
        yield return null;
        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        AsyncOperation menuLoad = SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
        while (menuLoad != null && !menuLoad.isDone) yield return null;
        yield return null;

        GameObject startObject = GameObject.Find("StartButton");
        Assert.IsNotNull(startObject, "MainMenu 里找不到 StartButton");
        Button start = startObject.GetComponent<Button>();
        Assert.IsNotNull(start);
        Assert.Greater(start.onClick.GetPersistentEventCount(), 0,
            "StartButton 没有持久化点击回调，场景接线丢了");
        Assert.IsNotNull(start.onClick.GetPersistentTarget(0),
            "StartButton 的目标脚本引用丢失");

        start.onClick.Invoke();

        yield return WaitUntil(
            delegate { return SceneManager.GetActiveScene().name == "FirstFloor"; },
            10f,
            "点击开始后进入 FirstFloor");
        yield return WaitUntil(
            delegate
            {
                return GameRoot.Instance != null
                    && GameRoot.Instance.Flow != null
                    && GameRoot.Instance.Flow.State == GameState.Playing;
            },
            10f,
            "FirstFloor 自动开局");

        PlayerController player = UnityEngine.Object.FindObjectOfType<PlayerController>();
        Assert.IsNotNull(player, "一楼没有生成玩家");
        Assert.IsTrue(player.gameObject.activeInHierarchy, "玩家对象必须处于激活状态");
        Assert.IsNotNull(Camera.main, "一楼没有主相机");
        Assert.AreEqual(1, GameRoot.Instance.Session.round);
    }

    private static IEnumerator WaitUntil(Func<bool> condition, float timeout, string label)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - start < timeout) yield return null;
        Assert.IsTrue(condition(), label + " 超时。");
    }
}
