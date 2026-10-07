/// <summary>
/// 场景名 ↔ 区域 的映射。场景文件名改了要同步这里。
/// </summary>
public static class AreaIdUtil
{
    public static AreaId FromSceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return AreaId.None;
        switch (sceneName)
        {
            case "FirstFloor": return AreaId.FirstFloor;
            case "SecondFloor": return AreaId.SecondFloor;
            case "Backyard": return AreaId.Backyard;
            default: return AreaId.None;
        }
    }
}
