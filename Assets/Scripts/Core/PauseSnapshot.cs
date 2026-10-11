using UnityEngine;

/// <summary>
/// 暂停时留下的"现场"：暂停那一刻的画面截图 + 回来时要进的场景和玩家站位。
///
/// 暂停 = 跳到暂停场景（游戏场景被卸载），所以这些东西必须放在常驻对象这边，
/// 不能挂在场景里。暂停场景的背景就用这里的 Texture 画。
/// </summary>
public static class PauseSnapshot
{
    /// <summary>暂停那一刻的画面。可能为空（无渲染设备 / 截图失败），那背景就退回纯色。</summary>
    public static Texture2D Texture { get; private set; }

    /// <summary>点暂停时玩家在哪个区域，恢复时回到这个场景。</summary>
    public static AreaId ReturnArea { get; private set; }

    private static float pendingPlayerX;
    private static bool hasPendingPlayerX;

    public static bool HasSnapshot { get { return ReturnArea != AreaId.None; } }

    public static string ReturnSceneName { get { return SceneNameOf(ReturnArea); } }

    public static void Store(Texture2D texture, AreaId area, float playerX)
    {
        ReleaseTexture();

        Texture = texture;
        ReturnArea = area == AreaId.None ? AreaId.FirstFloor : area;
        pendingPlayerX = playerX;
        hasPendingPlayerX = true;
    }

    /// <summary>恢复完成之后清掉现场（截图也一起释放）。</summary>
    public static void Clear()
    {
        ReleaseTexture();
        ReturnArea = AreaId.None;
        hasPendingPlayerX = false;
        pendingPlayerX = 0f;
    }

    /// <summary>
    /// 只释放截图（暂停场景卸载时调用）。
    /// 站位信息要留到 PlayerController.Start 消费，所以这里不能一起清。
    /// </summary>
    public static void ReleaseTexture()
    {
        if (Texture == null) return;
        Object.Destroy(Texture);
        Texture = null;
    }

    /// <summary>
    /// 取回暂停前的站位，只成功一次（恢复时 PlayerController.Start 用它把玩家放回原地）。
    /// </summary>
    public static bool ConsumePlayerX(out float x)
    {
        x = pendingPlayerX;
        if (!hasPendingPlayerX) return false;

        hasPendingPlayerX = false;
        return true;
    }

    public static string SceneNameOf(AreaId area)
    {
        switch (area)
        {
            case AreaId.SecondFloor: return "SecondFloor";
            case AreaId.Backyard: return "Backyard";
            default: return "FirstFloor";
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Texture = null;
        ReturnArea = AreaId.None;
        hasPendingPlayerX = false;
        pendingPlayerX = 0f;
    }
}
