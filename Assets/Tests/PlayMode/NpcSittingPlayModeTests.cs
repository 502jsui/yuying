using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 老婆婆 / 大叔的坐姿表现：区域待机时显示坐姿图，行动结束走回家后恢复到坐姿。
/// </summary>
public class NpcSittingPlayModeTests
{
    [TearDown]
    public void TearDown()
    {
        if (GameRoot.Instance != null) UnityEngine.Object.Destroy(GameRoot.Instance.gameObject);
    }

    [UnityTest]
    public IEnumerator Adults_AreHiddenAtOpening_ThenCanShowSittingArt()
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

        GameRoot root = GameRoot.Instance;
        root.Config.absentRoundChance = 0f;

        IntroNarration intro = UnityEngine.Object.FindObjectOfType<IntroNarration>();
        if (intro != null) intro.RequestSkip();
        yield return WaitUntil(delegate { return !NarrationState.IsBlockingInput; }, 10f);
        yield return WaitUntil(delegate { return root.Flow.Phase == TurnPhase.PlayerInput; }, 10f);

        AssertAdultSittingHidden(ResidentId.Grandma);
        AssertAdultSittingHidden(ResidentId.Uncle);

        // 已验证回家坐姿仍然隐藏；下面切换为出门状态检查动作图与朝向。
        ResidentView grandma = FindView(ResidentId.Grandma);
        ResidentView uncle = FindView(ResidentId.Uncle);

        ResidentData grandmaData = root.Session.GetResident(ResidentId.Grandma);
        ResidentData uncleData = root.Session.GetResident(ResidentId.Uncle);
        grandmaData.isOut = true;
        uncleData.isOut = true;
        grandmaData.state = NpcState.GoingOut;
        uncleData.state = NpcState.GoingOut;
        root.Npc.SetVisualPose(ResidentId.Grandma, NpcVisualPose.Walking);
        root.Npc.SetVisualPose(ResidentId.Uncle, NpcVisualPose.Walking);
        AssertAdultPose(ResidentId.Grandma, "Grandma_Walking");
        AssertAdultPose(ResidentId.Uncle, "Uncle_Walking");
        grandma.SetFacing(true);
        uncle.SetFacing(true);
        Assert.IsTrue(grandma.bodyRenderer.flipX);
        Assert.IsTrue(uncle.bodyRenderer.flipX);
        grandma.SetFacing(false);
        uncle.SetFacing(false);
        Assert.IsFalse(grandma.bodyRenderer.flipX);
        Assert.IsFalse(uncle.bodyRenderer.flipX);

        grandmaData.state = NpcState.StayOut;
        grandmaData.isOut = true;
        grandma.RefreshVisual();
        Assert.IsTrue(grandma.bodyRenderer.enabled, "没回家的住户必须可见");

        root.Npc.SetVisualPose(ResidentId.Grandma, NpcVisualPose.Talking);
        root.Npc.SetVisualPose(ResidentId.Uncle, NpcVisualPose.Talking);
        AssertAdultPose(ResidentId.Grandma, "Grandma_Talking");
        AssertAdultPose(ResidentId.Uncle, "Uncle_Talking");

        AsyncOperation loadSecond = SceneManager.LoadSceneAsync("SecondFloor", LoadSceneMode.Single);
        while (loadSecond != null && !loadSecond.isDone) yield return null;
        yield return null;
        yield return null;

        ResidentData girl = root.Session.GetResident(ResidentId.Girl);
        ResidentView girlView = FindView(ResidentId.Girl, AreaId.SecondFloor);
        Assert.IsNotNull(girlView);
        Assert.IsNotNull(girlView.bodyRenderer.sprite);
        StringAssert.Contains("Girl_Standing", girlView.bodyRenderer.sprite.name);
        girl.state = NpcState.GoingOut;
        girl.visualPose = NpcVisualPose.Walking;
        girlView.RefreshVisual();
        StringAssert.Contains("Girl_Walking", girlView.bodyRenderer.sprite.name);
    }

    private static void AssertAdultSittingHidden(ResidentId id)
    {
        ResidentView target = FindView(id);
        Assert.IsNotNull(target);
        Assert.IsNotNull(target.data);
        Assert.AreEqual(NpcState.Sitting, target.data.state);
        Assert.IsFalse(target.data.IsOutside, "回家坐姿不能显示在走廊");
        Assert.IsFalse(target.bodyRenderer.enabled, "已经回家的住户应保持隐藏");
        Assert.IsNotNull(target.bodyRenderer.sprite);
        StringAssert.Contains(id == ResidentId.Grandma ? "Grandma_Sitting" : "Uncle_Sitting",
            target.bodyRenderer.sprite.name);
    }

    private static void AssertAdultSitting(ResidentId id, string spriteName)
    {
        ResidentView target = FindView(id);
        Assert.IsNotNull(target, "找不到住户 View：" + id);
        Assert.IsNotNull(target.data);
        Assert.AreEqual(NpcState.Sitting, target.data.state);
        Assert.IsNotNull(target.bodyRenderer);
        Assert.IsTrue(target.bodyRenderer.enabled, "坐姿住户应该在场景里可见");
        Assert.IsNotNull(target.bodyRenderer.sprite);
        StringAssert.Contains(spriteName, target.bodyRenderer.sprite.name);
    }

    private static void AssertAdultPose(ResidentId id, string spriteName)
    {
        ResidentView target = FindView(id);
        Assert.IsNotNull(target);
        Assert.IsTrue(target.bodyRenderer.enabled);
        Assert.IsNotNull(target.bodyRenderer.sprite);
        StringAssert.Contains(spriteName, target.bodyRenderer.sprite.name);
    }

    private static ResidentView FindView(ResidentId id)
    {
        return FindView(id, AreaId.FirstFloor);
    }

    private static ResidentView FindView(ResidentId id, AreaId area)
    {
        ResidentView[] views = UnityEngine.Object.FindObjectsOfType<ResidentView>();
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null && views[i].id == id && views[i].area == area)
                return views[i];
        }
        return null;
    }

    private static IEnumerator WaitUntil(Func<bool> condition, float timeout)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - start < timeout) yield return null;
        Assert.IsTrue(condition(), "等待超时。");
    }
}
