using UnityEngine;

/// <summary>
/// 场景相机。按《余音_相机规范》：
///   - 相机固定，不跟随玩家；
///   - 场景与屏幕等大：整个场景完整地填满一屏（16:9 设计分辨率下是精确等大）。
///
/// 做法：给出场景在世界坐标里的矩形范围，相机自动取
///   size = max(场景高/2, 场景宽/(2*屏幕宽高比))
/// 这样整个场景一定可见，并且在目标宽高比下刚好铺满屏幕。
/// NPC 行动阶段因为整个场景本来就已经在画面里，所以不需要再拉远。
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

    [Header("NPC 行动阶段再拉远（可选）")]
    [Tooltip("场景已经和屏幕等大时保持关闭；如果以后有比一屏更大的场景再打开。")]
    public bool zoomOutForNpcAction = false;

    private Camera cameraComponent;
    private float lastAspect;

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        ApplyFraming();
    }

    private void OnEnable()
    {
        ApplyFraming();
    }

    private void LateUpdate()
    {
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
        float aspect;
        if (!Application.isPlaying)
        {
            // 编辑期 Game 视图比例不一定是 16:9，统一按设计比例算，
            // 否则保存进场景的相机尺寸会随编辑器窗口变化。
            aspect = DesignAspect;
        }
        else
        {
            aspect = cameraComponent != null && cameraComponent.aspect > 0.01f
                ? cameraComponent.aspect
                : DesignAspect;
        }

        float width = SceneWidth + padding * 2f;
        float height = SceneHeight + padding * 2f;

        float sizeByHeight = height * 0.5f;
        float sizeByWidth = width / (2f * aspect);
        return Mathf.Max(sizeByHeight, sizeByWidth);
    }

    /// <summary>把相机固定到场景中心，并取景到整个场景。</summary>
    public void ApplyFraming()
    {
        if (cameraComponent == null) cameraComponent = GetComponent<Camera>();
        if (cameraComponent == null) return;

        float centerX = (sceneMinX + sceneMaxX) * 0.5f;
        float centerY = (sceneMinY + sceneMaxY) * 0.5f;
        transform.position = new Vector3(centerX, centerY, transform.position.z);

        cameraComponent.orthographic = true;
        cameraComponent.orthographicSize = ComputeOrthographicSize();
        lastAspect = cameraComponent.aspect;
    }

    /// <summary>NPC 行动阶段：只有明确打开 zoomOutForNpcAction 时才再拉远。</summary>
    public void SetWide(bool wide)
    {
        if (!zoomOutForNpcAction) return;
        if (cameraComponent == null) cameraComponent = GetComponent<Camera>();
        if (cameraComponent == null) return;

        float target = ComputeOrthographicSize();
        if (wide) target *= 1.15f;
        cameraComponent.orthographicSize = target;
    }
}
