using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// "明明看着没人，碰东西却说有人" 的回归测试：
/// 把区域占用故意记成一个根本不在该区域的人（模拟旧 bug 留下的脏数据），
/// 断言这个区域会被校验掉、触碰不会弹强行触碰警告。
/// </summary>
public class GhostAreaBlockPlayModeTests
{
    [TearDown]
    public void TearDown()
    {
        if (GameRoot.Instance != null) UnityEngine.Object.Destroy(GameRoot.Instance.gameObject);
    }

    [UnityTest]
    public IEnumerator StaleAreaBlock_IsIgnored_AndDoesNotWarn()
    {
        yield return null;
        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
        }
        yield return null;

        AsyncOperation load = SceneManager.LoadSceneAsync("FirstFloor", LoadSceneMode.Single);
        while (load != null && !load.isDone) yield return null;
        yield return null;
        yield return null;

        IntroNarration intro = UnityEngine.Object.FindObjectOfType<IntroNarration>();
        if (intro != null) intro.RequestSkip();

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; },
            10f,
            "玩家输入阶段");

        GameSession session = GameRoot.Instance.Session;
        ItemSystem items = GameRoot.Instance.Items;

        // 造脏数据：老婆婆实际在后院，却把"一楼"标成被她占着
        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        grandma.currentArea = AreaId.Backyard;
        grandma.isOut = true;
        session.SetNpcState(ResidentId.Grandma, NpcState.StayOut);
        items.SetAreaBlocked(AreaId.FirstFloor, ResidentId.Grandma);

        // 校验式查询应该立刻识别出"人不在这个区域"
        Assert.IsFalse(items.IsAreaBlocked(AreaId.FirstFloor), "人不在的区域不应该算被占着");

        ResidentId blocker;
        Assert.IsFalse(items.TryGetBlockedResident(AreaId.FirstFloor, out blocker));

        // 找一件一楼的物品，触碰它：不应该因为"幽灵占用"弹强行触碰警告
        ItemData target = null;
        for (int i = 0; i < session.items.Count; i++)
        {
            ItemData item = session.items[i];
            if (item.IsInScene && !item.isReturned && !item.hasTemperature && item.area == AreaId.FirstFloor)
            {
                target = item;
                break;
            }
        }
        Assert.IsNotNull(target, "一楼应该有一件可以碰的物品");

        int warned = 0;
        Action<ForceTouchWarnedEvent> handler = delegate (ForceTouchWarnedEvent evt) { warned++; };
        EventBus.Subscribe(handler);

        session.touchesThisRound = 0;
        ItemActionResult result = items.Touch(target.itemId);

        EventBus.Unsubscribe(handler);

        Assert.AreEqual(0, warned, "区域里没人，就不该出现强行触碰的警告");
        Assert.AreNotEqual(ItemActionStatus.ForceTouchWarned, result.status);
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
