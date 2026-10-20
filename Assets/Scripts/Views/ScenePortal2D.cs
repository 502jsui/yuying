using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景边界上的门：玩家站进范围内按交互键才切场景（不再自动触发）。
/// 提示走 PortalHintEvent，由 PromptOverlay 画在屏幕下方。
/// </summary>
[DefaultExecutionOrder(150)]
[DisallowMultipleComponent]
public class ScenePortal2D : MonoBehaviour
{
    [Header("连接")]
    public Transform player;
    public string sceneName;
    public float radius = 1.45f;
    public bool enabledPortal = true;

    [Header("交互")]
    [Tooltip("玩家在范围内按这个键切场景。")]
    public KeyCode interactKey = KeyCode.F;

    [Tooltip("留空时按目标场景自动取词：去后院 = 出门，回一楼 = 回屋。")]
    public string hintText;

    [Header("入口身份")]
    public string portalId;
    public string arrivalPortalId;

    [Header("到达位置")]
    public float arrivalOffset = 2.6f;
    public float arrivalDirection = 1f;

    [Header("第二出口（可选）")]
    [Tooltip("同一个门口再提供一个目的地，例如一楼右侧既可去后院也可上二楼。")]
    public string alternateSceneName;
    public string alternateArrivalPortalId;
    public float alternateArrivalDirection = 1f;
    public KeyCode alternateInteractKey = KeyCode.G;
    public string alternateHintText;

    private bool hintShown;
    private string shownHint;
    private bool switching;

    private void Start()
    {
        if (SceneTransitionState.ConsumeArrival(portalId) && player != null)
        {
            Vector3 position = transform.position;
            position.x += arrivalOffset * arrivalDirection;
            player.position = position;
        }
    }

    private void OnDisable()
    {
        PublishHint(false, null);
    }

    private void Update()
    {
        if (!enabledPortal || switching || player == null || string.IsNullOrWhiteSpace(sceneName))
        {
            PublishHint(false, null);
            return;
        }

        if (!CanInteractNow())
        {
            PublishHint(false, null);
            return;
        }

        bool inRange = (player.position - transform.position).sqrMagnitude <= radius * radius;
        if (inRange) PublishHint(true, ResolveCombinedHintText());
        else PublishHint(false, null);

        if (inRange && Input.GetKeyDown(interactKey))
        {
            TryUsePortal();
        }
        else if (inRange && HasAlternateExit() && Input.GetKeyDown(alternateInteractKey))
        {
            TryUseAlternatePortal();
        }
    }

    /// <summary>输入和测试共用的入口：满足条件就切到目标场景，返回是否真的开始切。</summary>
    public bool TryUsePortal()
    {
        return TryUse(sceneName, arrivalPortalId, arrivalDirection);
    }

    /// <summary>第二出口：例如一楼右侧的 G 键上二楼。</summary>
    public bool TryUseAlternatePortal()
    {
        if (!HasAlternateExit()) return false;
        return TryUse(alternateSceneName, alternateArrivalPortalId, alternateArrivalDirection);
    }

    private bool TryUse(string targetScene, string targetArrivalPortalId, float targetArrivalDirection)
    {
        if (!enabledPortal || switching || player == null || string.IsNullOrWhiteSpace(targetScene)) return false;
        if (!CanInteractNow()) return false;
        if ((player.position - transform.position).sqrMagnitude > radius * radius) return false;

        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogWarning("[场景门] 目标场景没进 Build Settings：" + targetScene);
            return false;
        }

        switching = true;
        PublishHint(false, null);
        StartCoroutine(LoadNextScene(targetScene, targetArrivalPortalId));
        return true;
    }

    /// <summary>当前门口提示文案（双出口时合并成一行）。</summary>
    public string ResolveCombinedHintText()
    {
        string primary = ResolveHintText();
        if (!HasAlternateExit()) return primary;

        string alternate = ResolveHintText(alternateSceneName, alternateHintText, alternateInteractKey);
        if (string.IsNullOrEmpty(primary)) return alternate;
        if (string.IsNullOrEmpty(alternate)) return primary;
        return primary + "　·　" + alternate;
    }

    /// <summary>主出口提示文案（hintText 有值就优先用它）。</summary>
    public string ResolveHintText()
    {
        return ResolveHintText(sceneName, hintText, interactKey);
    }

    private bool HasAlternateExit()
    {
        return !string.IsNullOrWhiteSpace(alternateSceneName);
    }

    /// <summary>纯函数：按目标场景给门口提示（方便单测 / 以后改键）。</summary>
    public static string ResolveHintText(string targetScene, string overrideText, KeyCode key)
    {
        if (!string.IsNullOrEmpty(overrideText)) return overrideText;

        string keyName = key.ToString();
        if (targetScene == "Backyard") return "按 " + keyName + " 去后院";
        if (targetScene == "FirstFloor") return "按 " + keyName + " 回一楼";
        if (targetScene == "SecondFloor") return "按 " + keyName + " 上楼";
        return "按 " + keyName + " 切换场景";
    }

    /// <summary>切场景 / 旁白 / NPC 行动 / 暂停时都不让按门。</summary>
    private static bool CanInteractNow()
    {
        if (SceneTransitionState.IsLoading) return false;
        if (NarrationState.IsBlockingInput) return false;

        GameRoot root = GameRoot.Instance;
        if (root == null || root.Flow == null) return true;
        return root.Flow.CanPlayerMove;
    }

    private void PublishHint(bool available, string text)
    {
        if (hintShown == available && shownHint == text) return;

        hintShown = available;
        shownHint = text;
        EventBus.Publish(new PortalHintEvent
        {
            available = available,
            text = text
        });
    }

    /// <summary>淡出 0.25 秒 → 异步加载下一个场景 → 淡入；避免切场景时的卡顿。</summary>
    private System.Collections.IEnumerator LoadNextScene(string targetScene, string targetArrivalPortalId)
    {
        SceneTransitionState.SetArrival(targetArrivalPortalId);
        SceneTransitionState.BeginLoad();

        while (SceneTransitionState.FadeAlpha < 0.99f) yield return null;

        AsyncOperation operation = SceneManager.LoadSceneAsync(targetScene);
        while (operation != null && !operation.isDone) yield return null;

        // 正常情况到这里已经随场景销毁了；如果没销毁（比如目标场景没配好），这里兜一次底。
        SceneTransitionState.EndLoad();
    }
}
