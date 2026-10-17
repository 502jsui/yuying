using UnityEngine;

/// <summary>
/// 旁白层的全局状态。开场独白播放期间 IsBlockingInput = true，
/// PlayerController 会据此禁止移动（和 SceneTransitionState.IsLoading 一个套路）。
/// </summary>
public static class NarrationState
{
    /// <summary>开场独白 / A1 播放中：玩家不能移动。</summary>
    public static bool IsBlockingInput { get; private set; }

    /// <summary>开场序列是否还在跑（调试 / 测试用）。</summary>
    public static bool IsIntroPlaying { get; private set; }

    /// <summary>触碰旁白（B1/B2 + 物品独白）是否还在播。NPC 阶段要等它结束再拉镜头。</summary>
    public static bool IsTouchNarrationPlaying { get; private set; }

    public static void SetBlockingInput(bool value)
    {
        IsBlockingInput = value;
    }

    public static void SetIntroPlaying(bool value)
    {
        IsIntroPlaying = value;
    }

    public static void SetTouchNarrationPlaying(bool value)
    {
        IsTouchNarrationPlaying = value;
    }

    public static void Reset()
    {
        IsBlockingInput = false;
        IsIntroPlaying = false;
        IsTouchNarrationPlaying = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        Reset();
    }
}
