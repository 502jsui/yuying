using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 数值结算系统的纯逻辑单测：所有加减法和阈值都集中在这里。
/// </summary>
public class StatSettlementTests
{
    private GameConfig config;
    private GameSession session;
    private StatSettlement settlement;

    [SetUp]
    public void SetUp()
    {
        EventBus.Clear();
        config = ScriptableObject.CreateInstance<GameConfig>();
        config.EnsureDefaults();

        session = new GameSession();
        session.Reset(config, 12345);

        settlement = new StatSettlement(config, session);
    }

    [TearDown]
    public void TearDown()
    {
        EventBus.Clear();
        if (config != null) Object.DestroyImmediate(config);
    }

    // ---------------- 物品 ----------------

    [Test]
    public void ItemTaken_WithTemperature_Adds18And15()
    {
        ResidentData aunt = session.GetResident(ResidentId.Uncle);
        float warmth = aunt.warmth;
        float awareness = aunt.awareness;

        settlement.ApplyItemTaken(ResidentId.Uncle, true);

        Assert.AreEqual(warmth + 18f, aunt.warmth, 0.001f);
        Assert.AreEqual(awareness + 15f, aunt.awareness, 0.001f);
    }

    [Test]
    public void ItemTaken_WithoutTemperature_AddsOnlyAwareness()
    {
        ResidentData aunt = session.GetResident(ResidentId.Uncle);
        float warmth = aunt.warmth;
        float awareness = aunt.awareness;

        settlement.ApplyItemTaken(ResidentId.Uncle, false);

        Assert.AreEqual(warmth + 0f, aunt.warmth, 0.001f);
        Assert.AreEqual(awareness + 15f, aunt.awareness, 0.001f);
    }

    [Test]
    public void GirlMovedItem_Adds18AndClearsTemperature()
    {
        ItemData item = new ItemData("x", ItemOwner.Uncle);
        item.hasTemperature = true;

        ResidentData girl = session.GetResident(ResidentId.Girl);
        float warmth = girl.warmth;

        settlement.ApplyGirlMovedItem(item);

        Assert.AreEqual(warmth + 18f, girl.warmth, 0.001f);
        Assert.IsFalse(item.hasTemperature);
    }

    // ---------------- 交流 ----------------

    [Test]
    public void Talk_ToOtherAdult_Adds4And8()
    {
        ResidentData aunt = session.GetResident(ResidentId.Uncle);
        float warmth = aunt.warmth;
        float awareness = aunt.awareness;

        settlement.ApplyTalk(ResidentId.Uncle, false);

        Assert.AreEqual(warmth + 4f, aunt.warmth, 0.001f);
        Assert.AreEqual(awareness + 8f, aunt.awareness, 0.001f);
    }

    [Test]
    public void Talk_ToMother_Adds11AwarenessOnly()
    {
        ResidentData mother = session.GetResident(ResidentId.Mother);
        float warmth = mother.warmth;
        float awareness = mother.awareness;

        settlement.ApplyTalk(ResidentId.Mother, false);

        Assert.AreEqual(warmth + 0f, mother.warmth, 0.001f);
        Assert.AreEqual(awareness + 11f, mother.awareness, 0.001f);
    }

    [Test]
    public void Talk_WithContagion_TargetLoses5Warmth()
    {
        ResidentData aunt = session.GetResident(ResidentId.Uncle);
        float warmth = aunt.warmth;

        settlement.ApplyTalk(ResidentId.Uncle, true);

        Assert.AreEqual(warmth - 5f, aunt.warmth, 0.001f);
    }

    [Test]
    public void ForceTouch_And_Contagion_Penalties()
    {
        ResidentData grandma = session.GetResident(ResidentId.Grandma);
        ResidentData aunt = session.GetResident(ResidentId.Uncle);
        float grandmaWarmth = grandma.warmth;
        float auntWarmth = aunt.warmth;

        settlement.ApplyForceTouch(ResidentId.Grandma);
        settlement.ApplyForceTouchContagion(ResidentId.Uncle);

        Assert.AreEqual(grandmaWarmth - 8f, grandma.warmth, 0.001f);
        Assert.AreEqual(auntWarmth - 5f, aunt.warmth, 0.001f);
    }

    // ---------------- 母亲开门 ----------------

    [Test]
    public void Mother_WarmthStays30_DoorJudgedAtEnd()
    {
        ResidentData mother = session.GetResident(ResidentId.Mother);

        settlement.ApplyTalk(ResidentId.Mother, false);
        settlement.ApplyTalk(ResidentId.Mother, false);
        Assert.AreEqual(52f, mother.awareness, 0.001f);

        // 第 6 轮结束前不开门：暖意一直是 30。
        settlement.ApplyTalk(ResidentId.Mother, false);
        Assert.AreEqual(63f, mother.awareness, 0.001f);
        Assert.AreEqual(30f, mother.warmth, 0.001f);

        // 结算时判定开门 → 暖意跳到 61。
        Assert.IsTrue(settlement.ResolveMotherDoorAtEnd());
        Assert.AreEqual(61f, mother.warmth, 0.001f);
    }

    [Test]
    public void MotherDoor_StaysClosedWhenAwarenessNotHigh()
    {
        settlement.ApplyTalk(ResidentId.Mother, false);
        settlement.ApplyTalk(ResidentId.Mother, false);

        Assert.IsFalse(settlement.ResolveMotherDoorAtEnd());
        Assert.AreEqual(30f, session.GetResident(ResidentId.Mother).warmth, 0.001f);
    }

    [Test]
    public void Contagion_DoesNotMoveMotherWarmth()
    {
        settlement.ApplyForceTouchContagion(ResidentId.Mother);
        Assert.AreEqual(30f, session.GetResident(ResidentId.Mother).warmth, 0.001f);
    }

    // ---------------- 阈值 ----------------

    [Test]
    public void HighThreshold_IsStrictlyGreaterThan60()
    {
        Assert.IsFalse(settlement.IsHigh(60f));
        Assert.IsTrue(settlement.IsHigh(60.5f));
    }

    [Test]
    public void DoorSeam_Uses30And55()
    {
        Assert.AreEqual(StatSettlement.DoorSeamState.Closed, settlement.ResolveDoorSeam(29.9f));
        Assert.AreEqual(StatSettlement.DoorSeamState.Seam, settlement.ResolveDoorSeam(30f));
        Assert.AreEqual(StatSettlement.DoorSeamState.Seam, settlement.ResolveDoorSeam(54.9f));
        Assert.AreEqual(StatSettlement.DoorSeamState.HalfOpen, settlement.ResolveDoorSeam(55f));
    }

    [Test]
    public void DoorLight_Uses30And54()
    {
        Assert.AreEqual(StatSettlement.DoorLightTint.White, settlement.ResolveLightTint(29f, true));
        Assert.AreEqual(StatSettlement.DoorLightTint.PaleYellow, settlement.ResolveLightTint(30f, true));
        Assert.AreEqual(StatSettlement.DoorLightTint.PaleYellow, settlement.ResolveLightTint(54f, true));
        Assert.AreEqual(StatSettlement.DoorLightTint.Orange, settlement.ResolveLightTint(55f, true));
        Assert.AreEqual(StatSettlement.DoorLightTint.White, settlement.ResolveLightTint(90f, false));
    }

    [Test]
    public void MotherOpen_Uses60()
    {
        Assert.IsFalse(settlement.IsMotherOpen(60f));
        Assert.IsTrue(settlement.IsMotherOpen(60.1f));
    }
}
