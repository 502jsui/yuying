using UnityEngine;

/// <summary>
/// 正式全景背景。普通场景保存自己的取景，NPC 行动拼接时临时切到整张全景。
/// </summary>
[DisallowMultipleComponent]
public class PanoramaBackdropView : MonoBehaviour
{
    public AreaId area;
    public SpriteRenderer spriteRenderer;

    public Vector3 normalPosition;
    public Vector3 normalScale = Vector3.one;
    public Vector3 stitchedPosition = new Vector3(9.6f, 1.76f, 0f);
    public Vector3 stitchedScale = Vector3.one;

    private bool captured;

    public void Configure(AreaId targetArea, SpriteRenderer renderer)
    {
        area = targetArea;
        spriteRenderer = renderer;
        normalPosition = transform.position;
        normalScale = transform.localScale;
        stitchedPosition = SceneLayoutMetrics.StitchedBackdropPosition;
        stitchedScale = SceneLayoutMetrics.BackdropScale;
        captured = true;
    }

    public void CaptureNormalPose()
    {
        if (captured) return;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        normalPosition = transform.position;
        normalScale = transform.localScale;
        stitchedPosition = SceneLayoutMetrics.StitchedBackdropPosition;
        stitchedScale = SceneLayoutMetrics.BackdropScale;
        captured = true;
    }

    public void ApplyStitchedPose()
    {
        ApplyStitchedPose(Vector3.zero);
    }

    public void ApplyStitchedPose(Vector3 worldOffset)
    {
        CaptureNormalPose();
        transform.position = stitchedPosition + worldOffset;
        transform.localScale = stitchedScale;
        SetVisible(true);
    }

    public void RestoreNormalPose()
    {
        CaptureNormalPose();
        transform.position = normalPosition;
        transform.localScale = normalScale;
        SetVisible(true);
    }

    public void SetVisible(bool visible)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) spriteRenderer.enabled = visible;
    }

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        CaptureNormalPose();
    }
}
