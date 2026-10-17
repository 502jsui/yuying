using UnityEngine;

/// <summary>
/// 切场景（传送门）期间的共享状态：谁在切、淡入淡出到什么程度、这次从哪个门进来。
///
/// 写的人：`ScenePortal2D`（开始 / 结束 / 记落点）、`SceneFadeOverlay`（加载完成收尾）。
/// 读的人：`PlayerController`（这段时间不许动）、`ScenePortal2D`、`PauseDirector`（切场景期间不许暂停）。
///
/// 放 Core 而不是 Views：它是被逻辑和表现两边共同读写的**共享状态**，
/// 放在 Views 里会让 Core 反向依赖 Views。
/// </summary>
public static class SceneTransitionState
{
    public static string ArrivalPortalId { get; private set; }
    public static bool HasArrival { get; private set; }

    /// <summary>正在切场景：这段时间玩家不能动，画面在淡入淡出。</summary>
    public static bool IsLoading { get; private set; }

    /// <summary>0 = 全透明，1 = 全黑。</summary>
    public static float FadeAlpha { get; private set; }

    public const float FadeDuration = 0.25f;

    public static void BeginLoad()
    {
        IsLoading = true;
    }

    public static void EndLoad()
    {
        IsLoading = false;
    }

    /// <summary>由 SceneFadeOverlay 每帧推进。</summary>
    public static void TickFade(float deltaTime)
    {
        float target = IsLoading ? 1f : 0f;
        FadeAlpha = Mathf.MoveTowards(FadeAlpha, target, Mathf.Max(0.0001f, deltaTime) / FadeDuration);
    }

    public static void SetArrival(string portalId)
    {
        ArrivalPortalId = portalId;
        HasArrival = !string.IsNullOrWhiteSpace(portalId);
    }

    public static bool ConsumeArrival(string portalId)
    {
        if (!HasArrival || string.IsNullOrWhiteSpace(portalId) || ArrivalPortalId != portalId) return false;
        HasArrival = false;
        ArrivalPortalId = null;
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ArrivalPortalId = null;
        HasArrival = false;
        IsLoading = false;
        FadeAlpha = 0f;
    }
}
