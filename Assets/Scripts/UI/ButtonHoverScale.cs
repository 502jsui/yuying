using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 鼠标悬停时把按钮（或指定物体）平滑放大。
///
/// 用「指针是否落在矩形内」判断，【不依赖射线目标】——
/// 所以文本、图标、子物体、透明 Image 都不会影响它，
/// 鼠标在字形之间的空隙里也照样触发。
///
/// 【注意】用 unscaledDeltaTime：暂停菜单里 timeScale = 0 也要能动。
/// </summary>
[DisallowMultipleComponent]
public class ButtonHoverScale : MonoBehaviour
{
    [Tooltip("鼠标移上去时的放大倍数（1.08 = 放大 8%）")]
    public float hoverScale = 1.08f;

    [Tooltip("缩放速度，越大越快")]
    public float speed = 12f;

    [Tooltip("要缩放的物体。留空 = 自己")]
    public RectTransform scaleTarget;

    [Tooltip("鼠标判定区域。留空 = 用 scaleTarget 自己的矩形")]
    public RectTransform hitArea;

    [Tooltip("不可点时不变大。留空会自动找同物体上的 Button")]
    public Button button;

    private RectTransform rect;
    private Vector3 baseScale;
    private float target = 1f;

    private void Awake()
    {
        if (scaleTarget == null) scaleTarget = transform as RectTransform;
        if (hitArea == null) hitArea = scaleTarget;
        if (button == null) button = GetComponent<Button>();

        rect = scaleTarget;
        baseScale = rect != null ? rect.localScale : Vector3.one;
    }

    private void OnDisable()
    {
        target = 1f;
        if (rect != null) rect.localScale = baseScale;
    }

    private void Update()
    {
        if (rect == null || hitArea == null || baseScale.x == 0f) return;

        bool over = IsPointerOver();
        if (button != null && !button.interactable) over = false;
        target = over ? hoverScale : 1f;

        float k = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
        rect.localScale = baseScale * Mathf.Lerp(rect.localScale.x / baseScale.x, target, k);
    }

    private bool IsPointerOver()
    {
        // Screen Space - Overlay 的 Canvas：eventCamera 传 null
        Camera cam = null;
        Canvas canvas = hitArea.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(hitArea, Input.mousePosition, cam);
    }
}