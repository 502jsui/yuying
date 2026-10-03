using UnityEngine;

public class DoorLightView : MonoBehaviour
{
    public SpriteRenderer lightRenderer;

    public void SetWarmth(float warmth)
    {
        float t = Mathf.Clamp01(warmth / 60f);
        lightRenderer.color = Color.Lerp(
            new Color(0.6f, 0.6f, 0.7f),
            new Color(1f, 0.85f, 0.5f), t);
    }
}