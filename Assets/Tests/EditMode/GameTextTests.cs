using NUnit.Framework;
using UnityEngine;

/// <summary>交付文案的映射 / 兜底测试。</summary>
public class GameTextTests
{
    private GameText text;

    [SetUp]
    public void SetUp()
    {
        text = ScriptableObject.CreateInstance<GameText>();
        text.ApplyDefaults();
    }

    [TearDown]
    public void TearDown()
    {
        if (text != null) Object.DestroyImmediate(text);
    }

    [Test]
    public void Defaults_ContainDeliveredCopy()
    {
        Assert.AreEqual(6, text.openingMonologue.Length);
        StringAssert.Contains("好冷", text.openingMonologue[0]);
        StringAssert.Contains("门是进不去的", text.tutorialA1);
        StringAssert.Contains("方向键", text.tutorialA2);
        StringAssert.Contains("只能触碰1次", text.tutorialC3);
        Assert.AreEqual("得走近一点才碰得到", text.tutorialD1);
        Assert.AreEqual("这扇门一直是关着的", text.tutorialD2);
        Assert.AreEqual("这里没什么可碰的", text.tutorialD3);
        Assert.AreEqual("已经收好了，不需要再动了", text.alreadyTouched);
    }

    [Test]
    public void OpeningLine_IsBoundsSafe()
    {
        Assert.IsNotNull(text.OpeningLine(0));
        Assert.IsNotNull(text.OpeningLine(5));
        Assert.IsNull(text.OpeningLine(-1));
        Assert.IsNull(text.OpeningLine(6));
    }

    [Test]
    public void ItemMonologue_MapsByItemId()
    {
        StringAssert.Contains("针线活", text.ItemMonologueFor("grandma_01"));
        StringAssert.Contains("很细心", text.ItemMonologueFor("grandma_02"));
        StringAssert.Contains("很有条理", text.ItemMonologueFor("grandma_03"));
        StringAssert.Contains("有点歪", text.ItemMonologueFor("aunt_01"));
        StringAssert.Contains("木雕", text.ItemMonologueFor("aunt_02"));
        StringAssert.Contains("工具", text.ItemMonologueFor("aunt_03"));
        Assert.IsNull(text.ItemMonologueFor("mother_01"));
        Assert.IsNull(text.ItemMonologueFor(null));
    }

    [Test]
    public void ForceTouchWarning_PicksByCount_AndClamps()
    {
        StringAssert.Contains("好重", text.ForceTouchWarning(1));
        StringAssert.Contains("故意放在这", text.ForceTouchWarning(2));
        StringAssert.Contains("故意放在这", text.ForceTouchWarning(9));
    }

    [Test]
    public void ForceTouchAccepted_UsesHeavyLineForWoodenBox()
    {
        Assert.AreEqual(text.forceTouchHeavy, text.ForceTouchAcceptedLine(ItemType.WoodenBox));

        string normal = text.ForceTouchAcceptedLine(ItemType.Clothes);
        Assert.IsFalse(string.IsNullOrEmpty(normal));
        CollectionAssert.Contains(text.forceTouchAccepted, normal);
    }

    [Test]
    public void ForceTouchAccepted_UsesHeavyLineForAllHeavyTypes()
    {
        Assert.AreEqual(text.forceTouchHeavy, text.ForceTouchAcceptedLine(ItemType.WoodenBox));
        Assert.AreEqual(text.forceTouchHeavy, text.ForceTouchAcceptedLine(ItemType.ToolBag));
        Assert.AreEqual(text.forceTouchHeavy, text.ForceTouchAcceptedLine(ItemType.WoodCarving));
        Assert.AreEqual(text.forceTouchHeavy, text.ForceTouchAcceptedLine(ItemType.FlowerPot));

        ItemType[] lightTypes = { ItemType.Clothes, ItemType.MedicineBag, ItemType.Diary };
        for (int i = 0; i < lightTypes.Length; i++)
        {
            string line = text.ForceTouchAcceptedLine(lightTypes[i]);
            Assert.IsFalse(string.IsNullOrEmpty(line));
            CollectionAssert.Contains(text.forceTouchAccepted, line);
        }
    }

    [Test]
    public void PlacementBlocked_UsesPerTypeCopy()
    {
        Assert.AreEqual(text.placementBlockedClothes, text.PlacementBlocked(ItemType.Clothes, "fallback"));
        Assert.AreEqual(text.placementBlockedMedicineBag, text.PlacementBlocked(ItemType.MedicineBag, "fallback"));
        Assert.AreEqual(text.placementBlockedWoodenBox, text.PlacementBlocked(ItemType.WoodenBox, "fallback"));
        Assert.AreEqual("fallback", text.PlacementBlocked(ItemType.FlowerPot, "fallback"));
    }

    [Test]
    public void StuckHint_Rule_IsDeterministic()
    {
        Assert.AreEqual(PlayerController.StuckHint.D2, PlayerController.ResolveStuckHint(true, true));
        Assert.AreEqual(PlayerController.StuckHint.D2, PlayerController.ResolveStuckHint(true, false));
        Assert.AreEqual(PlayerController.StuckHint.D1, PlayerController.ResolveStuckHint(false, true));
        Assert.AreEqual(PlayerController.StuckHint.D3, PlayerController.ResolveStuckHint(false, false));
    }
}
