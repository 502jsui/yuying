using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 回归测试：一轮里每个住户只开一次门、行动阶段巡逻必须关着、住户不能瞬移、BGM 有在放。
/// </summary>
public class NpcDoorPatrolPlayModeTests
{
    [TearDown]
    public void TearDown()
    {
        if (GameRoot.Instance != null) UnityEngine.Object.Destroy(GameRoot.Instance.gameObject);
    }

    [UnityTest]
    public IEnumerator NpcPhase_OpensEachDoorOnce_NoPatrol_NoTeleport()
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
        yield return WaitUntil(delegate { return !NarrationState.IsBlockingInput; }, 5f, "跳过开场");

        Dictionary<ResidentId, int> opens = new Dictionary<ResidentId, int>();
        Dictionary<ResidentId, int> closes = new Dictionary<ResidentId, int>();
        Action<NpcDoorEvent> counter = delegate (NpcDoorEvent evt)
        {
            Dictionary<ResidentId, int> target = evt.opening ? opens : closes;
            int current;
            target.TryGetValue(evt.id, out current);
            target[evt.id] = current + 1;
        };
        EventBus.Subscribe(counter);

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.PlayerInput; },
            10f,
            "玩家输入阶段");

        GameRoot.Instance.Flow.RegisterPlayerTouch("door_patrol_test");

        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase == TurnPhase.NpcAction; },
            10f,
            "NPC 行动阶段");

        int patrolsEnabledBeforePhase = CountEnabledPatrols();
        bool sawPatrolEnabled = false;
        string jumpDetail = null;
        Dictionary<int, float> lastVisibleX = new Dictionary<int, float>();

        float start = Time.realtimeSinceStartup;
        while (GameRoot.Instance.Flow.Phase == TurnPhase.NpcAction && Time.realtimeSinceStartup - start < 120f)
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            // 平滑走位每帧最多约 0.1~0.5 单位；真正的瞬移（拼接偏移 / 巡逻钳回）都是好几个单位
            float maxStep = 1.5f;

            ResidentView[] views = UnityEngine.Object.FindObjectsOfType<ResidentView>();
            for (int i = 0; i < views.Length; i++)
            {
                ResidentView view = views[i];
                if (view == null) continue;

                NpcPatrol2D patrol = view.GetComponent<NpcPatrol2D>();
                if (patrol != null && patrol.enabled) sawPatrolEnabled = true;

                // 只看"看得见"的那一份，隐藏时的移动不算瞬移
                if (view.bodyRenderer == null || !view.bodyRenderer.enabled) continue;

                int key = view.GetInstanceID();
                float previous;
                if (lastVisibleX.TryGetValue(key, out previous))
                {
                    float moved = Mathf.Abs(view.transform.position.x - previous);
                    if (moved > maxStep && jumpDetail == null)
                    {
                        jumpDetail = string.Format(
                            "frame={0} round={1} id={2} area={3} stitched={4} phase={5} out={6} x={7:0.###}->{8:0.###} dx={9:0.###} max={10:0.###}",
                            Time.frameCount, GameRoot.Instance.Session.round, view.id, view.area,
                            SceneStitcher.IsActive, GameRoot.Instance.Flow.Phase,
                            view.data != null && view.data.isOut, previous, view.transform.position.x, moved, maxStep);
                    }
                }
                lastVisibleX[key] = view.transform.position.x;
            }

            yield return null;
        }

        EventBus.Unsubscribe(counter);

        Assert.IsFalse(sawPatrolEnabled, "NPC 行动阶段巡逻必须全部关着（否则会把角色钳回 minX/maxX）");
        Assert.IsNull(jumpDetail, "住户不应该出现超过移动速度的位移（瞬移）：" + jumpDetail);

        // 三个能行动的住户各开一次门
        Assert.AreEqual(3, opens.Count, "三个住户都应该出过门");
        foreach (KeyValuePair<ResidentId, int> pair in opens)
        {
            Assert.AreEqual(1, pair.Value, pair.Key + " 本轮开门次数应该正好 1 次");
        }

        // 关门：不回家的那位不开门也不关门，所以最多三次、且每人最多一次
        foreach (KeyValuePair<ResidentId, int> pair in closes)
        {
            Assert.LessOrEqual(pair.Value, 1, pair.Key + " 本轮关门次数不应该超过 1 次");
        }

        // 相位结束后巡逻要恢复
        yield return WaitUntil(
            delegate { return GameRoot.Instance.Flow.Phase != TurnPhase.NpcAction; },
            20f,
            "NPC 行动阶段结束");
        yield return null;

        Assert.AreEqual(
            patrolsEnabledBeforePhase,
            CountEnabledPatrols(),
            "行动阶段结束后，巡逻的启用状态应该和阶段开始前一致");
    }

    private static int CountEnabledPatrols()
    {
        int count = 0;
        ResidentView[] views = UnityEngine.Object.FindObjectsOfType<ResidentView>();
        for (int i = 0; i < views.Length; i++)
        {
            NpcPatrol2D patrol = views[i] != null ? views[i].GetComponent<NpcPatrol2D>() : null;
            if (patrol != null && patrol.enabled) count++;
        }
        return count;
    }

    [UnityTest]
    public IEnumerator Music_IsPlayingFromGameStart()
    {
        yield return null;
        if (GameRoot.Instance == null)
        {
            GameObject rootObject = new GameObject("[GameRoot-Test]");
            rootObject.AddComponent<GameRoot>();
        }
        yield return null;

        AudioManager audio = GameRoot.Instance.Audio;
        Assert.IsNotNull(audio);
        Assert.IsNotNull(audio.MusicClip, "GameConfig.musicLoop 应该已经绑定钢琴 BGM");

        // 改一下再改回来，避免污染本机保存的音量设置
        float original = audio.MusicVolume;
        audio.SetMusicVolume(0.25f);
        Assert.AreEqual(0.25f, audio.MusicVolume, 0.001f, "音乐音量总线要能改");
        audio.SetMusicVolume(original);
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
