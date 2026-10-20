using System.Collections;
using UnityEngine;

/// <summary>
/// 场景相机。按《余音_相机规范》：
///   - 相机固定，不跟随玩家；
///   - 场景与屏幕等大：整个场景完整地填满一屏（16:9 设计分辨率下是精确等大）。
///
/// 做法：给出场景在世界坐标里的矩形范围，相机自动取
///   size = max(场景高/2, 场景宽/(2*屏幕宽高比))
/// 这样整个场景一定可见，并且在目标宽高比下刚好铺满屏幕。
/// NPC 行动阶段会把一楼、二楼和后院拼成整栋楼，镜头用动画拉远 / 拉回，
/// 开场独白则先推近到主角，再回到单场景取景。
/// </summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public class SceneCameraFollow2D : MonoBehaviour
{
    /// <summary>设计分辨率宽高比（16:9）。编辑期也按它取景，保证保存下来的相机参数一致。</summary>
    public const float DesignAspect = 16f / 9f;

    [Header("场景范围（世界坐标）")]
    public float sceneMinX = -9.6f;
    public float sceneMaxX = 9.6f;
    public float sceneMinY = -5.4f;
    public float sceneMaxY = 5.4f;

    [Header("留白")]
    [Min(0f)] public float padding = 0f;

    [Header("镜头动画（秒）")]
    [Tooltip("NPC 行动阶段拉到拼接全场景的时长。")]
    [Min(0f)] public float npcZoomOutDuration = 0.8f;

    [Tooltip("NPC 行动结束、回到单场景取景的时长。")]
    [Min(0f)] public float npcZoomBackDuration = 0.6f;

    private Camera cameraComponent;
    private float lastAspect;
    private Coroutine animationRoutine;
    private bool animating;

    /// <summary>镜头是否正在做动画（SceneStitcher 用它等拉远 / 拉回结束）。</summary>
    public bool IsAnimating { get { return animating; } }

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        ApplyFraming();
    }

    private void OnEnable()
    {
        if (cameraComponent == null) cameraComponent = GetComponent<Camera>();
        if (!animating) ApplyFraming();
    }

    private void OnDisable()
    {
        CancelAnimation();
    }

    private void LateUpdate()
    {
        // 动画期间由动画自己写位置 / 尺寸，别被分辨率检查打断。
        if (animating) return;

        // 分辨率 / 窗口比例变化时重新取景。
        if (cameraComponent != null && !Mathf.Approximately(lastAspect, cameraComponent.aspect))
        {
            ApplyFraming();
        }
    }

    /// <summary>场景在世界坐标里的宽度。</summary>
    public float SceneWidth
    {
        get { return Mathf.Max(0.01f, sceneMaxX - sceneMinX); }
    }

    /// <summary>场景在世界坐标里的高度。</summary>
    public float SceneHeight
    {
        get { return Mathf.Max(0.01f, sceneMaxY - sceneMinY); }
    }

    /// <summary>按当前屏幕比例算出“刚好装下整个场景”的正交尺寸。</summary>
    public float ComputeOrthographicSize()
    {
        return ComputeOrthographicSizeFor(sceneMinX, sceneMaxX, sceneMinY, sceneMaxY);
    }

    /// <summary>给一组世界边界，算“刚好装下它”的正交尺寸。</summary>
    public float ComputeOrthographicSizeFor(float minX, float maxX, float minY, float maxY)
    {
        float aspect = ResolveAspect();
        float width = Mathf.Max(0.01f, maxX - minX) + padding * 2f;
        float height = Mathf.Max(0.01f, maxY - minY) + padding * 2f;

        float sizeByHeight = height * 0.5f;
        float sizeByWidth = width / (2f * aspect);
        return Mathf.Max(sizeByHeight, sizeByWidth);
    }

    private float ResolveAspect()
    {
        if (!Application.isPlaying)
        {
            // 编辑期 Game 视图比例不一定是 16:9，统一按设计比例算，
            // 否则保存进场景的相机尺寸会随编辑器窗口变化。
            return DesignAspect;
        }

        return cameraComponent != null && cameraComponent.aspect > 0.01f
            ? cameraComponent.aspect
            : DesignAspect;
    }

    /// <summary>把相机固定到场景中心，并取景到整个场景。</summary>
    public void ApplyFraming()
    {
        CancelAnimation();

        if (cameraComponent == null) cameraComponent = GetComponent<Camera>();
        if (cameraComponent == null) return;

        float centerX = (sceneMinX + sceneMaxX) * 0.5f;
        float centerY = (sceneMinY + sceneMaxY) * 0.5f;
        transform.position = new Vector3(centerX, centerY, transform.position.z);

        cameraComponent.orthographic = true;
        cameraComponent.orthographicSize = ComputeOrthographicSize();
        lastAspect = cameraComponent.aspect;
    }

    /// <summary>把镜头动画到指定中心点和正交尺寸（smoothstep、非缩放时间）。</summary>
    public void AnimateTo(Vector3 center, float size, float duration)
    {
        if (cameraComponent == null) cameraComponent = GetComponent<Camera>();
        if (cameraComponent == null) return;

        CancelAnimation();

        Vector3 targetPosition = new Vector3(center.x, center.y, transform.position.z);
        float targetSize = Mathf.Max(0.01f, size);

        if (duration <= 0.0001f)
        {
            transform.position = targetPosition;
            cameraComponent.orthographicSize = targetSize;
            lastAspect = cameraComponent.aspect;
            return;
        }

        animationRoutine = StartCoroutine(AnimateRoutine(targetPosition, targetSize, duration));
    }

    /// <summary>把镜头动画到一组世界边界的完整取景。</summary>
    public void AnimateToBounds(float minX, float maxX, float minY, float maxY, float duration)
    {
        if (cameraComponent == null) cameraComponent = GetComponent<Camera>();
        if (cameraComponent == null) return;

        float size = ComputeOrthographicSizeFor(minX, maxX, minY, maxY);
        Vector3 center = new Vector3(
            (minX + maxX) * 0.5f,
            (minY + maxY) * 0.5f,
            transform.position.z);
        AnimateTo(center, size, duration);
    }

    /// <summary>回到当前场景字段（sceneMinX…）的完整取景。</summary>
    public void AnimateToSceneFraming(float duration)
    {
        AnimateToBounds(sceneMinX, sceneMaxX, sceneMinY, sceneMaxY, duration);
    }

    /// <summary>
    /// 开场特写：先切到 fromSize 的中景，再在 duration 内推到 toSize。
    /// 只改镜头，不改场景边界。
    /// </summary>
    public void FocusOn(Vector3 center, float fromSize, float toSize, float duration)
    {
        if (cameraComponent == null) cameraComponent = GetComponent<Camera>();
        if (cameraComponent == null) return;

        CancelAnimation();
        transform.position = new Vector3(center.x, center.y, transform.position.z);
        cameraComponent.orthographicSize = Mathf.Max(0.01f, fromSize);
        AnimateTo(center, toSize, duration);
    }

    public void CancelAnimation()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }
        animating = false;
    }

    private IEnumerator AnimateRoutine(Vector3 targetPosition, float targetSize, float duration)
    {
        animating = true;

        Vector3 startPosition = transform.position;
        float startSize = cameraComponent.orthographicSize;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            transform.position = Vector3.Lerp(startPosition, targetPosition, eased);
            cameraComponent.orthographicSize = Mathf.Lerp(startSize, targetSize, eased);
            yield return null;
        }

        transform.position = targetPosition;
        cameraComponent.orthographicSize = targetSize;
        lastAspect = cameraComponent.aspect;
        animating = false;
        animationRoutine = null;
    }

}
