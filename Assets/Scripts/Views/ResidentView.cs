using UnityEngine;

public class ResidentView : MonoBehaviour
{
    [Header("身份")]
    public ResidentId id;

    [Header("引用")]
    public SpriteRenderer bodyRenderer;
    public Transform doorLight;
    public SpriteRenderer doorLightRenderer;

    [Header("颜色配置")]
    public Color warmthLow = new Color(0.6f, 0.6f, 0.7f);
    public Color warmthHigh = new Color(1f, 0.85f, 0.5f);

    [HideInInspector] public ResidentData data;

    public void RefreshVisual()
    {
        if (data == null) return;
        float t = Mathf.Clamp01(data.warmth / 60f);
        if (bodyRenderer) bodyRenderer.color = Color.Lerp(warmthLow, warmthHigh, t);
        if (doorLightRenderer) doorLightRenderer.color = Color.Lerp(warmthLow, warmthHigh, t);
    }
}