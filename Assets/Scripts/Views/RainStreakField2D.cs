using UnityEngine;

[DisallowMultipleComponent]
public class RainStreakField2D : MonoBehaviour
{
    public Sprite streakSprite;
    public int streakCount = 120;
    public float width = 60f;
    public float height = 14f;
    public float groundY = -3.4f;
    public Vector2 velocity = new Vector2(6f, -22f);
    public float streakWidth = 0.018f;
    public float streakLength = 0.45f;
    public Color color = new Color(0.62f, 0.78f, 1f, 0.25f);
    public int sortingOrder = -12;
    public float rotationJitter = 0.04f;
    public float respawnPadding = 1.5f;

    public int StreakCount { get { return streakCount; } }

    private Transform[] streaks;
    private SpriteRenderer[] renderers;
    private bool configured;

    public void Configure(Sprite sprite, int count, float fieldWidth, float fieldHeight, float fieldGroundY, Vector2 fieldVelocity, float visualWidth, float visualLength, Color visualColor, int order, float jitter)
    {
        streakSprite = sprite;
        streakCount = Mathf.Max(1, count);
        width = fieldWidth;
        height = fieldHeight;
        groundY = fieldGroundY;
        velocity = fieldVelocity;
        streakWidth = visualWidth;
        streakLength = visualLength;
        color = visualColor;
        sortingOrder = order;
        rotationJitter = jitter;
        configured = true;
        Build();
    }

    private void OnEnable()
    {
        if (configured && (streaks == null || streaks.Length != streakCount)) Build();
    }

    private void Update()
    {
        if (streaks == null || streaks.Length == 0) return;

        Vector3 origin = transform.position;
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float floorY = origin.y - halfHeight - respawnPadding;

        float dt = Time.deltaTime;
        for (int i = 0; i < streaks.Length; i++)
        {
            Transform streak = streaks[i];
            if (streak == null) continue;

            streak.position += (Vector3)(velocity * dt);

            // 用雨场自身的范围做回收，保证雨覆盖整个雨场（包括屏幕最底部）。
            if (streak.position.y < floorY ||
                streak.position.x < origin.x - halfWidth - respawnPadding ||
                streak.position.x > origin.x + halfWidth + respawnPadding)
            {
                Respawn(i, true);
            }
        }
    }

    private void Build()
    {
        if (streakSprite == null) return;

        if (streaks != null)
        {
            for (int i = 0; i < streaks.Length; i++)
            {
                if (streaks[i] != null) Destroy(streaks[i].gameObject);
            }
        }

        streaks = new Transform[streakCount];
        renderers = new SpriteRenderer[streakCount];

        for (int i = 0; i < streakCount; i++)
        {
            GameObject streak = new GameObject("Rain_Streak_" + i);
            streak.transform.SetParent(transform, false);
            SpriteRenderer renderer = streak.AddComponent<SpriteRenderer>();
            renderer.sprite = streakSprite;
            renderer.sortingOrder = sortingOrder;
            streaks[i] = streak.transform;
            renderers[i] = renderer;
            Respawn(i, false);
        }
    }

    private void Respawn(int index, bool fromTop)
    {
        Transform streak = streaks[index];
        if (streak == null) return;

        Vector3 origin = transform.position;
        float x = origin.x + Random.Range(-width * 0.5f, width * 0.5f);
        float y = fromTop
            ? origin.y + height * 0.5f + Random.Range(0f, 2f)
            : origin.y + Random.Range(-height * 0.5f, height * 0.5f);

        streak.position = new Vector3(x, y, 0f);
        streak.localScale = new Vector3(streakWidth * Random.Range(0.85f, 1.15f), streakLength * Random.Range(0.75f, 1.25f), 1f);

        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f;
        angle += Random.Range(-rotationJitter, rotationJitter) * Mathf.Rad2Deg;
        streak.localRotation = Quaternion.Euler(0f, 0f, angle);

        Color streakColor = color;
        streakColor.a *= Random.Range(0.72f, 1.08f);
        renderers[index].color = streakColor;
    }
}
