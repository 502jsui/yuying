using UnityEngine;

[DisallowMultipleComponent]
public class RainRipple2D : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private float life;
    private float age;
    private float startScale;
    private float endScale;

    public void Initialize(SpriteRenderer targetRenderer, Color color, float duration, float scaleStart, float scaleEnd)
    {
        spriteRenderer = targetRenderer;
        baseColor = color;
        life = Mathf.Max(0.01f, duration);
        startScale = scaleStart;
        endScale = scaleEnd;
        transform.localScale = new Vector3(startScale, startScale * 0.32f, 1f);
        spriteRenderer.color = baseColor;
    }

    private void Update()
    {
        if (spriteRenderer == null) return;

        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / life);
        float scale = Mathf.Lerp(startScale, endScale, t);
        transform.localScale = new Vector3(scale, scale * 0.32f, 1f);

        Color color = baseColor;
        color.a *= 1f - t;
        spriteRenderer.color = color;

        if (age >= life) Destroy(gameObject);
    }
}

