using NUnit.Framework;

/// <summary>程序分工 1.2「朝向跟随移动方向」的纯函数单测。</summary>
public class PlayerFacingTests
{
    [Test]
    public void Facing_FollowsHorizontalInput()
    {
        Assert.IsTrue(PlayerController.ResolveFacing(-1f, false), "往左走要朝左");
        Assert.IsFalse(PlayerController.ResolveFacing(1f, true), "往右走要朝右");
    }

    [Test]
    public void Facing_KeepsLastDirection_WhenIdle()
    {
        Assert.IsTrue(PlayerController.ResolveFacing(0f, true), "站着不动要保持朝左");
        Assert.IsFalse(PlayerController.ResolveFacing(0f, false), "站着不动要保持朝右");
        Assert.IsTrue(PlayerController.ResolveFacing(0.005f, true), "微小输入不算转向");
    }

    [Test]
    public void Facing_HasNoDeadZoneAtRealInput()
    {
        Assert.IsTrue(PlayerController.ResolveFacing(-0.02f, false));
        Assert.IsFalse(PlayerController.ResolveFacing(0.02f, true));
    }
}
