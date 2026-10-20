using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 一轮收尾时住户"走回家"的表现：
///   1. 只要人还站在走廊上（没到门口），就必须看得见 —— 不能先集体消失、再一个一个冒出来；
///   2. 开过门的人，回家时必须有关门（不回家那位不算）。
/// </summary>
public class NpcRoundEndVisibilityTests
{
    [TearDown]
    public void TearDown()
    {
        if (GameRoot.Instance != null) UnityEngine.Object.Destroy(GameRoot.Instance.gameObject);
    }

    [UnityTest]
    public IEnumerator RoundEnd_WalksHome_VisibleAllTheWay_AndClosesEveryDoor()
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
        GameRoot.Instance.Config.absentRoundChance = 0f;

        IntroNarration intro = UnityEngine.Object.FindObjectOfType<IntroNarration>();
        if (intro != null) intro.RequestSkip();
        yield return WaitUntil(delegate { return !NarrationState.IsBlockingInput; }, 5f);

        Dictionary<ResidentId, int> opens = new Dictionary<ResidentId, int>();
        Dictionary<ResidentId, int> closes = new Dictionary<ResidentId, int>();
        Action<NpcDoorEvent> counter = delegate (NpcDoorEvent evt)
        {
            Dictionary<ResidentId, int> target = evt.opening ? opens : closes;
            int current;
            target.TryGetValue(evt.id, out current);
            target[evt.id] = current + 1;
        };

        yield return WaitUntil(delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; }, 10f);

        EventBus.Subscribe(counter);
        GameRoot.Instance.Flow.RegisterPlayerTouch("round_end_visibility_test");
        GameRoot.Instance.Narration.FinishTouchNarration();

        yield return WaitUntil(delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.NpcAction; }, 10f);

        // 盯着整个行动阶段（含最后的"走回家"）：人不在门口就必须可见。
        string hiddenAwayFromHome = null;
        float start = Time.realtimeSinceStartup;
        while (GameRoot.Instance.Flow.Phase == TurnPhase.NpcAction && Time.realtimeSinceStartup - start < 120f)
        {
            ResidentView[] views = UnityEngine.Object.FindObjectsOfType<ResidentView>();
            for (int i = 0; i < views.Length; i++)
            {
                ResidentView view = views[i];
                if (view == null || view.bodyRenderer == null || view.data == null) continue;
                if (view.area != GameRoot.Instance.Session.playerArea) continue;

                // 人已经走到别的场景去了（currentArea 变了）——那一份 View 隐藏是对的，不算问题。
                if (view.data.currentArea != view.area) continue;

                bool awayFromHome = Mathf.Abs(view.transform.position.x - view.HomePosition.x) > 0.15f;
                if (awayFromHome && !view.bodyRenderer.enabled && hiddenAwayFromHome == null)
                {
                    hiddenAwayFromHome = string.Format(
                        "frame={0} id={1} x={2:0.###} home={3:0.###} state={4} isOut={5}",
                        Time.frameCount, view.id, view.transform.position.x, view.HomePosition.x,
                        view.data.state, view.data.isOut);
                }
            }
            yield return null;
        }

        EventBus.Unsubscribe(counter);

        bool hasAbsent = GameRoot.Instance.Session.hasAbsentResident;
        int expectedCloses = 3 - (hasAbsent ? 1 : 0);

        Assert.IsNull(hiddenAwayFromHome,
            "住户站在走廊上却不可见（应该一直可见地走回门口）：" + hiddenAwayFromHome);
        Assert.AreEqual(expectedCloses, closes.Count,
            "开过门的人回家时都该有关门声（不回家那位除外）。开门=" + opens.Count + " 关门=" + closes.Count
            + " 不回家=" + hasAbsent);

        yield return WaitUntil(
            delegate
            {
                return GameRoot.Instance.Session.round == 2
                    && GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput;
            },
            10f);

        AssertResidentAtOwnDoor(ResidentId.Grandma);
        AssertResidentAtOwnDoor(ResidentId.Uncle);

        AsyncOperation loadSecond = SceneManager.LoadSceneAsync("SecondFloor", LoadSceneMode.Single);
        while (loadSecond != null && !loadSecond.isDone) yield return null;
        yield return null;
        yield return null;
        AssertResidentAtOwnDoor(ResidentId.Girl);
    }

    private static IEnumerator WaitUntil(Func<bool> condition, float timeout)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - start < timeout) yield return null;
    }

    private static void AssertResidentAtOwnDoor(ResidentId id)
    {
        AreaId expectedArea = id == ResidentId.Girl ? AreaId.SecondFloor : AreaId.FirstFloor;
        ResidentView[] views = UnityEngine.Object.FindObjectsOfType<ResidentView>();
        for (int i = 0; i < views.Length; i++)
        {
            ResidentView view = views[i];
            if (view == null || view.id != id || view.area != expectedArea) continue;
            Assert.AreEqual(expectedArea, view.data.currentArea, id + " 应回到自己家");
            Assert.AreEqual(view.HomePosition.x, view.transform.position.x, 0.05f, id + " 应走到自己门口");
            return;
        }
        Assert.Fail("找不到 " + id + " 的 " + expectedArea + " View");
    }
}
